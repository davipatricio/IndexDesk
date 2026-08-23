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
| `POST /sync/backfill` | TriggerPilotBackfill | backfill dos pilotos + benchmarks (MXRF11, VWRA11, GOLD11, WRLD11, IBOV, IFIX) |
| `POST /sync/macro` | TriggerMacroSync | dispara sync BCB (CDI/Selic/IPCA/IGP-M) |
| `GET /` | GetAssets | catálogo paginado (exclui `AssetType="INDEX"` salvo `assetType=INDEX` explícito) |
| `GET /rankings` | GetAssetRankings | ranking por métrica (`retorno12m/30d/6m/ano`, `variacaodia`, `volatilidade`, `sharpe`, `drawdown`, `volume`); sem dados na métrica → última posição; exclui INDEX salvo pedido explícito |
| `GET /market-indicators` | GetMarketIndicators | snapshot CDI/Selic/IPCA (séries 12/11/433) com acumulado 12m; cache Redis 24h; 404 se séries vazias |
| `GET /macro-series` | GetMacroRateSeries | janelas **brutas** de taxas (% por período) das séries macro locais (`codes=CDI,SELIC,IPCA&days=N`) p/ acumulação client-side (espelha `AccumulateRateSeries`); cache Redis 24h (DEC-002); resultado vazio não é cacheado; janela clamp 7–7300 dias |
| `GET /quotes/batch` | GetAssetQuotesBatch | janelas de fechamento (`tickers=A,B&days=90`, máx 50 tickers, 7–7300 dias) p/ sparklines/gráficos; cache 15 min; tickers desconhecidos omitidos |
| `GET /{ticker}` | GetAssetByTicker | detalhe do ativo (metadados + fiscal) |
| `GET /{ticker}/quotes` | GetAssetQuotes | série histórica |
| `GET /{ticker}/dividends` | GetAssetDividends | histórico completo de proventos + total 12m e DY 12m (`DividendCalculators`; cache Redis 30 min; 404 se sem eventos) |
| `GET /{ticker}/performance` | GetAssetPerformance | métricas de performance |

`GET /{ticker}` e rankings expõem também `avgVolume30D` (preço médio × volume das ~30 sessões recentes,
proxy de "negociação diária média"). Cache Redis de leitura: 10 min (lista e rankings).

**Benchmarks índice (IBOV/IFIX):** catálogo curado em `Modules.IndexDesk.MarketData/Ingestion/BenchmarkCatalog.cs`
— IBOV ("Ibovespa", Yahoo `^BVSP`, histórico desde 2015-01-01) e IFIX ("IFIX (Índice de Fundos
Imobiliários)", Yahoo `IFIX.SA`, **forward-only**: Yahoo não expõe histórico do índice, série local cresce
1 fechamento/pregão desde ago/2026). Tickers IBOV/IFIX são criados como `AssetType="INDEX"` pelo backfill
de forma idempotente — única exceção à regra "curadoria cria o ativo antes". `YahooFinanceClient.ToYahooSymbol`
mapeia IBOV→`^BVSP`; `BrapiClient.SupportsTicker` rejeita benchmarks (Yahoo only). INDEX fica fora de
catálogo/rankings públicos.

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

CLI on-demand: `dotnet run --project src/IndexDesk.Worker -- --backfill TICKER[,TICKER2]` — aceita os
pilotos e benchmarks (IBOV start 2015, IFIX start hoje-7d). Sync diário (`SyncDailyQuotesAsync` default)
cobre MXRF11, VWRA11, GOLD11, WRLD11, IBOV, IFIX. Backfill executado: IBOV 2890 cotações (2015→hoje,
YahooFinance); IFIX 1 ponto (forward-only).
Serviços de ingestão persistem auditoria em `sync_job_logs`. Polly: apenas pipeline default definido
(`ResiliencePipelines.cs`) — circuit breaker/rate limiter por provider **ainda não wired**.

## Frontend (`apps/web/src/app`) — páginas com implementação atual

| Rota | Descrição |
| :--- | :--- |
| `/` (home) | mini-dashboard de mercado: strip CDI/Selic/IPCA, movers 12m (altas/quedas via rankings), lista compacta de ferramentas — sem cards/KPI vazios |
| `(public)/ativos` + `/ativos/[ticker]` | catálogo denso e página de ativo redesenhada (fase C): header cotação + KPIs inline com tooltips + hero chart + simulação + fiscal por classe + cross-links |
| `(public)/etf/[ticker]`, `(public)/bdr/[ticker]`, `(public)/fii/[ticker]` | páginas por classe reexportam a página genérica; painel fiscal escolhido pela classe (`FiiTaxCard` p/ FII, `FiscalTaxCard` caso contrário) |
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

**Página de ativo (redesign fase C):** header denso (preço grande tabular, variação dia, badges
classe/moeda/CNPJ), linha de KPIs inline (retorno12m/vol anual/Sharpe/drawdown/volume médio-dia) **com
tooltips de ajuda** por indicador, hero chart (`components/charts/price-hero-chart.tsx`: lightweight-charts
v5 área de fechamento + histograma volume, período na URL via nuqs `p` 1M/3M/6M/1A/TUDO, cores resolvidas
de tokens oklch via `lib/chart-colors.ts`, display-only — scroll/scale off), painel fiscal por classe
(`FiiTaxCard` novo: rendimentos mensais isentos PF Lei 8.668/93, venda 20%, DARF 8968, sem isenção R$35k,
sem come-cotas, atenção a amortizações no preço médio), footer com cross-links
(comparador/backtest/rankings) + dados cadastrais compactos. Histórico buscado com `days=7300`.

**Seção "Simular investimento":** `components/assets/investment-simulation.tsx` ('use client') — valor
editável (`ScrubNumberField`, default R$1000), períodos 6M/1A/3A/TUDO, gráfico normalizado em R$ (recharts)
do ativo vs Ibovespa/IFIX/CDI (colunas do sumário funcionam como toggle on/off das linhas); fallback
automático p/ data de início do ativo quando mais novo que a janela (caso VWRA11); disclaimer educacional
(variação de preço, sem proventos/custos; Selic fora do gráfico por acompanhar o CDI). Motor puro testado:
`lib/simulation.ts` (+ `lib/__tests__/simulation.test.ts`, 10 casos) — alinha calendário do ativo com
benchmarks (ffill), converte CDI/Selic (`kind:'rate'`) em fatores acumulados espelhando
`AccumulateRateSeries`, normaliza tudo ao valor investido na âncora da janela; benchmarks sem cobertura
prévia da janela ficam indisponíveis (sem truncar). `lib/chart-colors.ts`
(+ `chart-colors.test.ts`, 5 casos) lê custom properties autorais e converte `oklch()`→hex p/ canvas
(lightweight-charts não parseia lab()/oklch()). DTOs/fetcher novos em `lib/api-client.ts`:
`MacroRatePointDto`/`MacroRateSeriesDto` + `fetchMacroRateSeries`; `avgVolume30D` em `AssetQuoteStatsDto`.

**Proventos na página do ativo:** `components/assets/dividend-summary.tsx` (server) — total 12m por
cota, DY 12m e nº de pagamentos com tooltips explicativos + `<details>` com os pagamentos mais
recentes; renderizada só quando `GET /{ticker}/dividends` tem eventos (404 = sem proventos locais,
estado válido tratado como null em `fetchAssetDividends`). Testes: `dividend-summary.test.tsx`,
`investment-simulation.test.tsx` (RTL, mocks das fetchers), `chart-period.test.ts`
(`lib/chart-period.ts`: fatiamento de período do hero chart compartilhado c/ componente).
Interações do hero chart: wheel scroll/zoom ligado; drag/arrasto desligados (`handleScroll`/
`handleScale` granulares) p/ evitar "arrastar pra fora"; legenda linha/barras sob o gráfico.

**Redesign:** plano de fases A/B/C em `~/.opencode/plan/frontend-redesign.md`. **A, B e C concluídas**
(2026-08-22). A (home dashboard + nav agrupada + palette); B: `GET /quotes/batch` (sparklines SVG
server-rendered em movers/rankings/catálogo), tabelas densas (header sticky, coluna ordenada destacada,
colunas 6m/drawdown no rankings, alinhamento tabular), chips de filtro ativo no rankings; C: página de
ativo redesenhada (header/KPIs c/ tooltips, hero chart lightweight-charts, simulação vs benchmarks,
painéis fiscais por classe, cross-links).
Registries aprovados na política (`apps/web/CLAUDE.md` §4.1): `@kinetic/scrub-number-field` já
integrado aos inputs numéricos do backtest; `@evilcharts`, `@kibo-ui/file-upload`, `@ogimagecn`
e editor rich text (MVP-017) entram junto das respectivas fases.

**Ainda não existem:** `/noticias`, `/relatorios`, screener avançado, premium/desconto vs PL, captação
líquida/CVM informe diário, overlap/tax drag/DARF/aposentadoria/fluxo-CVM, sitemaps/OG dinâmicos
(MVP-003 ⬜ reaberto, MVP-012..018, MVP-021/022, MVP-024/025 ⬜).

## Pendências conhecidas de ambiente

Containers Docker agora **rodando** e migrations aplicadas no banco local (tabelas `assets`,
`asset_quotes`, `asset_dividends`, `macro_economic_series`, `sync_job_logs` verificadas em 2026-08-22) —
a task FND-013 segue `not_started` no roadmap (aceite exige restaurar banco vazio); SDK .NET local usa
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; advisory NU1902 no pacote OTLP exporter.
