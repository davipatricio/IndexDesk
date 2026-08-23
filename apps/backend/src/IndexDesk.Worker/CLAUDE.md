# IndexDesk.Worker — Ingestão & Jobs Quartz (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md) e [`../../CLAUDE.md`](../../CLAUDE.md).
> Este host é o **único** lugar que fala com provedores externos (local-first, NON-NEGOTIABLE).

## Composição do `Program.cs` (ordem importa)

1. `AddIndexDeskObservability` (sempre primeiro).
2. DbContext Npgsql (`DefaultConnection` → fallback `Postgres` → default dev local).
3. Redis em try/catch: conectou → `RedisCacheService`; senão → `WorkerInMemoryCacheFallback`
   (classe definida no fim do próprio Program.cs — preservar o fallback, nunca derrubar o host).
4. `AddMarketDataModule(configuration)` — a lógica de ingestão vive no **módulo**
   (`Modules/IndexDesk.Modules.MarketData/Ingestion/`), não aqui.
5. Quartz jobs/triggers + `AddQuartzHostedService(WaitForJobsToComplete = true)`.

## Jobs registrados

| Job | Cron | Grupo | Serviço chamado |
| :--- | :--- | :--- | :--- |
| `BcbSyncJob` | `0 0 23 ? * * *` (23:00 UTC diário) | MarketDataIngest | `IMacroEconomicSyncService.SyncAllAsync` (CDI 12, Selic 11, IPCA 433, IGP-M 189) |
| `MarketDataDailySyncJob` | `0 0 22 ? * MON-FRI *` (19:00 BRT) | MarketDataIngest | `IDailyCloseSyncService.SyncDailyCloseAsync` — UMA chamada batch Brapi → fila de proventos espaçada ≥7 s → gaps por ticker via Yahoo sidecar → TradingView sidecar |
| `FxRatesDailySyncJob` | `0 5 22 ? * MON-FRI *` (22:05 UTC) | MarketDataIngest | `IFxRateSyncService.SyncLatestAsync` (AwesomeAPI USD/EUR/BTC-BRL; bid = proxy de close em `fx_rates`) |
| `TradingViewDailySyncJob` | `0 30 22 ? * MON-FRI *` (22:30 UTC) | MarketDataIngest | `ITradingViewRefreshSyncService.RefreshAsync` (tickers não cobertos/defasados; config `Providers:Sync:TradingViewTickers`) |
| `HoldingsWeeklySyncJob` | `0 0 8 ? * SAT *` (sáb 08:00 UTC) | MarketDataIngest | `IEtfHoldingsSyncService.SyncWeeklyAsync` (iShares CSV, SPDR XLSX, It Now JSON-first via sidecar `fetch` + fallback HTML, Investo HTML → upsert idempotente em `etf_holdings`; fonte que falha = PARTIAL_WARNING e o job segue) |
| `PilotAssetBackfillJob` | sem trigger (manual) | Maintenance (`StoreDurably`) | `IAssetBackfillService.BackfillPilotAssetsAsync` |

Cadeia declarativa OHLCV (`IMarketDataClient`, ordenada por Priority): **Brapi (1) → YahooSidecar (2) → TradingViewSidecar (3)**. HG Brasil fora da cadeia (pago). InfoMoney fora da cadeia — secundária, só via backfill explícito.

## Modo CLI de backfill (on-demand)

```bash
dotnet run --project src/IndexDesk.Worker -- --backfill WRLD11     # ou -b (cadeia completa)
dotnet run --project src/IndexDesk.Worker -- --backfill MXRF11,GOLD11
dotnet run --project src/IndexDesk.Worker -- -b IVVB11 --provider yahoo   # yahoo|tv|infomoney|brapi
```

- Roda antes de `host.Run()` e **encerra o processo** ao terminar (não sobe schedulers).
- `--provider` (`-p`) força uma fonte única via `SidecarProviderDirectory`; upsert idempotente = seguro reiniciar.
- Datas iniciais por ticker num switch em Program.cs (MXRF11 2015; VWRA11/WRLD11 2021; GOLD11 2020;
  default 2021) — atualizar o switch ao adicionar piloto novo.

## Padrão para adicionar um job (seguir exatamente)

1. Criar `Jobs/<Nome>SyncJob.cs`: `sealed`, `IJob`, `[DisallowConcurrentExecution]`.
2. Construtor injeta **o serviço do módulo** + `ILogger`. O Worker é casca fina: regra de negócio/
   acesso a provider/COPY ficam no módulo (`Ingestion/`), nunca no job.
3. `Execute` passa `context.CancellationToken` adiante e loga pelo padrão `Result`:
   sucesso → resumo (linhas/duração/status); falha → `LogError` com mensagem. Um provider que falha
   **não pode** interromper os demais.
4. Registrar JobKey + trigger na seção Quartz do Program.cs (grupo `MarketDataIngest` para ingestão,
   cron em UTC). Jobs manuais: `.StoreDurably()` e sem trigger.
5. O serviço do módulo grava auditoria em `sync_job_logs` (`SyncJobLogEntity`: status
   SUCCESS/FAILED/PARTIAL_WARNING, contadores processed/updated/skipped, duração) — a saúde dos
   providers (`ProviderHealthAggregator`) agrega essas linhas. Falha ao gravar o log só gera warning,
   nunca derruba a execução.
6. Idempotência obrigatória: reexecutar o mesmo dia atualiza (upsert), nunca duplica.

## Regras fixas

- Provedores externos são acessados **apenas daqui** (via serviços do módulo) — nunca do Api/request path.
- Resiliência (wired na Fase 4 provider-sync, tudo no módulo, não no job):
  - **Pool de chaves** (`IApiKeyPool`/`InMemoryApiKeyPool`, singleton): round-robin entre chaves
    saudáveis; cooldown após 429/quota (honra `Retry-After`; quota diária dorme até o próximo dia
    UTC); chave `Invalid` (401/403) sai da rotação até reinício. Token bucket por chave vive
    DENTRO do pool (`Acquire` consome token; Brapi 10 req/min/chave default). Aplica-se a limites
    **por chave** (Brapi/AwesomeApi/InfoMoney); Yahoo/TV são **por IP** — lá vale espaçamento
    fixo (200 ms no DailyClose) + breaker.
  - **Breaker por provider** (`ProviderResilience` + `ResiliencePipelines.CreateProviderCallPipeline`,
    knobs em `Providers:Resilience:*`): circuito aberto responde códigos **soft**
    `Provider.CircuitOpen`; pool esgotado responde `Provider.PoolExhausted`. Ambos viram
    **PARTIAL_WARNING** no estágio (`DailyCloseChain.StatusFor`) e disparam failover pro próximo
    slot da cadeia (Brapi → YahooSidecar → TVSidecar) — nunca erro duro de job.
  - **Taxonomia:** `.RateLimit` = retry sim/breaker não · `Scrape.WafBlocked`/`*.AuthFailed` =
    breaker sim/retry não · `Sidecar.Timeout/FetchFailed/.HttpError/.Exception` = ambos ·
    `*.NoApiKey/.NoData/PoolExhausted/ParseError/Usage/SpawnFailed` = pass-through. Sidecar tem
    1 retry só em Timeout/FetchFailed (`Providers:Sidecar:MaxRetries`, default 1).
- TTLs de cache após ingestão: histórico fechado 30d · intraday 15min · macro 24h · holdings 7d (DEC-002).
- Segredos em `.env`/env vars (`ConnectionStrings__*`, `Providers__*`); `appsettings.json` só defaults dev
  (cookie TV, chaves Brapi/AwesomeApi/InfoMoney **nunca** em appsettings commitado).
- Nunca logar segredos nem dados pessoais nos logs/spans OTel (pool loga índice `#{Index}`, nunca valor;
  cookie TV vai no argv do filho, key InfoMoney no env do filho).
