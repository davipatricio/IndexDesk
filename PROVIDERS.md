# `PROVIDERS.md` — Guia de Provedores de Dados e Integrações

Este documento especifica todas as fontes de dados e APIs externas utilizadas pelo **IndexDesk**, incluindo custos, limites de taxa (_rate limits_), métodos de acesso e a estratégia de resiliência do sistema.

---

## 1. Resumo Consolidado dos Provedores

| Provedor                                                   | Categoria               |           Custo            |           Rate Limit            | Função Principal no IndexDesk                                       |
| :--------------------------------------------------------- | :---------------------- | :------------------------: | :-----------------------------: | :------------------------------------------------------------------ |
| **BCB (Banco Central - SGS)**                              | Macroeconomia           |        **Gratuito**        |          ~100 req/min           | Histórico de CDI, Selic, IPCA e IGP-M.                              |
| **CVM (Informe Diário)**                                   | Regulatório / PL        |        **Gratuito**        |  Sem limite rígido (HTTP/FTP)   | Cota, PL histórico (12M), Cotistas diários e Gestoras.              |
| **CVM (CDA - Carteira Mensal)**                            | Holdings Nacionais      |        **Gratuito**        | Sem limite rígido (HTTP mensal) | **Composição e ativos dos fundos/ETFs da B3** (Holdings & Overlap). |
| **B3 (Portal de Dados Abertos)**                           | Mercado Oficial         |        **Gratuito**        |  Download programático diário   | Carteiras teóricas de índices (IBOV, SMLL, IDIV) e códigos ISIN.    |
| **ANBIMA (Dados Abertos)**                                 | Feriados & Renda Fixa   |        **Gratuito**        |        Sem limite rígido        | Feriados bancários (regra 252 dias úteis) e índices IMA-B / IDA.    |
| **Feeds Diretos de Gestoras (iShares, SPDR, It Now, Investo)** | Holdings Globais/Locais |        **Gratuito**        |    Download de CSVs/XLSX/HTML públicos    | Holdings diários oficiais dos ETFs (Top 10, pesos, setores). It Now vai pelo sidecar `fetch` (WAF Akamai). |
| **Brapi.dev**                                              | B3 Market Data (slot 1)   |  **Freemium** (free apertado)  |       ~10 req/min/chave · anônimo = whitelist-only · dados +30 min       | Cotações diárias ajustadas e dividendos de ETFs e BDRs na B3. UMA chamada batch/dia útil + fila de proventos espaçada; nunca bulk history. |
| **Yahoo Finance (sidecar Python)**                         | OHLCV slot 2 + Benchmarks | **Gratuito (Não oficial)** |       ~2.000 req/IP/hora        | Volume/histórico/backfill (`*.SA`) e benchmarks globais (`^BVSP`, `USDBRL=X`, `GC=F`). Fetch via `curl_cffi` do yfinance. |
| **TradingView (sidecar tv-scraper)**                       | OHLCV slot 3              | **Gratuito c/ conta (cookie)** |       Por IP · chunking ≥4500 bars flaky       | Histórico adicional/tickers que Brapi e Yahoo não cobrem (`BMFBOVESPA:TICKER`). |
| **AwesomeAPI (economia.awesomeapi.com.br)**                | Câmbio & Cripto           | **Gratuito** (token premium opcional) |       Sem limite rígido documentado       | FX diário bid/ask `USD-BRL`, `EUR-BRL`, `BTC-BRL` na tabela `fx_rates` (bid = proxy de close). |
| **InfoMoney (XP Inc)**                                     | B3 secundária             | **Gratuito** (key APIM pública do frontend) |       APIM por chave       | Validação cruzada, leaderboard em lote e proventos com vocabulário B3 nativo. **FORA da cadeia** — só backfill explícito; série sem adjclose. |
| **Financial Modeling Prep (FMP) / EODHD**                  | Dados Globais (Backup)  | **Freemium / $19-$29/mês** |      250 a 10.000 req/dia       | Holdings e setores de ETFs UCITS (Irlanda) e ETFs dos EUA.          |
| **HG Brasil Finanças**                                     | ~~B3 Backup Data~~        |  **Pago (não contratado)** |       —                            | **REMOVIDO da cadeia ativa** — descoberto ser pago (não freemium). Client mantido no código, desativado; contingência paga só mediante decisão explícita de custo. |

---

## 2. Detalhamento Técnico por Provedor

### 2.1. Banco Central do Brasil (BCB - SGS)

- **Tipo:** API REST (JSON)
- **Autenticação:** Nenhuma (Pública)
- **Documentação:** [BCB SGS API](https://dadosabertos.bcb.gov.br/)

#### Principais Séries Cadastradas:

| Indicador        | Código da Série | Frequência | Endpoint BCB                                                           |
| :--------------- | :-------------: | :--------: | :--------------------------------------------------------------------- |
| **CDI Diário**   |      `12`       |   Diária   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.12/dados?formato=json`  |
| **Selic Diária** |      `11`       |   Diária   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.11/dados?formato=json`  |
| **IPCA Mensal**  |      `433`      |   Mensal   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.433/dados?formato=json` |
| **IGP-M Mensal** |      `189`      |   Mensal   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.189/dados?formato=json` |

#### Estratégia de Ingestão:

- **Cron Job:** Ingestão diária às **23:00 UTC** no `IndexDesk.Worker` via Quartz.NET.
- **Filtro de Data:** Query string `?dataInicial=dd/mm/aaaa&dataFinal=dd/mm/aaaa` para evitar carregar o histórico completo.

---

### 2.2. CVM — Portal de Dados Abertos (Informes + Composição CDA)

- **Tipo:** Arquivos CSV/Zip (HTTP estático)
- **Autenticação:** Nenhuma (Pública)
- **Frequência de Atualização:** Diária (D+1) e Mensal

#### Arquivos Utilizados:

1. **Informe Diário (`/INFORME_DIARIO/DADOS/inf_diario_fi_YYYYMM.zip`):**
   - **Campos:** `CNPJ_Fundo`, `DT_COMPTC`, `VL_QUOTA`, `VL_PATRIM_LIQ`, `NR_COTST`.
   - **Ingestão:** Worker diário às **04:00 AM** com streaming via `CsvHelper` + `NpgsqlBinaryImporter` (`COPY`).
2. **Composição e Diversificação das Aplicações (`/CDA/DADOS/cda_fi_YYYYMM.zip`):**
   - **Campos:** `CNPJ_Fundo`, `TP_APLIC` (Tipo de Ativo), `CD_ATIVO` (Ticker/Código), `QT_TIT` (Quantidade), `VL_MERC` (Valor de Mercado).
   - **Função:** Alimenta a tabela `etf_holdings` para a **Matriz de Overlap** e Top 10 Holdings dos ETFs nacionais.
3. **Cadastro Geral (`/CAD/DADOS/cad_fi.csv`):**
   - **Campos:** `CNPJ_Fundo`, `DENOM_SOCIAL`, `G_GESTOR`, `ADMIN`, `TAXA_ADM`.

---

### 2.3. Feeds Oficiais de Gestoras (Holdings Diários) — recon ao vivo 23/08/2026

Job semanal `HoldingsWeeklySyncJob` (sáb 08:00 UTC, grupo `MarketDataIngest`) → upsert idempotente
em `etf_holdings` (dedupe por `etf + as_of_date + holding_ticker`; linhas **sem ticker** — caso
Investo — dedupe por nome). Cada fonte que falha vira `PARTIAL_WARNING` isolado; o job segue com
as demais. Parsers puros testados contra fixtures locais.

**Globais (Top 10 AUM 2026):**

| Gestora | Exposição | Veredito / URL viva |
| :--- | :--- | :--- |
| **BlackRock iShares** | ✅ CSV diário | Pattern vivo `blackrock.com/br/products/{id}/{slug}` — o link do CSV ajax (`{hash}.ajax?fileType=csv`) **rotaciona** e vem como href relativo → extrair da página do produto (1 fetch HTML + regex `ISharesHoldingsParser.ExtractAjaxCsvUrl`). IDs semeados em `Providers:Ishares:Products`: BOVA11 = `251816`, IVVB11 = `251902`. Pattern antigo (`/br/produtos/[ID]/[ticker]/1495093766861.ajax`) **morto** (404). Ao vivo 23/08: BOVA11 = 83 holdings, top VALE3 11,00%. |
| **State Street SPDR** | ✅ XLSX diário | `ssga.com/library-content/products/fund-data/etfs/us/holdings-daily-us-en-{ticker}.xlsx` — URL previsível e **case-sensitive: ticker minúsculo** (`...us-en-spy.xlsx` 200; `SPY.xlsx` 404). Parse ClosedXML. |
| **Invesco** | ❌ | **Descartada** — site novo é SPA shell sem endpoints no HTML estático; download legacy retorna HTML; REST candidatos 404; DNS NXDOMAIN nos fallbacks. Requereria browser real p/ nada essencial. |
| Vanguard / Amundi | ⚠️ parcial | holdings em páginas de fundo sem URL estável — fallback, não fonte primária; CVM cobre os BDRs listados na B3. |
| Fidelity / UBS / GS / BNY / JPMorgan | ❌ | web-only ou irrelevante pro catálogo — parse HTML custa mais que vale. |

**Nacionais (Top 10 AUM fundos):**

| Gestora | ETFs B3? | Exposição própria |
| :--- | :--- | :--- |
| **It Now (Itaú Asset)** | ✅ BOVV11, SPXI11... | **JSON-first**: `POST www.itnow.com.br/history-api-json/?type=composicoes-indices&fundo={code}` (JSON ticker+pct); o código do fundo (ex.: BOVV11 = `BRBOVVCTF009`) é resolvido dinamicamente do próprio HTML da página `/bovv11/composicao/` (regex `fundo=([A-Z0-9]{6,})`, candidato mais frequente vence), com fallback semeado em `Providers:Holdings:ItNow:FundCodes:{TICKER}`. AngleSharp/HTML virou fallback. **Host bloqueia TLS nativo (Akamai 403)** → transporte obrigatório pelo sidecar `fetch` (`Providers:Holdings:ItNow:Transport=sidecar`, default). Apex `itnow.com.br` tem NXDOMAIN — usar `www.`. Ao vivo: BOVV11 = 78 holdings, as-of 21/08/2026, top VALE3 11,2377%. |
| **Investo (BTG)** | ✅ WRLD11, BDEF11, ALUG11 | HTML `investoetf.com/etf/{ticker}/` — tabela "Ativo \| Peso" com **nomes de empresas, sem coluna de ticker**, mais uma tabela "País" (exposição) que o parser classifica e ignora; holdings sem ticker dedupe por nome. |
| **XP Asset** | ✅ XINA11 etc. | Estruturada mas **WAF TLS**: `wp-json/composicao_carteira/v1/etf/{TICKER}` (holdings JSON) e `wp-json/quotas_table/v1/etf/{TICKER}` passam via `curl_cffi impersonate=chrome`. **Bônus futuro** — ainda sem cliente no Worker. |
| Bradesco/Santander/Caixa/BB/Vinci/etc | ❌ | lâminas/institucional/PDFs — metadata secundária; CVM CDA cobre mensalmente. |
| Investing.com | — | REPLICÁVEL-parcial (`api.investing.com/api/financialdata/{id}/historical/chart/`) mas **descartada da cadeia** por redundância OHLCV. Recon + HARs em `tools/providers/recon/`. |

Conclusão prática: fonte primária de composição = **CVM CDA mensal + iShares CSV (diário,
único arquivo estruturado nacional) + It Now JSON** (via sidecar); Investo complementa por HTML;
SPDR cobre SPY-likes p/ ETF BDR.

---

### 2.4. ANBIMA & B3 (Feriados e Índices)

1. **Feriados Bancários ANBIMA:**
   - Lista oficial de feriados nacionais até 2099 utilizada para preencher a tabela `market_holidays` e calcular o CDI acumulado em dias úteis (base 252).
2. **Índices Teva e ANBIMA:**
   - Séries históricas de índices de Renda Fixa (`IMA-B`, `IMA-B 5+`, `IRF-M`, `IDA`) e índices temáticos Teva.
3. **B3 Market Data Aberto (`dados.b3.com.br`):**
   - Composição das carteiras teóricas do Ibovespa, IFIX, SMLL e IDIV para benchmarks no comparador.

---

### 2.5. Brapi.dev (B3 Market Data & Proventos) — slot 1 da cadeia

- **Tipo:** API REST (JSON)
- **Autenticação:** `token=` na query (pool de chaves; anônimo funciona só numa whitelist mínima)
- **Documentação:** [Brapi Docs](https://brapi.dev/docs)

#### Realidade observada ao vivo (23/08/2026 · atualizado 26/08/2026)

- **Anônimo é whitelist-only:** `/quote/PETR4` responde 200 (`ratelimit-limit: 20`), mas os
  demais tickers devolvem **401 `MISSING_TOKEN`** (whitelist estável observada: PETR4/VALE3).
  A parede do plano free é **auth**, não rate limit — o 429 só aparece com token.
- **Batch anônimo ignora o filtro:** `/quote/list?tickers=<lista>` retorna 200 mas traz o
  mercado inteiro (~2000 ativos, ~471 KB) → filtrar client-side (`GetDailyBatchQuotesAsync`).
- **Plano free apertado:** ~10 req/min/chave. O pool de chaves (`Providers__Brapi__ApiKeys__0..N`)
  multiplica o throughput (N chaves ≈ N×10 req/min agregados) via round-robin + token bucket.
- **Histórico por range é paywall no free** (26/08): `/quote/{ticker}?range=…` aceita só
  `1d,5d,1mo,3mo`; pedidos maiores = **400 `INVALID_RANGE`** ("upgrade para Startup") — exceto
  tickers em cache/quente tipo PETR4, que respondem `1y` mesmo no free. `CalculateRange`
  devolve `1y` para janelas de 365 dias → é esse o BadRequest visto no backfill. Reforça a regra:
  **bulk history é papel do Yahoo sidecar**.
- **Proventos são paywall total no free** (26/08): `/quote/{ticker}?dividends=true` responde
  403 `FEATURE_NOT_AVAILABLE` (`canAccessDividendsData`, plano requerido: Startup R$119/mês).
  Esse 403 marca a chave como `Invalid` no pool (disable até reinício) — inofensivo no CLI
  one-shot, mas o daily close degradaria essa etapa todos os dias.
- **Catálogo completo com token:** `/available` (~1825 tickers) + `/quote/list` (agora traz
  `type`/`subType` por ativo: stock/bdr/fund × etf/fii/unit…) — usado em 26/08/2026 para curar
  1922 tickers de uma vez (`ON CONFLICT DO NOTHING`). Cobertura falha justamente nas listagens
  novas/iliquidas do universo ETF (241 tickers que só o Bora Investir tinha — ver §2.12).

#### Orçamento de consumo (jobs do Worker)

1. **Cotações do dia = UMA chamada batch** (`/quote/list`) por dia útil no
   `MarketDataDailySyncJob` (22:00 UTC MON–FRI). Nunca loop por ticker.
2. **Proventos = fila espaçada** ≥ 7 s entre tickers (`Providers__Brapi__DividendSpacingMs`,
   default 7000) — < 10 req/min mesmo com uma chave.
3. **Bulk history NUNCA via Brapi** — esse papel é do Yahoo sidecar. Backfill por aqui só
   pontual e espaçado.
4. **Idempotência vale cota:** upsert por `(ticker, date)`; reexecutar o mesmo dia atualiza,
   nunca duplica nem repete chamada concluída.
5. 429/quota → chave entra em cooldown no pool (honrando `Retry-After`; quota diária dorme até
   o próximo dia UTC). Pool inteiro esgotado = código soft `Provider.PoolExhausted` → a cadeia
   faz failover para Yahoo/TV; o job degrada para `PARTIAL_WARNING`, sem erro duro.

**Balanceamento entre providers (cadeia OHLCV declarativa):**

| Slot | Provedor | Papel |
| :--- | :--- | :--- |
| 1 | **Brapi** | Primário B3: lote diário pós-fechamento + proventos (vocabulário/taxa por ação) |
| 2 | **Yahoo sidecar** | Volume/histórico/backfill + benchmarks globais (`^BVSP`, `USDBRL=X`, `GC=F`); tickers `.SA` |
| 3 | **TradingView sidecar** | Histórico adicional/tickers que os outros não cobrem (`BMFBOVESPA:TICKER`) |

- **Brapi** = exclusivo para cotações/proventos de **B3** (ETFs, BDRs, FIIs). Não usar para
  benchmarks globais.
- **InfoMoney** fica **fora da cadeia** (secundária — ver §2.10): acessível só por backfill
  explícito (`--provider infomoney`).
- **HG Brasil** foi **removido da cadeia** (pago — ver §2.11); o `HgBrasilClient` permanece
  compilando, inativo, sem chave.
- Regra geral: **dado local-first primeiro** — provider só roda via Worker agendado; usuário
  nunca dispara chamada externa.

---

### 2.6. Sidecar Python (`tools/providers/sidecar`) — transporte dos clients OHLCV

Processo auxiliar que roda em conjunto com o `IndexDesk.Worker`: o .NET orquestra
(agendamento Quartz, validação, upsert idempotente) e delega o fetch HTTP ao Python, que herda
o anti-bloqueio das libs (**curl_cffi impersonate=chrome** do yfinance, sessão do tv-scraper).
Gerenciado por uv (`pyproject.toml`, deps pinadas: `yfinance==1.6.0`, `tv-scraper==1.5.1`,
`curl-cffi==0.16.1`). Config: `Providers__Sidecar__UvPath` (default `uv`),
`Providers__Sidecar__ProjectPath` (auto-resolvido), `Providers__Sidecar__TimeoutSeconds`
(default 120 s, kill da árvore de processos), `Providers__Sidecar__MaxRetries` (default 1,
retry só em `Sidecar.Timeout`/`Sidecar.FetchFailed`).

Comandos e contrato NDJSON v1:

```bash
sidecar yf quotes --symbol PETR4.SA --start YYYY-MM-DD --end YYYY-MM-DD   # {ticker,date,open,high,low,close,adj_close,volume}
sidecar yf dividends --symbol PETR4.SA                                    # {ticker,date,rate,type}
sidecar tv history --symbol BMFBOVESPA:BOVA11 --interval 1d --bars 5000 --cookie "$TV_COOKIE"
sidecar im quotes|dividends --symbol MGLU3                                # InfoMoney (env INFOMONEY_SUBSCRIPTION_KEY)
sidecar fetch --url URL [--method POST] [--data BODY] [--header "K: V"] [--b64]  # transporte genérico anti-WAF
```

- Dado = **NDJSON puro no stdout**; erros = JSON envelope no **stderr**
  (`{"error":{"code":"...","status":...}}`); exit codes `0` ok · `2` usage · `3` fetch/WAF ·
  `4` parse. Logs só no stderr.
- `--fixture arquivo` emite NDJSON pronto → testes C#/pytest sem rede.
- TV faz chunking interno (janelas cumulativas 1000→…→cap 5000, retry com backoff, dedupe por
  timestamp) porque payloads grandes são flaky (ver §2.8).

---

### 2.7. Yahoo Finance via Sidecar (OHLCV slot 2 + Benchmarks)

- **Tipo:** API não-oficial acessada pelo comando `sidecar yf` (lib `yfinance` + curl_cffi)
- **Autenticação:** Nenhuma — o anti-bloqueio vem do TLS fingerprint do `curl_cffi`
  (impersonate=chrome), que contorna cookie/crumb challenges sem spoof manual de headers.

#### Regras de símbolo

- Sufixo **`.SA` obrigatório** para B3: bare `BOVA11` = vazio/404; `PETR4.SA`, `BOVA11.SA` ok
  (o cliente C# mapeia e o sidecar normaliza bare → `.SA`).
- Benchmarks: `^BVSP` (Ibovespa), `^GSPC`, `^IXIC`, `USDBRL=X`, `GC=F`.
- IFIX: série **forward-only** (Yahoo não expõe histórico do índice).

#### Contrato e limitações

- `adjclose` ok e é o campo canônico de retorno (splits/inplits).
- **Dividendos vazios para ETF nacional** (observado: BOVA11 n=0 enquanto PETR4 n=61) —
  proventos de ETF BR vêm de **Brapi/CVM**, nunca do Yahoo.
- Rate limit ~2.000 req/IP/hora — limite **por IP**: pool de chaves não ajuda; controle =
  espaçamento fixo (200 ms entre tickers no fluxo diário) + breaker/retry.
- Papéis: gap-fill do fluxo diário (tickers sem barra após o batch Brapi), backfill histórico
  (`--backfill TICKER --provider yahoo`; provado: IVVB11 = 1405 quotes idempotentes),
  câmbio primário `USDBRL=X` (com AwesomeAPI como secundária, §2.9).

---

### 2.8. TradingView via Sidecar (OHLCV slot 3)

- **Tipo:** websocket/sessão privada via lib **tv-scraper 1.5.1** (`CandleStreamer`)
- **Autenticação:** **COOKIE de sessão autenticado** (`Providers__TradingView__Cookie`) — a lib
  **não tem** fluxo email/senha. Cookie vai apenas no argv do processo filho (nunca logado);
  falha de auth = código `TradingView.AuthFailed`.
- **Símbolos:** prefixo de exchange obrigatório — `BMFBOVESPA:{TICKER}`.
- **Chunking obrigatório:** requisições ≥4500 bars são flaky (3/3 `WebSocketTimeoutException`
  na Fase 0; BOVA11 passou até 4000). O CLI busca janelas cumulativas 1000→2000→…→cap 5000 com
  retry + dedupe por timestamp — smoke real de 2500 bars passou sem retry.
- **Papel na cadeia:** slot 3 — histórico adicional/tickers não cobertos por Brapi/Yahoo;
  job dedicado `TradingViewRefreshSyncJob` 22:30 UTC MON–FRI (config
  `Providers:Sync:TradingViewTickers`, senão detecção de séries defasadas no DB).
- **Dividendos:** vazio por design — TV não é fonte de proventos no nosso pipeline.
- Risco de protocolo privado: fork/pin da versão + breaker isola; duas fontes restantes na
  cadeia se a TV quebrar.

---

### 2.9. AwesomeAPI (Câmbio & Cripto — FX diário)

> Seção nova (herda o slot de contingência de câmbio que era do HG Brasil).

- **Tipo:** API REST pública `https://economia.awesomeapi.com.br/` — sem WAF (curl direto = 200)
- **Autenticação:** opcional — token premium `sk_...` como query param `token=` (chave vive
  **somente em `.env`**: `Providers__AwesomeApi__Token` / array `Tokens__0..N`; nunca em código,
  commit, log ou span OTel).
- **Contrato FX = bid/ask**, sem OHLCV de pregão nem volume; **bid = proxy de close**.
  Persistido na tabela `fx_rates` (PK par+data, upsert idempotente) pelo
  `FxRatesDailySyncJob` (22:05 UTC MON–FRI, serviço `IFxRateSyncService`).
- **Multi-pares numa chamada:** `GET /json/last/USD-BRL,EUR-BRL,BTC-BRL`
  (pares em `Providers:AwesomeApi:Pairs`); histórico diário:
  `GET /json/daily/{par}?start_date=YYYYMMDD&end_date=YYYYMMDD` (array com
  `high/low/varBid/pctChange/bid/ask/timestamp/create_date`).
- Cliente C# nativo (HttpClient, sem sidecar) com pool de tokens; 429 → cooldown com
  `Retry-After`; esgotado = `Provider.PoolExhausted` (soft).

---

### 2.10. InfoMoney (XP Inc) — secundária FORA da cadeia

- **Host base:** `https://api-infomoney.xpi.com.br/infomoney-services-marketdata/v1/api/v1/`
- **Auth:** header `ocp-apim-subscription-key` (Azure APIM) — chave **pública embutida no
  frontend** (sem segredo próprio; risco de revogação baixo/médio). No IndexDesk a chave vem de
  `Providers__InfoMoney__SubscriptionKeys__0` e injetada como env
  `INFOMONEY_SUBSCRIPTION_KEY` no processo sidecar.
- **WAF XP:** bloqueia TLS não-browser **mesmo com key** (403 Akamai "Acesso Bloqueado") →
  acesso obrigatoriamente via sidecar `im` (`curl_cffi impersonate=chrome`);
  403 = `Scrape.WafBlocked`, 401 = `InfoMoney.AuthFailed`.
- Endpoints usados: `b3/quotes/daily/{ticker}` (OHLCV paginado), `b3/corporate-events/cash-dividends/{ticker}`
  (proventos paginados), `b3/quotes/intraday/leaderboard?Property=Change&Order=Desc&PageSize=995`
  (~995 ativos B3 em UMA chamada — snapshot barato).
- **Sem adjusted close** — série é preço cru (`adj_close=close`): NÃO substitui Brapi/Yahoo
  para série histórica ajustada (backtests usam ajustado). Serve como fonte cruzada de
  validação e cobertura extra.
- **Vocabulário B3 nativo nos proventos** (`DIVIDENDO`, `JRSCAPPROPRIO`, esperados JSCP/
  RENDIMENTO/AMORTIZACAO) — separa JSCP (retenção 15%) de dividendo, importante p/ IR;
  `rate` = valor por ação em R$; `paymentDate` frequentemente `null` → fallback `ComDate`.
- **Nunca primária** (ToS não-oficial + chave revogável): fora da coleção `IMarketDataClient`;
  acessível só por `--backfill TICKER --provider infomoney`.

---

### 2.11. Provedores de Contingência / Backup (FMP & HG Brasil)

- **Financial Modeling Prep (FMP):** Usado como plano de contingência caso os feeds diretos de gestoras mudem de formato para ETFs internacionais (Holdings, country allocation e sector allocation via `/api/v3/etf-holder/{ticker}`).
- **HG Brasil Finanças:** **REMOVIDO da cadeia ativa** — descoberto ser pago (não freemium),
  não contingência grátis. O `HgBrasilClient` permanece no código, compilando e **desativado**
  (fora da coleção `IMarketDataClient`), sem `ApiKey` configurada. Reativação só mediante
  decisão explícita de custo; enquanto isso o papel de fallback recai sobre Yahoo (`.SA`) +
  AwesomeAPI (câmbio) + dado local.

---

### 2.12. Bora Investir (B3) — catálogo/metadata do universo ETF — recon 26/08/2026

- **Tipo:** WordPress REST (`?rest_route=/wp/v2/asset&per_page=100&page=N&_embed=wp:term`) +
  HTML SSR por ticker (`/cotacoes/etfs/{TICKER}/`). Cloudflare na frente, mas curl com
  User-Agent de browser responde 200 sem cookie/JS challenge. Sem API de candles/proventos.
- **Cobertura real: só universo ETF** — 519 tickers (218 ETFs nacionais x11 + 301 BDRs de
  ETF x39, dual-tagged `bdrs`+`etfs`). Ações/FIIs/BDR-ação = 404; taxonomias `fiis`/`acoes`
  existem com count 0.
- **Metadata útil via `_embed`:** índice de referência (grupo 2 dos termos), gestor (grupo 5),
  geografia/segmento; PL e nº de investidores só no HTML SSR (baixo valor, evitar scraping
  de 520 páginas).
- **Uso no projeto (26/08/2026):** curou os **241 tickers ausentes do Brapi** (listagens novas/
  iliquidas, ex.: Hashdex/AR x39) + reclassificou 135 `BDR`→`BDR_ETF` que o Brapi marca como
  bdr genérico. Nome do ativo = nome do índice quando disponível.
- **Não usar para:** cotações históricas, proventos, dados intraday — o site não expõe.
  Candles continuam Yahoo/TV sidecar; proventos de ETF seguem lacuna aberta (recon futuro:
  feeds de gestoras/CVM).

---

## 3. Resiliência, Rate Limit e Cache Strategy

```text
[Requisição de Backtest / Consulta do Usuário]
               │
               ▼
   ┌──────────────────────┐
   │  Aprovação em Redis? │ ─── (SIM) ───► Retorna em < 5ms (Cache Hit)
   └───────────┬──────────┘
               │ (NÃO)
               ▼
   ┌──────────────────────┐
   │  Existe no Postgres? │ ─── (SIM) ───► Salva no Redis e Retorna
   └───────────┬──────────┘
               │ (NÃO - Dado inexistente ou reprocessamento do Worker)
               ▼
   ┌──────────────────────┐
   │ Ingestão em Worker   │ ─── Utiliza Polly (Retry Exponencial + Circuit Breaker)
   └───────────┬──────────┘
               │
               ▼
   ┌──────────────────────┐
   │ Salva no DB + Redis  │ ─── Disponível instantaneamente para os usuários
   └──────────────────────┘
```

### Regras de Cache no Redis:

1. **Cotações Históricas Fechadas (Dias Anteriores):** Cache com TTL **infinito/30 dias**, pois dados passados não mudam.
2. **Dados do Dia Vigente:** Cache com TTL de **15 minutos** durante o pregão da B3 (10h às 18h).
3. **Indicadores BCB / ANBIMA (CDI/Selic/IPCA/Feriados):** Cache com TTL de **24 horas**.
4. **Holdings & Composição (CVM CDA / Gestoras):** Cache com TTL de **7 dias**.

### Resiliência por provider (pool de chaves + breaker — wired desde a Fase 4):

- **Pool de chaves** (`IApiKeyPool`/`InMemoryApiKeyPool`, singleton): round-robin entre chaves
  saudáveis; cooldown `RateLimited` (honra `Retry-After`; quota diária dorme até o próximo dia
  UTC); chave `Invalid` (401/403) sai até reinício do processo. Token bucket **dentro do pool**
  (Brapi default 10 req/min/chave) faz o pacing antes do Polly. Válido para limites **por
  chave** (Brapi, AwesomeAPI token, InfoMoney APIM); Yahoo/TV limitam **por IP** — lá o controle
  é espaçamento fixo + breaker, pool não ajuda.
- **Breaker por provider** (`ProviderResilience`, pipelines Polly cacheados por nome): 5 falhas/
  60 s → circuito abre ~30 s; chamadas short-circuitadas respondem códigos **soft**
  (`Provider.CircuitOpen` / `Provider.PoolExhausted`) e o estágio vira `PARTIAL_WARNING` no
  `sync_job_logs` — nunca derrubam o job.
- **Taxonomia de error codes:** `.RateLimit` = retry sim/breaker não; `Scrape.WafBlocked`/
  `*.AuthFailed` = breaker sim/retry não; `Sidecar.Timeout/FetchFailed/.HttpError/.Exception` =
  ambos; `*.NoApiKey/.NoData/PoolExhausted/ParseError/Usage/SpawnFailed` = pass-through.
- **Failover:** esgotou o pool/circuit aberto → próximo provider da cadeia
  (Brapi → Yahoo sidecar → TV sidecar), sem retry cego na mesma fonte.
- **Segredos:** chaves aparecem em log/métrica só como índice (`#{Index}`) — nunca o valor.

---

## 4. Variáveis de Ambiente (.env)

Configurações para o **`IndexDesk.Worker`** e **`IndexDesk.Api`** (espelho do `.env.example`;
valores abaixo = defaults públicos, segredos ficam vazios aqui):

```env
# Database & Cache
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=indexdesk;Username=indexdesk;Password=..."
ConnectionStrings__Redis="localhost:6379"
ConnectionStrings__RabbitMQ="amqp://indexdesk:...@localhost:5672"

# Cadeia OHLCV: Brapi (1) -> Yahoo sidecar (2) -> TradingView sidecar (3)
Providers__Brapi__ApiKeys__0=""
Providers__Brapi__BaseUrl="https://brapi.dev/api/"
Providers__Yahoo__BaseUrl="https://query1.finance.yahoo.com/"

# Sidecar Python (tools/providers/sidecar; vazio = defaults do código)
Providers__Sidecar__UvPath=""
Providers__Sidecar__ProjectPath=""
Providers__Sidecar__TimeoutSeconds="120"
Providers__Sidecar__MaxRetries="1"

# TradingView: auth por COOKIE de sessão autenticado (nunca commit/log)
Providers__TradingView__Cookie=""

# FX diário (AwesomeAPI) — token premium sk_... opcional
Providers__AwesomeApi__BaseUrl="https://economia.awesomeapi.com.br/"
Providers__AwesomeApi__Token=""

# InfoMoney secundária (fora da cadeia; backfill explícito)
Providers__InfoMoney__SubscriptionKeys__0=""

# Holdings — It Now via transporte sidecar (Akamai); mapa fundCode semeado no appsettings.json
Providers__Holdings__ItNow__Transport="sidecar"

# Demais Base URLs (BCB/CVM/ANBIMA/gestoras) têm defaults dev no appsettings.json do Worker
```
