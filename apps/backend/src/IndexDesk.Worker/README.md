# IndexDesk.Worker

> Host de background (.NET 9 + **Quartz.NET**) que faz a ingestão de dados externos.
> É o **único** lugar do sistema que fala com provedores externos — **local-first é NON-NEGOTIABLE**:
> nenhuma request de usuário dispara chamada externa; tudo chega aqui por schedule ou CLI e vira
> linha no Postgres (upsert idempotente). Convenções detalhadas: [`CLAUDE.md`](./CLAUDE.md).
> Regras do módulo de ingestão: [`../Modules/IndexDesk.Modules.MarketData/CLAUDE.md`](../Modules/IndexDesk.Modules.MarketData/CLAUDE.md).

## Como rodar

```bash
# direto:
dotnet run --project src/IndexDesk.Worker          # a partir da raiz do repo
dotnet watch --project apps/backend/src/IndexDesk.Worker   # hot-reload

# via Turbo na raiz (sobe web + backend juntos):
bun run dev
```

O host sobe schedulers Quartz e fica rodando. Redis caiu? Fallback em memória mantém o host vivo.

## Jobs Quartz registrados

Registrados em [`Program.cs`](./Program.cs) (fonte da verdade). Crons em **UTC**.

| Job | Cron (UTC) | Serviço do módulo chamado |
| :--- | :--- | :--- |
| `BcbSyncJob` | `0 0 23 ? * * *` (diário, 23:00) | `IMacroEconomicSyncService.SyncAllAsync` (CDI/Selic/IPCA/IGP-M) |
| `MarketDataDailySyncJob` | `0 0 22 ? * MON-FRI *` (19:00 BRT) | `IDailyCloseSyncService.SyncDailyCloseAsync` (Brapi batch + gap fill Yahoo → TV) |
| `FxRatesDailySyncJob` | `0 5 22 ? * MON-FRI *` (22:05) | `IFxRateSyncService.SyncLatestAsync` (AwesomeAPI USD/EUR/BTC-BRL) |
| `TradingViewDailySyncJob` | `0 30 22 ? * MON-FRI *` (22:30) | `ITradingViewRefreshSyncService.RefreshAsync` (tickers não cobertos/defasados) |
| `HoldingsWeeklySyncJob` | `0 0 8 ? * SAT *` (sábado, 08:00) | `IEtfHoldingsSyncService.SyncWeeklyAsync` (iShares/SPDR/It Now/Investo) |
| `PilotAssetBackfillJob` | sem trigger (manual, `StoreDurably`) | `IAssetBackfillService.BackfillPilotAssetsAsync` |

Detalhe de cada arquivo de job: [`Jobs/CLAUDE.md`](./Jobs/CLAUDE.md).

## Backfill on-demand (CLI)

Roda **antes** dos schedulers (não sobe Quartz) e encerra o processo ao terminar:

```bash
dotnet run --project src/IndexDesk.Worker -- --backfill WRLD11        # ou -b (cadeia Brapi → Yahoo → TV)
dotnet run --project src/IndexDesk.Worker -- -b IVVB11 --provider yahoo
dotnet run --project src/IndexDesk.Worker -- -b MXRF11,IBOV --days 365  # janela móvel de N dias
```

- `--backfill` / `-b TICKER` — aceita lista separada por vírgula (`MXRF11,GOLD11`). Sem ticker = `WRLD11`.
- `--provider` / `-p` — força fonte única: `brapi|yahoo|tv|infomoney` (aliases aceitos, ex.: `yf`, `tradingview`).
- `--days N` — janela móvel (`hoje − N → hoje`) para todos os tickers da lista, sobrescrevendo as
  datas iniciais do switch por ticker (ex.: refresh de 1 ano do catálogo sem refazer histórico cheio).
- Upsert idempotente: seguro reexecutar/derrubar no meio.

## Configuração

Via variáveis de ambiente / `.env` (nunca valores reais commitados — `appsettings.json` só tem defaults dev):

| Variável | Uso |
| :--- | :--- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL/TimescaleDB (destino da ingestão) |
| `ConnectionStrings__Redis` | cache pós-ingestão (fallback in-memory se indisponível) |
| `Providers__*` | knobs de provedores: chaves, rate limits, resiliência (`Providers__Resilience__*`), sidecar, tickers (`Providers__Sync__TradingViewTickers`) |

Rate limits, TTLs e regras por provider: [`../../../../PROVIDERS.md`](../../../../PROVIDERS.md).

## Auditoria

Cada execução grava linha em `sync_job_logs` (status SUCCESS/FAILED/PARTIAL_WARNING, contadores,
duração) — base do health check de providers.
