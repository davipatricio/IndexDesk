# Features Atuais — Inventário do que JÁ existe implementado

> Snapshot 2026-08-22. **Antes de usar/estender uma superfície, confirme aqui ou no código.**
> Este arquivo é atualizado sempre que uma feature entra/sai (ver seção "Skills internas" do CLAUDE.md raiz).
> Estado macro de fases/tarefas: [`project-state.md`](project-state.md).

## API .NET (`apps/backend/src/IndexDesk.Api`)

### `/api/v1/auth` (módulo Auth)
`POST /signup` · `POST /signin` · `POST /refresh` · `POST /signout` · `GET /me` (autenticado).
Detalhes/contratos: [`../../../../apps/backend/src/Modules/IndexDesk.Modules.Auth/CLAUDE.md`](../../../../../apps/backend/src/Modules/IndexDesk.Modules.Auth/CLAUDE.md)
(resumo também na skill: hashing Argon2id, rotação de refresh, RBAC `PERMISSION:`).

### `/api/v1/assets` (módulo MarketData)
| Rota | Nome | Função |
| :--- | :--- | :--- |
| `POST /sync/daily` | TriggerDailySync | dispara sincronização diária de cotações/dividendos |
| `POST /sync/backfill` | TriggerPilotBackfill | backfill dos pilotos (MXRF11, VWRA11, GOLD11…) |
| `POST /sync/macro` | TriggerMacroSync | dispara sync BCB (CDI/Selic/IPCA/IGP-M) |
| `GET /` | GetAssets | catálogo paginado |
| `GET /{ticker}` | GetAssetByTicker | detalhe do ativo (metadados + fiscal) |
| `GET /{ticker}/quotes` | GetAssetQuotes | série histórica |
| `GET /{ticker}/performance` | GetAssetPerformance | métricas de performance |

### `/api/v1/analytics` (módulo Analytics)
- `POST /backtest` — simulação com pesos/aportes.
- `GET /real-yield?nominalRate&inflationRate` — rendimento real (Fisher).

### `/api/v1/providers` (MarketData)
- `GET /health` — saúde por provider agregada de `sync_job_logs`.

### Infra
`GET /health` · Scalar em `/scalar/v1` (Development only).

## Jobs do Worker (`IndexDesk.Worker`)
| Job | Agenda (UTC) | Serviço |
| :--- | :--- | :--- |
| `BcbSyncJob` | diário 23:00 | `IMacroEconomicSyncService.SyncAllAsync` |
| `MarketDataDailySyncJob` | Mon–Fri 22:00 | `IAssetSyncService.SyncDailyQuotesAsync` |
| `PilotAssetBackfillJob` | sem agenda (manual/CLI) | `IAssetBackfillService.BackfillPilotAssetsAsync` |

CLI on-demand: `dotnet run --project src/IndexDesk.Worker -- --backfill TICKER[,TICKER2]`.
Serviços de ingestão persistem auditoria em `sync_job_logs`. Polly: apenas pipeline default definido
(`ResiliencePipelines.cs`) — circuit breaker/rate limiter por provider **ainda não wired**.

## Frontend (`apps/web/src/app`) — páginas com implementação atual

| Rota | Descrição |
| :--- | :--- |
| `(public)/ativos` + `/ativos/[ticker]` | catálogo e página de ativo genérica |
| `(public)/etf/[ticker]`, `(public)/bdr/[ticker]` | páginas por classe (painel fiscal, TradingView link) |
| `(public)/comparador` | comparador multi-ativos |
| `(public)/ferramentas/backtest` | simulador de backtest público |
| `(public)/ferramentas/rendimento-real` | calculadora de rendimento real (Fisher) |
| `(admin)/admin` | painel admin (uma página; subrotinas de curadoria/uploads pendentes) |
| `~offline`, `serwist/[path]` | fallback offline PWA |

**Ainda não existem:** `/noticias`, `/relatorios`, `/entrar` (sem page.tsx), overlap/tax drag/DARF/
aposentadoria/fluxo-CVM, sitemaps/OG dinâmicos, hubs editoriais (MVP-012..018, MVP-021/022 ⬜).

## Pendências conhecidas de ambiente

Migrations FND-013 não aplicadas (containers Docker parados por decisão do dono); SDK .NET local usa
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; advisory NU1902 no pacote OTLP exporter.
