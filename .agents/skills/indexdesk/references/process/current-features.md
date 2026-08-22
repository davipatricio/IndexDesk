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
| `GET /rankings` | GetAssetRankings | ranking por métrica (`retorno12m/30d/6m/ano`, `variacaodia`, `volatilidade`, `sharpe`, `drawdown`, `volume`); sem dados na métrica → última posição |
| `GET /market-indicators` | GetMarketIndicators | snapshot CDI/Selic/IPCA (séries 12/11/433) com acumulado 12m; cache Redis 24h; 404 se séries vazias |
| `GET /{ticker}` | GetAssetByTicker | detalhe do ativo (metadados + fiscal) |
| `GET /{ticker}/quotes` | GetAssetQuotes | série histórica |
| `GET /{ticker}/performance` | GetAssetPerformance | métricas de performance |

`GET /{ticker}` e rankings expõem também `avgVolume30D` (preço médio × volume das ~30 sessões recentes,
proxy de "negociação diária média"). Cache Redis de leitura: 10 min (lista e rankings).

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
| `/` (home) | mini-dashboard de mercado: strip CDI/Selic/IPCA, movers 12m (altas/quedas via rankings), lista compacta de ferramentas — sem cards/KPI vazios |
| `(public)/ativos` + `/ativos/[ticker]` | catálogo e página de ativo genérica |
| `(public)/etf/[ticker]`, `(public)/bdr/[ticker]` | páginas por classe (painel fiscal, TradingView link) |
| `(public)/rankings` | ranking público por métrica/tipo com estado nuqs (`tipo`, `metrica`, `direcao`) |
| `(public)/comparador` | comparador multi-ativos |
| `(public)/ferramentas/backtest` | simulador de backtest público |
| `(public)/ferramentas/rendimento-real` | calculadora de rendimento real (Fisher) |
| `(admin)/admin` | painel admin (uma página; subrotinas de curadoria/uploads pendentes) |
| `~offline`, `serwist/[path]` | fallback offline PWA |

**Layout/nav:** navbar agrupada em dropdowns (Mercado/Ferramentas); command palette `Ctrl+K`
(`cmdk` via `components/ui/command.tsx`) busca páginas + ativos e substitui o popover antigo;
`<Toaster />` (sonner) montado em `providers.tsx`. Componentes UI novos: `command`, `tooltip`,
`sonner`, `scroll-area`, `alert-dialog` (base-nova/base-ui, escritos à mão onde o CLI conflitou).

**Redesign em andamento:** plano de fases A/B/C em
`~/.opencode/plan/frontend-redesign.md` (A concluída: home dashboard + nav agrupada + palette;
B: densidade de tabelas + sparklines batch; C: página de ativo).

**Ainda não existem:** `/noticias`, `/relatorios`, screener avançado, premium/desconto vs PL, captação
líquida/CVM informe diário, overlap/tax drag/DARF/aposentadoria/fluxo-CVM, sitemaps/OG dinâmicos
(MVP-003 ⬜ reaberto, MVP-012..018, MVP-021/022, MVP-024/025 ⬜).

## Pendências conhecidas de ambiente

Migrations FND-013 não aplicadas (containers Docker parados por decisão do dono); SDK .NET local usa
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; advisory NU1902 no pacote OTLP exporter.
