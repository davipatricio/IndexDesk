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
| `MarketDataDailySyncJob` | `0 0 22 ? * MON-FRI *` (19:00 BRT) | MarketDataIngest | `IAssetSyncService.SyncDailyQuotesAsync` (cotações + dividendos pós-fechamento) |
| `PilotAssetBackfillJob` | sem trigger (manual) | Maintenance (`StoreDurably`) | `IAssetBackfillService.BackfillPilotAssetsAsync` |

## Modo CLI de backfill (on-demand)

```bash
dotnet run --project src/IndexDesk.Worker -- --backfill WRLD11     # ou -b
dotnet run --project src/IndexDesk.Worker -- --backfill MXRF11,GOLD11
```

- Roda antes de `host.Run()` e **encerra o processo** ao terminar (não sobe schedulers).
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
- Resiliência: `BuildingBlocks.Resilience/ResiliencePipelines.cs` já expõe `CreateDefaultHttpPipeline`
  (retry exponencial c/ jitter + timeout 10s). Circuit breaker/rate limiter por provider ainda **não**
  estão wired nos serviços — ao conectar, faça no serviço do módulo, não no job.
- TTLs de cache após ingestão: histórico fechado 30d · intraday 15min · macro 24h · holdings 7d (DEC-002).
- Segredos em `.env`/env vars (`ConnectionStrings__*`, `Providers__*`); `appsettings.json` só defaults dev.
- Nunca logar segredos nem dados pessoais nos logs/spans OTel.
