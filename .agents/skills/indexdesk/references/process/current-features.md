# Features Atuais — Inventário do que JÁ existe implementado

> Snapshot 2026-08-23 (pós provider-sync Fases 0–4; Fase 5 docs). **Antes de usar/estender uma superfície, confirme aqui ou no código.**
> Este arquivo é atualizado sempre que uma feature entra/sai (ver seção "Skills internas" do CLAUDE.md raiz).
> Estado macro de fases/tarefas: [`project-state.md`](project-state.md).

## API .NET (`apps/backend/src/IndexDesk.Api`)

### `/api/v1/auth` e `/api/v1/users` (módulo Auth)
`POST /signup` · `POST /signin` · `POST /refresh` · `POST /signout` · `GET /me` (autenticado, inclui `preferences`) · `PATCH /api/v1/users/me` (autenticado, toggle `hideValues`).
Detalhes/contratos: [`../../../../apps/backend/src/Modules/IndexDesk.Modules.Auth/CLAUDE.md`](../../../../../apps/backend/src/Modules/IndexDesk.Modules.Auth/CLAUDE.md)
(resumo também na skill: hashing Argon2id, rotação de refresh, RBAC `PERMISSION:`, preferências de usuário jsonb).

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

### Clientes sidecar Python (`Modules.MarketData/Clients/`, Fase 2 provider-sync)
Três `IMarketDataClient` que delegam o fetch ao processo Python `tools/providers/sidecar` via
`SidecarProcessRunner` (spawn uv, timeout 120 s default com kill da árvore, NDJSON no stdout,
erros como envelope JSON no stderr; exit 2/3/4 → `Sidecar.Usage/FetchFailed/ParseError`,
spawn falho → `Sidecar.SpawnFailed`). Desde a Fase 3 a cadeia declarativa `IMarketDataClient`
(Priority) é **Brapi (1) → YahooSidecar (2) → TradingViewSidecar (3)**; HG Brasil saiu da cadeia
(pago, client inativo); InfoMoney segue FORA da cadeia — secundária explícita:
- `YfinanceSidecarClient` (`YahooSidecar`, Priority 2) — `sidecar yf quotes|dividends`.
- `TradingViewSidecarClient` (`TradingView`, Priority 3) — `sidecar tv history`, prefixo
  `BMFBOVESPA:`, cookie de `Providers__TradingView__Cookie` (opcional; falha auth =
  `TradingView.AuthFailed`; dividends = vazio por design).
- `InfoMoneySidecarClient` (`InfoMoney`, Priority 4, secundária) — `sidecar im quotes|dividends`;
  key de `Providers__InfoMoney__SubscriptionKeys__0` injetada como env
  `INFOMONEY_SUBSCRIPTION_KEY` no filho; sem key = `InfoMoney.NoApiKey` sem spawn;
  403 Akamai = `Scrape.WafBlocked`, 401 = `InfoMoney.AuthFailed`; série diária **sem ajuste**
  (`adj_close=close`) e dividends com vocabulário B3 nativo (DIVIDENDO/JRSCAPPROPRIO...).
Config: `Providers__Sidecar__UvPath` (default `uv`), `Providers__Sidecar__ProjectPath`
(default: resolve subindo diretórios até `tools/providers/sidecar/pyproject.toml`),
`Providers__Sidecar__TimeoutSeconds` (default 120). Smoke manual permanente:
`SIDECAR_SMOKE=1 dotnet test --filter FullyQualifiedName~SidecarSmokeTests`.

### Catálogo oficial B3 no sidecar (`sidecar b3 ...`, 26/08/2026)

Novo módulo `b3_cmd.py`: consome os proxies JSON dos apps Angular que alimentam as páginas
públicas `empresas-listadas.htm` e `fiis-listados` (host `sistemaswebb3-listados.b3.com.br`,
Akamai + cookie warm-up, TLS chrome via curl_cffi). Comandos:

- `sidecar b3 companies [--page-size N] [--max-records N]` → NDJSON kind `company`
  (`cnpj`, `code_cvm`, `issuing_company`, `trading_name`, `market_indicator`, `date_listing`) —
  ~3500 registros ao vivo.
- `sidecar b3 fiis [--type FII] [...]` → kind `fund` (`ticker`, `name`, `fund_type`) —
  529 FIIs ao vivo; outros tipos via `assets/funds.json` do app (FIDC, FIP...).

Sem resolução de captcha: challenge duro = `Fetch.Failed`/403. Contrato v2 documentado no
README do sidecar; testes offline em `tests/test_b3_cmd.py`. Consumidor C#/persistência ainda
não existe — próximo passo natural é um client `B3CatalogSidecarClient` + job de curadoria.

### Recon de 5 sites de dados BR (26/08/2026) — spec em PROVIDERS.md §2.14–§2.18

Recon read-only com subagentes paralelos; relatórios completos versionados em
`tools/providers/recon/recon-{investidor10,maisretorno,fundsexplorer,clubefii,advfn}.md`.
Nenhum scraper/client implementado ainda — só conhecimento medido:

- **Investidor10 (ALTO enriquecimento):** APIs GET sem auth (`/api/cotacoes/batch`,
  `/api/historico-indicadores/{id}/…` 31 indicadores ×10y, balanços, comparador FIIs,
  índices/macros). Close-only — **sem volume**. IDs internos numéricos precisam resolução.
- **MaisRetorno (ALTO):** `__NEXT_DATA__`/`_next/data/{buildId}` públicos; rentabilidade
  mensal/anual desde a origem (PETR4/IBOV 1994); lista-acoes 528 tickers c/ CNPJ+code_cvm+ISIN;
  gestoras 3.236 c/ CNPJ; administradoras 334. Zero proventos/DY/PVP.
- **FundsExplorer (ALTO FIIs):** `POST /wp-admin/admin-ajax.php` actions `funds-get-income`
  (rendimentos 2016-06→), `funds-get-quotations` (diária ~5y), `funds-get-patrimonials`;
  nonce `data-nonce` do HTML; lista ~696 FIIs server-side.
- **ClubeFII (MÉDIO-ALTO):** XHR público `fundo_basico?cod=TICKER` (CNPJ/DY/PVP/cotistas/
  taxa adm), cotações mensais 2011–2026, lista 825 FIIs; rendimentos detalhados gated.
- **ADVFN (ALTO só proventos):** `/balanco/dividendos` server-rendered grátis (ações JCP/div +
  FII rendimentos); OHLCV inacessível por HTTP (WebSocket/ag-grid).

### InfoMoney keyless (26/08/2026)

`sidecar im ...` funciona **sem** `INFOMONEY_SUBSCRIPTION_KEY`: bootstrap faz warm-up GET na
página de cotação (obrigatório — Akamai agora rejeita chamadas stateless mesmo com TLS chrome)
e extrai a chave pública do frontend do blob `window.InfoMoneyPage`. Env/pool continua com
precedência quando configurada. Client C# spawna keyless (NoApiKey removido).

### FundsExplorer + MaisRetorno no sidecar (26/08/2026, implementado)

- **`sidecar fe income|quotations|patrimonials --symbol TICKER`** (curl_cffi chrome + nonce
  `data-nonce` do HTML). Mapeiam para ``dividend`` (income, desde 2016-06; 122 pontos KNCR11),
  ``quote`` (~5y diária, double-serialized, `volume=0`), e ``return_series``
  (`equity_per_share` mensal). Smoke medido: income 122 linhas OK.
- **`sidecar mr returns --symbol TICKER --kind acoes|etf|fii|indice`** (curl_cffi chrome,
  `_next/data/{buildId}` + `buildId` extraído da home). Emite `return_series`
  (mensal `YYYY-MM` + anual `YYYY`); WRLD11 → 65 linhas desde 2021. Novo kind
  `return_series` no NDJSON (`metric` distingue `return` de `equity_per_share`).

### Transporte HTTP genérico anti-WAF (`sidecar fetch` + `ISidecarHttp`, Fase 3 adendo)Hosts com fingerprinting TLS Akamai (ex.: `www.itnow.com.br`: curl/HttpClient nativo = 403
"Access Denied" mesmo com headers de browser; `curl_cffi impersonate="chrome"` = 200) são
servidos por um comando genérico do sidecar: `sidecar fetch --url URL [--method GET|POST]
[--data BODY] [--header "K: V"...] [--timeout-s N] [--b64]` — **corpo cru no stdout**
(texto; `--b64` para binário tipo XLSX), envelope no stderr com campo extra `status`
(`Scrape.WafBlocked` p/ 403, senão `Fetch.Failed`; ambos exit 3). Wrapper C#:
`ISidecarHttp`/`SidecarHttp` (singleton sobre o runner). Consumidor atual:
`ItNowHoldingsFeed` roteia página de composição + POST `history-api-json` pelo sidecar
por default (`Providers:Holdings:ItNow:Transport=sidecar|native`, default sidecar).
Smoke ao vivo 23/08: BOVV11 = 78 holdings, as-of 2026-08-21, top VALE3 11,2377%.

### FX e Holdings (Fase 3 provider-sync)
- `AwesomeApiClient`/`IAwesomeApiClient` — HttpClient nativo (sem sidecar);
  `GET json/last/{pares}` multi-par e `GET json/daily/{par}` histórico; contrato bid/ask com
  **bid = proxy de close**; token premium `Providers__AwesomeApi__Token` vai só como query param,
  nunca logado. Persistência em tabela `fx_rates` (PK pair+date, upsert idempotente) via
  `FxRateSyncService`.
- Holdings (`Ingestion/Holdings/`): parsers puros + feeds HttpClient — iShares CSV
  (link ajax extraído por regex da página do produto; mapa ticker→página em
  `Providers:Ishares:Products:{TICKER}`), SPDR XLSX via ClosedXML, It Now/Investo HTML via
  AngleSharp (`HtmlCompositionParser`: classifica tabela pelo header, ignora "País",
  Investo publica nomes sem tickers). It Now (JSON API + fallback HTML) vai por transporte
  **sidecar** (`ISidecarHttp`) — host bloqueia TLS nativo. `EtfHoldingsSyncService` faz upsert
  idempotente em `etf_holdings` (dedupe etf+as_of_date+ticker; linhas sem ticker dedupe por
  nome); fonte que falha = PARTIAL_WARNING e o job continua. Ao vivo 23/08: serviço SUCCESS
  4/4 contra o Postgres dev; WRLD11 upsertou 10 linhas idempotentes; backfill CLI
  `--backfill IVVB11 --provider yahoo` = 1405 quotes idempotentes (2021→2026).

### Catálogo & ingestão em massa (26/08/2026)

**Catálogo:** 2165 ativos ativos (`assets`) — STOCK 754 · BDR 508 · FII 350 · BDR_ETF 302 ·
ETF 249 · INDEX 2 (IBOV/IFIX). Curadoria operacional por duas fontes reais (sem metadado fabricado):
Brapi `/available` + `/quote/list` com token (`type`/`subType` → AssetType; nomes de fundo = ticker,
pobre mas verbatim da fonte) e **Bora Investir** WordPress REST (`?rest_route=/wp/v2/asset&_embed`,
519 tickers do universo ETF; nome = índice de referência; reclassificou 135 `BDR→BDR_ETF` e aportou
241 listagens ausentes do Brapi). Detalhes das fontes: `PROVIDERS.md` §2.5 e §2.12.
Upsert idempotente `ON CONFLICT ("Ticker") DO NOTHING`; os 7 originais preservados.

**Backfill 1y (CLI `--backfill <csv> --provider yahoo --days 365`, 4 processos paralelos):**
334.170 barras na janela 1y; cobertura **2118/2163 negociáveis (98%)** — STOCK 752/754 ·
FII 350/350 · BDR 508/508 · BDR_ETF 275/302 · ETF 233/249. Os **45 sem cobertura** são listagens
novíssimas ausentes de Yahoo **e** TradingView (só existem no site B3/Bora); ficarão cobertas
forward-only pelo batch diário Brapi `/quote/list`. Proventos: **31.919 eventos em 1.053 ativos**
(Yahoo tem histórico rico p/ ações/BDRs/FIIs; ETFs seguem lacuna da fonte).

**Fixes de ingestão que entraram junto (testes unitários no `MarketDataValidatorTests`):**
- `MarketDataValidator`: guard de magnitude — preços e rates ≥ 9_999_999 são lixo de provider
  (Yahoo mandou "dividendo" de R$190 mi/cota em PDGR3 2008-09) e estouravam o `decimal(14,6)`
  envenenando o audit-log no mesmo DbContext.
- `IngestionUpserts.DividendsAsync`: arredonda `Rate` para a escala da coluna
  (`AwayFromZero`, igual ao PG) + comparação com epsilon antes do insert — ruído de float do
  Yahoo (`2.0307215` vs armazenado `2.030722`) colidia no índice único `(AssetId, ComDate, Rate)`
  a partir do 2º run (23505). Idempotência real de re-runs restaurada.

### `/api/v1/analytics` (módulo Analytics)


- `POST /backtest` — simulação com pesos/aportes.
- `GET /real-yield?nominalRate&inflationRate` — rendimento real (Fisher).

### `/api/v1/portfolios` (módulo Portfolio — M-P1, PR `feat/portfolio-dashboard`)
Autenticado (JWT; policies `portfolio:read`/`portfolio:write`). Plano vivo:
`plans/dashboard-carteiras/` (decisões do grill, marcos M-P1..M-P5).
- `POST /` · `GET /` · `GET /{id}` (resumo+posições) · `PATCH /{id}` · `DELETE /{id}`
  — limite de **3 carteiras** por usuário (`Portfolio.LimitReached` → 409); ownership check
  retorna 404 em cross-user.
- `POST /{id}/transactions` · `GET /{id}/transactions` (paginado) ·
  `PUT /{id}/transactions/{txId}` (**edição lógica**: nova linha com `AmendedTransactionId`,
  original recebe `ReversedByTransactionId`; projetor ignora linhas superseded).
  Tipos: BUY/SELL/INCOME/CORP_ACTION/TRANSFER_IN|OUT; retroativo ilimitado;
  PM ponderado incluindo fees no BUY; corp actions split/grupamento/inpc/bonificacao/subscricao.
- `GET /lookup/{ticker}` — resolve ativo do catálogo p/ o wizard (id, ticker, nome, tipo, moeda).
- Calculadores puros: `AveragePriceCalculator`, `PositionProjector` (19 testes unitários);
  valuation local-first via último close de `asset_quotes` + `fx_rates` ("{CUR}-BRL");
  posição sem cotação usa custo (`hasMarketPrice=false`). Caixa sintético CDI/Selic valorizado
  ao custo até o accrual (M-P3).
- DDL manual: `docker/init-db/02-auth-and-fx.sql` (auth + seed RBAC + `fx_rates` — banco dev
  tinha sido recriado sem eles) e `03-portfolio.sql` (`portfolios`,
  `portfolio_transactions`, `portfolio_positions_summary`; colunas **PascalCase**, convenção EF).
- **M-P3 (RF/accrual):** `/fixed-income` attach/list/detach + `/timeline`; `PortfolioAccrualDailyJob`
  (23:10 UTC) e `PortfolioSnapshotDailyJob` (23:30 UTC) no Worker; `FixedIncomeAccrualCalculator`
  puro (CDI%/CDI+/Selic/IPCA+/Prefixado, base 252, dias úteis aprox. weekdays); caixa sintético
  e posições RF por ativo valorizados via accrual das séries SGS locais. Wizard com caixa
  sintético CDI/Selic + parâmetros RF + corp actions; timeline na carteira.
- **M-P4 (fiscal educacional):** `TaxCalculators` puros (regressiva 180/360/720, IOF, come-cotas,
  isenções PF — 34 testes); `/tax/redemption-simulation` e `/tax/darf/{year}/{month}` com premissas
  + disclaimer; aba Fiscal no frontend (simulador de resgate parcial/total + DARF mensal).
  ⛔ Exposição pública dos textos depende de revisão editorial (RISK-003).
- **M-P5 (compartilhar/metas/export):** `PATCH /visibility`, `/share-link/regenerate` (token SHA256
  retornado 1×), `/share-link` DELETE, público anônimo `/public/{slug}` respeitando percent_only +
  identidade ("Investidor X"), clone 1 clique normalizando pesos; goals CRUD + projeção
  (`portfolio_goals`, DDL 06); export CSV pt-BR em `/export/*.csv`. Pendente: página pública
  /c/[slug], UI metas/alocação-alvo, XLSX (⛔ pacote), social.
- Frontend: grupo `(dashboard)` com layout app shell dedicado (`DashboardShell` com sidebar retrátil, drawer mobile via Sheet, atalho global `Ctrl+.` para alternar "Esconder dados", busca `q` sincronizada entre header/sidebar/tabela, e `MaskedValue`/`BlurChart` exportados). Rotas: `/dashboard` (consolidado), `/dashboard/carteiras/nova`, `/dashboard/c/[id]`, `/dashboard/c/[id]/transacoes/nova`. Fetchers em `lib/api-client.ts`, store em `stores/privacy-store.ts`.

### `/api/v1/providers` (MarketData)
- `GET /health` — saúde por provider agregada de `sync_job_logs`.

### Infra
`GET /health` · Scalar em `/scalar/v1` (Development only).

## Jobs do Worker (`IndexDesk.Worker`)
| Job | Agenda (UTC) | Serviço |
| :--- | :--- | :--- |
| `BcbSyncJob` | diário 23:00 | `IMacroEconomicSyncService.SyncAllAsync` |
| `MarketDataDailySyncJob` | Mon–Fri 22:00 | `IDailyCloseSyncService.SyncDailyCloseAsync` — 1 batch Brapi + proventos espaçados ≥7 s + gap fill Yahoo→TV sidecar |
| `FxRatesDailySyncJob` | Mon–Fri 22:05 | `IFxRateSyncService.SyncLatestAsync` (AwesomeAPI USD/EUR/BTC-BRL → `fx_rates`) |
| `TradingViewDailySyncJob` | Mon–Fri 22:30 | `ITradingViewRefreshSyncService.RefreshAsync` (config ou séries defasadas) |
| `HoldingsWeeklySyncJob` | sáb 08:00 | `IEtfHoldingsSyncService.SyncWeeklyAsync` (iShares/SPDR/It Now/Investo → `etf_holdings`) |
| `PilotAssetBackfillJob` | sem agenda (manual/CLI) | `IAssetBackfillService.BackfillPilotAssetsAsync` |

CLI on-demand: `dotnet run --project src/IndexDesk.Worker -- --backfill TICKER[,TICKER2] [--provider yahoo|tv|infomoney|brapi] [--days N]`
(`SidecarProviderDirectory`; InfoMoney só por aqui; `--days N` = janela móvel hoje−N→hoje que
sobrescreve o switch de datas — usado p/ refresh 1y do catálogo inteiro em 2026-08-25: 7/7 SUCCESS,
IBOV/IFIX cobertos via TV até o dia, proventos sem novidade — Yahoo não publica dividends dos ETFs
B3 e Brapi cobra plano Startup pela rota). Aceita os pilotos e benchmarks
(IBOV start 2015, IFIX start hoje-7d). Backfill executado: IBOV 2890 cotações (2015→hoje,
YahooFinance); IFIX 1 ponto (forward-only); **IVVB11/yahoo 1405 quotes idempotentes**
(2021-01-04→2026-08-21, 2ª execução estável).
Serviços de ingestão persistem auditoria em `sync_job_logs`.

### Resiliência de providers (Fase 4 provider-sync)
- **`IApiKeyPool`/`InMemoryApiKeyPool`** (`Resilience/`, singleton): round-robin entre chaves
  saudáveis; cooldown `RateLimited` honrando `Retry-After` (quota diária → `UntilNextUtcDay`);
  `Invalid` (401/403) fora até reinício; token bucket por chave DENTRO do pool (`Acquire`
  consome token, refill proporcional; Brapi 10 req/min/chave). Chave única legada
  (`Brapi:ApiKey`, `AwesomeApi:Token`, `InfoMoney:SubscriptionKey`) liga como índice 0;
  provider sem chaves roda keyless (`HasKeys`). Config arrays:
  `Providers__Brapi__ApiKeys__*`, `Providers__AwesomeApi__Tokens__*`,
  `Providers__InfoMoney__SubscriptionKeys__*`; rpm em `Providers:{P}:RequestsPerMinutePerKey`.
- **`ProviderResilience`** + `BuildingBlocks.Resilience.ResiliencePipelines.CreateProviderCallPipeline<T>`:
  retry exp+jitter (respeita Retry-After) + circuit breaker (knobs `Providers:Resilience:*`;
  estado por NOME de provider, pipelines cacheados no singleton — clientes transient não perdem
  estado). Circuito aberto = `Provider.CircuitOpen`; pool esgotado = `Provider.PoolExhausted` —
  ambos **soft**: o estágio vira PARTIAL_WARNING (`DailyCloseChain.StatusFor`) e a cadeia faz
  failover Brapi → YahooSidecar → TVSidecar, sem retry cego.
- **Taxonomia de error codes** (documentada nos comentários dos clientes): `.RateLimit` =
  retry sim/breaker não · `Scrape.WafBlocked`/`*.AuthFailed` = breaker sim/retry não ·
  `Sidecar.Timeout/FetchFailed/.HttpError/.Exception` = ambos · `*.NoApiKey/.NoData/
  PoolExhausted/ParseError/Usage/SpawnFailed` = pass-through. Sidecar clients têm 1 retry só em
  Timeout/FetchFailed (`Providers:Sidecar:MaxRetries`).
- **Health:** `ProviderHealthDto.Keys` (`ProviderKeyHealthDto`) expõe contadores success/429/
  Invalid **por índice** de chave (nunca o valor); `GET /api/v1/providers/health` agrega.
- Planners puros testáveis: `Ingestion/DailyCloseChain.cs` e `Ingestion/DividendQueue.cs`
  (spacing ≥7 s após cada ticker, delay injetável).

## Frontend (`apps/web/src/app`) — páginas com implementação atual

| Rota | Descrição |
| :--- | :--- |
| `(dashboard)/dashboard` | home autenticada do usuário: patrimônio consolidado + cards por carteira + empty state (M-P1; gráficos/abas em M-P2) |
| `(dashboard)/dashboard/carteiras/nova` | criação de carteira (título/descrição/perfil etiqueta) |
| `(dashboard)/dashboard/c/[id]` | resumo da carteira: patrimônio/investido/não realizado + posições por custódia |
| `(dashboard)/dashboard/c/[id]/transacoes/nova` | wizard 3 etapas + revisão (BUY/SELL/INCOME com lookup de ticker, retroativo ilimitado) |
| `/` (home pública) | mini-dashboard de mercado: strip CDI/Selic/IPCA, movers 12m (altas/quedas via rankings), lista compacta de ferramentas — sem cards/KPI vazios |
| `(public)/ativos` + `/ativos/[ticker]` | catálogo denso e página de ativo redesenhada (fase C): header cotação + KPIs inline com tooltips + hero chart + simulação + fiscal por classe + cross-links |
| `(public)/etf/[ticker]`, `(public)/bdr/[ticker]`, `(public)/fii/[ticker]` | páginas por classe reexportam a página genérica; painel fiscal escolhido pela classe (`FiiTaxCard` p/ FII, `FiscalTaxCard` caso contrário) |
| `(public)/rankings` | ranking público por métrica/tipo com estado nuqs (`tipo`, `metrica`, `direcao`) |
| `(public)/comparador` | comparador multi-ativos |
| `(public)/ferramentas/backtest` | simulador de backtest público |
| `(public)/ferramentas/rendimento-real` | calculadora de rendimento real (Fisher) |
| `~offline`, `serwist/[path]` | fallback offline PWA |

Sem rotas admin: o mockup estático `(admin)/admin` foi removido em 2026-08-26 (botões sem handler,
sem dados). O link "Backoffice" saiu do menu de conta; painel real volta com MVP-015/016.

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
