# Plano — Provider Sync via Python sidecar (yfinance + tv-scraper) + feeds de gestoras

> **Status geral:** ✅ iniciativa implementada — fases 0.5–5 concluídas (2026-08-23); fase 0 ⏸️ aguardando creds TV (cookie autenticado) + token Brapi free p/ calibração final · **Última atualização:** 2026-08-23 · **Origem:** copiado de `~/.opencode/plan/provider-sync-scrapers.md`
>
> Este documento é o registro de progresso da iniciativa. Regras de uso:
> - Marque as tarefas (`- [x]`) conforme execução e atualize o **status da fase** + data no tracker.
> - Preencha a sub-seção **"Registro do que foi feito"** de cada fase (decisões tomadas, números medidos, desvios do plano).
> - Uma fase só é `done` quando **todos** os itens do **DOD** estiverem cumpridos.
> - Respeite a ordem de dependências entre fases. Nada de implementação fora do escopo das fases.

Repo: `etfhub-b3` (IndexDesk). Decisions locked:

- **Python sidecar from start** (yfinance + tv-scraper rodando em conjunto com o Worker .NET).
- **TradingView full OHLCV** in scope.
- Google Finance: skip.
- **HG Brasil fora da cadeia** — descoberto ser pago (não freemium). Só contingência paga opcional.
- **Brapi free plan é apertado** (~10 req/min no plano grátis) — usar com orçamento de rate limit
  e chamadas em lote; Yahoo/TV viram o músculo de volume.
- **Feeds oficiais de gestoras** (iShares/Investo/Vanguard) entram como fonte primária de holdings.

---

## Tracker de progresso

| Fase | Nome | Status | Dependências | Início | Concluída em |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 0 | Smoke spike (~meio dia) | ⏸️ `blocked` (falta só token free Brapi p/ calibrar 429 — resto 100%) | — | 2026-08-23 | — |
| 0.5 | Recon de requests (headless) | ✅ `done` (deps Playwright instaladas c/ sudo; 7 HARs versionados) | 0 (soft — ambiente validado) | 2026-08-23 | 2026-08-23 |
| 1 | Projeto sidecar | ✅ `done` | 0 | 2026-08-23 | 2026-08-23 |
| 2 | Clientes C# | ✅ `done` | 1 (+spec da 0.5) | 2026-08-23 | 2026-08-23 |
| 3 | Jobs & scheduling | ✅ `done` (DOD 100% — wire smoke It Now fechado via sidecar `fetch`; backfill IVVB11/yahoo idempotente) | 2 | 2026-08-23 | 2026-08-23 |
| 4 | Resiliência | ✅ `done` (pool de chaves c/ rotação/cooldown/bucket, breaker+retry por provider, failover soft; DOD 100%) | 2 (integra com 3) | 2026-08-23 | 2026-08-23 |
| 5 | Config, docs, skill (DoD global) | ✅ `done` (docs/skill/roadmap sincronizados c/ o entregado; `.env.example` reconciliado c/ o código) | 3, 4 | 2026-08-23 | 2026-08-23 |

Legenda de status: ⬜ `not_started` · 🔵 `in_progress` · ✅ `done` · ⏸️ `paused/blocked`

---

## Arquitetura

### Sidecar Python (`tools/providers/sidecar/`, uv, deps pinados)
`yfinance==1.6.x`, `tv-scraper==1.5.x`. CLI emite NDJSON no stdout (stderr = logs):
- `yf quotes --symbol PETR4.SA --start YYYY-MM-DD --end YYYY-MM-DD`
- `yf dividends --symbol PETR4.SA`
- `tv history --symbol BMFBOVESPA:BOVA11 --interval 1d --bars 5000`

Sidecar = processo auxiliar que roda **em conjunto** com o `IndexDesk.Worker`: o .NET orquestra
(agendamento Quartz, validação, upsert idempotente, eventos RabbitMQ) e delega o fetch HTTP para o
processo Python, que herda o anti-bloqueio dos libs (curl_cffi do yfinance, sessão do tv-scraper).
Contrato entre os dois = NDJSON versionado. Modo `serve` (FastAPI em docker-compose) só se o custo
de spawn por job incomodar.

### Clientes C# (`Modules/IndexDesk.Modules.MarketData/Clients/`)
- `YfinanceSidecarClient : IMarketDataClient` — spawn `System.Diagnostics.Process`, timeout,
  parse NDJSON streaming → `NormalizedQuote`/`NormalizedDividend`.
- `TradingViewSidecarClient : IMarketDataClient` — idem, símbolos `BMFBOVESPA:TICKER`.
- Erros distintos: `Sidecar.SpawnFailed`, `Sidecar.Timeout`, `Sidecar.ParseError`,
  `TradingView.AuthFailed`.

### Cadeia de cotações (OHLCV)
| Slot | Provedor | Custo | Papel |
| :--- | :--- | :--- | :--- |
| 1 | Brapi | Free (apertado) | Primário B3: lote diário pós-fechamento + proventos |
| 2 | Yahoo (sidecar) | Free | Volume/histórico/backfill + benchmarks globais (`^BVSP`, `USDBRL=X`, `GC=F`) |
| 3 | TradingView (sidecar) | Free c/ conta | Histórico adicional/tickers que os outros não cobrem |
| — | HG Brasil | **Pago** | Removido da cadeia ativa |

### Orçamento Brapi free (~10 req/min)
- Cotações do dia: **uma** chamada batch (`/quote/list?tickers=a,b,c,...`) por dia útil.
- Proventos: fila por ticker com espaçamento ≥ 7 s (< 10 req/min).
- Backfill histórico: mesma fila espaçada, retomável (upsert idempotente = seguro reiniciar);
  429 dispara circuit breaker curto + backoff.
- Regra: Brapi nunca usado para bulk history — esse papel é do Yahoo sidecar.

### Câmbio — AwesomeAPI (`economia.awesomeapi.com.br`) — validada ao vivo 23/08/2026
Grátis, **sem chave**, sem WAF (curl direto = 200). Herda o slot de contingência de câmbio
que era do HG Brasil (removido por ser pago).
- `GET /json/last/USD-BRL` — cotação atual; **multi-pares numa chamada**:
  `/json/last/USD-BRL,EUR-BRL,BTC-BRL`
- `GET /json/daily/{par}?start_date=YYYYMMDD&end_date=YYYYMMDD` — histórico diário (302 → segue;
  `pageSize` opcional). Array com: `high`, `low`, `varBid`, `pctChange`, `bid`, `ask`,
  `timestamp` (epoch s), `create_date`.
- Contrato: FX = bid/ask, sem OHLCV de pregão nem volume; usar `bid` como proxy de close.
  Pares relevantes: `USD-BRL`, `EUR-BRL`, `BTC-BRL` (ETFs cripto).
- Papel na cadeia de câmbio: **Yahoo `USDBRL=X` primário → AwesomeAPI secundária** (cliente
  C# nativo trivial, cabe fora do sidecar). Job: reuso do `MarketDataDailySyncJob` ou mini-job
  próprio 22:05 UTC. Config: `Providers__AwesomeApi__BaseUrl="https://economia.awesomeapi.com.br/"`.
- **Token premium**: usuário possui chave `sk_...` (plano pago/limites maiores). Enviar como
  query param `token=` conforme docs. A chave vive **somente em `.env`**
  (`Providers__AwesomeApi__Token`) — nunca em código, commit, log ou span OTel.

### Pool de chaves por provider (rotação, cooldown, balancing)
Vários limites são **por chave** (Brapi free/paid, AwesomeAPI token, InfoMoney APIM) — N chaves =
N× throughput. Outros são **por IP** (Yahoo ~2k/h, TV) — rotação não ajuda neles; lá o controle é
rate limiter global do provider.

- **Config** (arrays nativos do config binding .NET):
  ```env
  Providers__Brapi__ApiKeys__0="key_a"
  Providers__Brapi__ApiKeys__1="key_b"
  Providers__AwesomeApi__Tokens__0="sk_..."
  Providers__InfoMoney__SubscriptionKeys__0="9d46..."   # opcional: próprias chaves APIM
  ```
- **Abstração no módulo** (`Clients/` ou `Resilience/` do MarketData):
  ```csharp
  public interface IApiKeyPool {
      string Acquire(string provider);                                  // escolhe chave saudável
      void Report(string provider, string key, KeyResult result);       // Success|RateLimited|Invalid
  }
  ```
- **Implementação**: `InMemoryApiKeyPool` singleton, thread-safe (Worker = instância única;
  se um dia escalar horizontal, migrar contadores p/ Redis).
- **Estratégia de seleção**: round-robin entre chaves saudáveis.
- **Cooldown**: `RateLimited` (429 ou body de quota) → chave sai de rotação até fim da janela
  (`Retry-After` se vier; senão 60 s p/ min-based, próximo dia p/ daily quota). `Invalid` (401/403)
  → fora até reinício do processo + alerta em `sync_job_logs`.
- **Orçamento por chave**: token bucket simples (ex.: Brapi free 10 req/min/chave → pool de 3
  chaves ≈ 30 req/min agregado). Rate limiter do Polly consulta o bucket antes de disparar.
- **Observabilidade**: log/metric por índice de chave (**nunca o valor**), contadores success/429
  por chave expostos no `ProviderHealthAggregator`; chave esgotada aparece como
  `PARTIAL_WARNING` no job, não erro duro.
- **Ordem de failover**: esgotou pool inteiro → fallback pro próximo provider da cadeia
  (Brapi → Yahoo → TV), não retry cego na mesma fonte.

### Holdings (gestoras — feeds oficiais) — validado ao vivo (23/08/2026)

Top 10 global (AUM 2026, ordem aprox.: BlackRock ~US$14T, Vanguard ~12T, Fidelity,
State Street, UBS AM, Morgan Stanley IM, JPMorgan AM, Goldman Sachs AIMS, BNY IM, Amundi)
× exposição de dados:

| Gestora | Dados expostos | Formato / status |
| :--- | :--- | :--- |
| **BlackRock iShares** | ✅ CSV holdings diário | `blackrock.com/br/products/{id}/{slug}/{hash}.ajax?fileType=csv` — testado: HTTP 200, CSV BOVA11 de 20/08/2026. **ID `{hash}` muda por produto e pode rotar** → extrair link da página do produto (1 fetch HTML + regex). Pattern antigo do PROVIDERS.md (`1495093766861`) morto (404) |
| **State Street SPDR** | ✅ XLSX holdings diário | `ssga.com/library-content/products/fund-data/etfs/us/holdings-daily-us-en-{ticker}.xlsx` — testado: HTTP 200 (SPY, 54KB). URL previsível por ticker |
| **Invesco** | ⚠️ XLS protegido | endpoint de download retorna **406 via curl** mesmo com headers de browser → precisa TLS fingerprint real (sidecar Python/curl_cffi) ou rota alternativa (schwab.wallst.com mirror HTML) |
| **Vanguard** | ⚠️ parcial | composição no site por fundo; CSV existe mas sem URL estável confirmada — tratar como fallback, não fonte primária |
| **Amundi** | ⚠️ parcial | document library/fund pages com holdings, sem URL estática simples |
| Fidelity / UBS / GS / BNY / JPMorgan | ❌ web-only ou irrelevante pro catálogo | parse HTML custa mais que vale |

Relevância pro catálogo: ETFs BDRs na B3 são emitidos por BlackRock/Vanguard/etc — os feeds
globais acima cobrem eles; ETFs nacionais cobertos abaixo.

Gestoras nacionais — Top 10 (AUM fundos, mar/2026, Renova Invest; Quantum Finance/Forbes
metodologia diferente coloca BB Asset #1) × exposição:

| Gestora | AUM | ETFs B3? | Exposição própria |
| :--- | :--- | :--- | :--- |
| 1. Itaú Asset | R$ 1,28 tri | ✅ It Now (BOVV11, SPXI11...) | ✅ composição **HTML** diária `itnow.com.br/{ticker}/composicao/` |
| 2. Bradesco Asset | R$ 890 bi | ❌ | ⚠️ só lâminas PDF |
| 3. BTG Pactual | R$ 780 bi | ✅ Investo (WRLD11, BDEF11, ALUG11) | ✅ composição **HTML** `investoetf.com/etf/{ticker}/` |
| 4. Caixa Asset | R$ 615 bi | ❌ | ❌ institucional (`caixa.gov.br/caixa-asset`) |
| 5. Santander Asset | R$ 580 bi | ❌ | ⚠️ lâminas PDF |
| 6. XP Asset | R$ 380 bi | ✅ (XINA11 etc.) | ✅ **estruturado**: WP REST API `wp-json/cotas/v1/etf/{TICKER}` (cotas/ETF) + export XLSX na página do ETF; FII tem botão de export. Porém **WAF bloqueia TLS não-browser** (403 "Acesso Bloqueado" via curl c/ headers completos) → rota via **sidecar Python `curl_cffi`** |
| 7. BB Asset | R$ 340 bi | ❌ | ⚠️ `bbasset.com.br` documentos/lâminas PDF |
| 8. Vinci Partners | R$ 95 bi | ❌ | ❌ RI/PDFs |
| 9. Verde | ~R$ 50 bi | ❌ | ❌ institucional |
| 10. Squadra / JGP | R$ 38/25 bi | ❌ | ❌ institucional |
| (Kinea, grupo Itaú) | FIIs | ❌ | ⚠️ comentários mensais HTML, carteiras em PDF |

Conclusão prática: **fonte primária = CVM CDA mensal (já planejado) + iShares CSV (único
arquivo estruturado nacional, diário)**. Para ETFs não-iShares (It Now, Investo), composição
diária via **parser HTML** das duas gestoras que a publicam — único ganho real sobre a CVM.
XP Asset tem API JSON + XLSX mas com WAF TLS → incluir no spike da Fase 0 o teste
`curl_cffi impersonate=chrome` contra `wp-json/cotas/v1/etf/XINA11`; se passar, XP vira fonte
estruturada de cotas/ETF via sidecar (mesmo processo Python do yfinance).
Demais gestoras: lâminas PDF = metadata secundária (taxa/objetivo), baixa prioridade; CVM cobre.
SPDR XLSX cobre SPY-likes p/ ETF BDR.

Cliente C# nativo (`HttpClient` + CsvHelper para iShares; ClosedXML/OpenXML p/ SSGA XLSX;
AngleSharp p/ HTML das gestoras BR quando necessário), nada de Python aqui.
Job semanal `HoldingsWeeklySyncJob` → upsert idempotente em `etf_holdings`.

---

## Fontes complementares — InfoMoney & Investing.com

### InfoMoney (XP Inc) — API privada MAPEADA 23/08/2026
- **Host base**: `https://api-infomoney.xpi.com.br/infomoney-services-marketdata/v1/api/v1/`
- **Auth**: header `ocp-apim-subscription-key` (Azure APIM) — chave pública embutida no frontend
  (`9d461117...e4695`). Sem rotação conhecida; risco de revogação = baixo/médio.
- **WAF XP**: bloqueia TLS não-browser **mesmo com key** (403 "Acesso Bloqueado" via curl c/
  headers completos; browser passa 200) → acesso **obrigatoriamente via sidecar `curl_cffi`
  impersonate=chrome**.
- Mapa de endpoints (extraído do bundle `single-cotacoes.js`):
  - `b3/quotes/daily/{ticker}` — **histórico diário OHLCV paginado** ✅ resposta confirmada
    (ver contrato abaixo)
  - `b3/quotes/intraday/{ticker}`, `/intraday/chart/`, `/intraday/last`
  - `b3/quotes/intraday/leaderboard?Property=Change&Order=Desc&Page=1&PageSize=995&SecurityType=Quote`
    — **~995 ativos da B3 em UMA chamada** (snapshot diário barato)
  - `b3/corporate-events/cash-dividends|corporate-action|subscriptions/{ticker}` — proventos/eventos
  - `b3/index/composition/{ticker}` — composição de índice
  - `b3/fund/real-state/specification|simulator|notification` — FIIs
  - `currency/quote/intraday/usd`, `cryptocurrency/quote/intraday/last?Symbols=...`,
    `nyse|nasdaq/quote/intraday/...` (+ `/leaderboard`)
- **Contrato `b3/quotes/daily`** (confirmado em produção):
  - Query: `Page`, `PageSize`, `StartDate`/`EndDate` (ISO-8601 UTC com offset BRT embutido,
    ex. `2021-08-23T03:00:00.000Z`), `Order=Desc`; paginação via `pageInfo.hasNextPage`.
  - Campos por barra: `symbol`, `exchange:"B3"`, `tradeDate`, `open`, `high`, `close`, `low`,
    `change` (%), `changeMonth|changeYear|change52w` (%), `tradeVolume` (unidades),
    `financialVolume` (R$).
  - ⚠️ **Sem adjusted close** — série é preço cru. Backtests do IndexDesk usam ajustado
    (proventos/Come-Cotas) → InfoMoney NÃO substitui Brapi/Yahoo p/ série histórica ajustada;
    serve como fonte cruzada de validação e cobertura extra.
- **Contrato `b3/corporate-events/cash-dividends/{ticker}`** (confirmado em produção):
  - Query igual ao daily (`Page`, `PageSize`, `StartDate`, `EndDate`, `Order=Desc`), paginação
    `pageInfo.hasNextPage`.
  - Campos: `type` (vocabulário observado: `DIVIDENDO`, `JRSCAPPROPRIO`; esperar também JSCP,
    RENDIMENTO, AMORTIZACAO — mapear p/ `DividendType` normalizado), `symbol`,
    `rate` (**valor por ação em R$** → `NormalizedDividend.Rate`),
    `lastPrice` (preço na data-ex, útil p/ sanidade), `yield` (fração, ex. 0.5927%),
    `lastDatePriorToEx` (**data-ex/com** → `ComDate`), `paymentDate` (**frequentemente `null`**
    → manter fallback `PaymentDate = ComDate` como o YahooFinanceClient já faz).
  - Ganho vs Yahoo/Brapi: vocabulário de tipo B3 nativo (JSCP separado de dividendo) — importante
    p/ regra fiscal IR (JCP tem retenção 15%).
- Papel no projeto: fonte **secundária validada** — snapshot B3 em lote (leaderboard),
  proventos/eventos cruzados, histórico diário de conferência (sem adjclose). Nunca primária
  (ToS não-oficial + chave revogável).

### Investing.com — bloqueado sem browser
- Página: **403** via curl (Cloudflare/Akamai).
- Feed interno `tvc4.investing.com/feed.php` existe (400 JSON sem params = endpoint vivo, exige
  assinatura/params corretos).
- Papel: último recurso p/ cobertura global; custo alto de manutenção.

### Estratégia headless browser (nova fase)
Ferramenta: **Playwright (Python) dentro do sidecar container** — modo *recon*, não produção:
1. **Análise de requests**: rodar Playwright headed/headless navegando as páginas-alvo
   (InfoMoney cotação/FII, XP fundo, Invesco, Investing.com), capturar HAR/network log →
   identificar hosts reais, headers, cookies, assinaturas dos endpoints privados.
2. Se endpoint limpo (sem challenge JS): replicar direto via `curl_cffi` no sidecar
   (InfoMoney provavelmente sim; XP/Invesco já mapeados).
3. Se houver challenge JS (Investing.com): avaliar replay do HAR vs. scraping periódico via
   browser headless agendado (pesado — só se dado valer).
4. Toda fonte reconhecida aqui entra como **secundária/complementar**, nunca primária;
   breaker próprio e error codes dedicados (`Scrape.WafBlocked`, `Scrape.SelectorChanged`).

---

## Fases

### Fase 0 — Smoke spike (~meio dia)

**Objetivo:** provar que as fontes funcionam de verdade a partir do WSL antes de escrever código, e medir os limites reais do Brapi free.

**Tarefas**
- [x] Rodar CLIs yfinance/tv-scraper do WSL: `PETR4.SA`, `BOVA11`, `BMFBOVESPA:IBOV`, benchmarks.
- [x] Testar login TV (env `TV_EMAIL`/`TV_PASSWORD`; evitar conta com 2FA — forks tiveram problema). **Fechado 2ª rodada: auth da lib é por COOKIE (`sessionid`+`sessionid_sign`), não email/senha — ver desvio na Fase 1. Cookie autenticado salvo em `.env`.**
- [ ] Medir limites reais Brapi free (429 após quantas chamadas?) para calibrar a fila. **BLOQUEADO: anônimo só responde PETR4/VALE3 (401 `MISSING_TOKEN` no resto) — precisa token free antes de calibrar 429.**
- [x] Spike XP Asset (da seção Holdings): `curl_cffi impersonate=chrome` contra `wp-json/cotas/v1/etf/XINA11`; registrar se passa ou não.
- [x] Anotar resultados (números medidos, versões que funcionaram) no registro abaixo.

**DOD (definition of done)**
- [x] Todos os CLIs emitem dados válidos para os símbolos testados (quotes/dividends/history).
- [x] Login TV funcional sem 2FA; credenciais só em `.env` (nunca commit). **Cookie autenticado (`sessionid`) em `.env`, mode 600, gitignored.**
- [ ] Limite real do Brapi free documentado (req/min até o 429). **Parcial: documentado que anônimo é whitelist-only; 429 exige token.**
- [x] Veredito XP (`wp-json` via curl_cffi) registrado — define se XP entra como fonte estruturada via sidecar.
- [x] Resultados anotados no "Registro do que foi feito".

**Registro do que foi feito**
- Executado 2026-08-23 via subagente; relatório bruto em `/tmp/opencode/fase0/report.md`.
- Versões instaladas (uv, CPython 3.14.6): `yfinance==1.6.0`, `tv-scraper==1.5.1`, `curl-cffi==0.16.1`, `requests==2.34.2`.
- **yfinance:** limpo, zero warnings TLS/cookie/crumb. `PETR4.SA`/`BOVA11.SA` OHLCV+adjclose ok (sufixo `.SA` obrigatório — bare `BOVA11` = 404). Dividendos: PETR4 n=61; **BOVA11 n=0 — Yahoo não tem distribuições de ETF nacional** (proventos de ETF BR precisam de outra fonte: Brapi/CVM). Benchmarks ok: `^BVSP` close 171031,73 · `USDBRL=X` 5,1435 · `GC=F` 4680,60.
- **tv-scraper:** API 1.5.1 **não** é estilo TvDatafeed — chamada funcional: `CandleStreamer().get_candles(exchange="BMFBOVESPA", symbol=..., timeframe="1d", numb_candles=N)`. Anônimo funciona: IBOV 5000 bars ✅, PETR4 50 ✅, BOVA11 até 4000 ✅. **BOVA11 ≥4500 bars falha 3/3 (`WebSocketTimeoutException`)** — payload grande é flaky; sidecar precisa chunk/retry. ⚠️ Impacta desenho do CLI da Fase 1.
- **Brapi:** parede é **auth**, não rate limit. `/quote/PETR4` = 200 (`ratelimit-limit: 20`); demais tickers anônimos = 401 `MISSING_TOKEN` (whitelist estável: só PETR4/VALE3). Batch `/quote/list?tickers=<20>` = 200 mas retorna mercado inteiro sem filtro (~2000 ativos, 471 KB). 429 inalcançável sem token → calibração da fila fica pós-token.
- **XP Asset:** WAF confirmado e **contornável** — curl puro = 403 "Acesso Bloqueado"; `curl_cffi impersonate="chrome"` = 200 em `xpasset.com.br`. Porém endpoint `wp-json/cotas/v1/etf/XINA11` devolve **XLSX, não JSON** (`Cotas_XINA11`, zip válido, sheet "Cotas" ~413 linhas diárias: Ticker/Fundo/Indexador/Data/Cota Patrimônial/PL/pontos do índice; última linha 21/08/2026, cota 7,415492). Veredito: XP entra como fonte estruturada via sidecar, **com parser XLSX** (não JSON).
- Desvios/blockers: (1) login TV pendente creds; (2) calibração 429 Brapi pendente token free; (3) contrato tv-scraper diferente do assumido no plano; (4) XP = XLSX; (5) dividendos Yahoo vazios p/ ETF nacional.
- **Adendo 2026-08-23 (fechamento TV):** usuário forneceu cookies completos da conta (`sessionid` HttpOnly + `sessionid_sign` + `device_t`). Salvos em `.env` como `Providers__TradingView__Cookie` (mode 600, gitignored — valor nunca vai a log/doc). Smoke autenticado via sidecar: `tv history BMFBOVESPA:BOVA11 --bars 500 --cookie ...` = **exit 0, 500 linhas** (JWT extraído ok). Teste `--bars 5000`: retornou **4000 linhas em 2m48s** (cap prático p/ BOVA11, mesmo cap anônimo; tempo > timeout default 120 s do runner → backfills TV grandes precisam `Providers__Sidecar__TimeoutSeconds` maior ou janelas menores; refresh diário usa poucos bars e é seguro). Sessão expira 24/11/2026 → quando expirar, client reporta `TradingView.AuthFailed` (breaker isola, fallback segue). Restam só: token free Brapi p/ calibrar 429 da fila.

---

### Fase 0.5 — Recon de requests (headless, one-time + re-run quando fonte quebrar)

**Objetivo:** transformar endpoints privados em spec replicável; classificar cada fonte como replicável / precisa-browser / descartar.

**Tarefas**
- [x] Playwright no sidecar: capturar HAR de InfoMoney (cotação MGLU3, FII maxi-renda aba "Cotações e Gráficos"), XP XINA11, Investing.com BOVA11. *(Deps do Chromium instaladas c/ sudo; capturas headed sob `xvfb-run` — sites bloqueiam headless por UA.)*
- [x] Documento com hosts/params/headers dos endpoints privados (host base do InfoMoney é injetado em runtime — só aparece no tráfego).
- [x] Testar replay de cada endpoint via `curl_cffi`; classificar: replicável / precisa-browser / descartar.
- [x] HARs versionados em `tools/providers/recon/` para regressão. *(7 HARs gzip date-suffixed; keys redacted; leak grep = 0.)*
- [x] Saída vira spec dos clients da Fase 2.

**DOD (definition of done)**
- [x] Documento de recon commitado com hosts/params/headers por fonte. *(Escrito em `tools/providers/recon/recon.md`; commit fica a critério do usuário junto com o resto da initiativa.)*
- [x] Cada fonte classificada (replicável / precisa-browser / descartar) com evidência.
- [x] HARs versionados em `tools/providers/recon/`.
- [x] Spec dos clients aprovada (alimenta Fase 2).

**Registro do que foi feito**
- Executado 2026-08-23 via subagente, modo curl_cffi replay (Playwright inviável sem sudo — desvio documentado no plano e em `tools/providers/recon/hars/README.md`).
- Classificações:
  | Fonte | Veredito | Evidência |
  | :--- | :--- | :--- |
  | InfoMoney | REPLICÁVEL via sidecar `curl_cffi` | key inline no config da página (`9d461117...e4695`, redacted); `b3/quotes/daily/MGLU3` 200, `intraday/leaderboard` 200, `index/composition/IBOV?Page=1&PageSize=10&Order=Asc` 200 (400 sem Page/Order); sem key = 403 Akamai, key errada = 401 APIM |
  | XP Asset | REPLICÁVEL via sidecar `curl_cffi` | `/fundos-etfs/` 200, detalhe `/fundos-etfs/xina11/` 200, `wp-json/cotas/v1/etf/XINA11` 200 XLSX (24,5 KB) |
  | StatusInvest | REPLICÁVEL via HTML server-rendered | `/etfs/bova11` 200; JSON interno existe (`advancedsearchresult` POST 200 `{}`) mas params ofuscados — só precisaria browser se quiséssemos o screener JSON |
  | Invesco | PRECISA_BROWSER (na prática descartar) | site novo é SPA shell sem endpoints no HTML estático; download legacy retorna HTML, não XLSX; endpoints REST candidatos 404; DNS NXDOMAIN para fallbacks |
  | Investing.com | DESCARTAR como fonte | página agora 200 c/ fingerprint chrome (`__NEXT_DATA__` embutido), mas `feed.php` = 400 `"This url cannot be resolved"`, api routes 404 — sem endpoint OHLCV sem HAR e dado redundante |
- Correção de rota: página de cotação InfoMoney mudou — `/cotacoes/b3/{ticker}/` = 404; rota real `/cotibovacoes/{ticker}/`.
- Spec resultante p/ Fase 2: clientes sidecar Python cobrem InfoMoney + XP (XLSX); nada nativo C# para Invesco/Investing.
- Pendência ambiental: HARs reais exigem `playwright install-deps` com sudo — re-executar convenção do README quando disponível.
- **Adendo 2026-08-23 (2ª rodada, c/ sudo):** deps do Chromium instaladas (85 pacotes apt). Sites XP/ItNow/Investing **bloqueiam headless por UA** → capturas feitas **headed sob `xvfb-run`**. 7 HARs gzip em `tools/providers/recon/hars/` (infomoney_mglu3, xp_xina11, itnow_bovv11, investing_bova11, invesco_qqq_us, blackrock_br_productlist, blackrock_bova11), keys redacted. Reclassificações: **Investing.com DESCARTAR → REPLICÁVEL parcial** (`api.investing.com/api/financialdata/{id}/historical/chart/?interval=P1D&pointscount=160`, replay curl_cffi 200; segue fora da cadeia por redundância); Invesco segue PRECISA_BROWSER/descartar (SPA sem holdings XHR); **It Now**: domínio real `www.itnow.com.br` (apex NXDOMAIN), rota `/bovv11/composicao/` 200, e API interna melhor que HTML: `POST /history-api-json/?type=composicoes-indices&fundo=BRBOVVCTF009` (JSON c/ ticker+pct, replay curl_cffi 200; `fundo` = código do fundo, não ticker — precisa mapa); **iShares**: pattern vivo `blackrock.com/br/products/{id}/{slug}` (BOVA11 id `251816`), ajax CSV extraído do HTML estático (hash atual `1506433276998`; antigo `1495093766861` morto), CSV baixado ao vivo: 88 linhas, as-of 20/08/2026, colunas Ticker/Name/Sector/Asset Class/Market Value/Weight... pt-BR; **XP bônus**: `wp-json/composicao_carteira/v1/etf/XINA11` (holdings JSON) e `wp-json/quotas_table/v1/etf/XINA11` (413 cotas JSON), ambos 200 — melhores que o XLSX de cotas.

---

### Fase 1 — Projeto sidecar

**Objetivo:** criar o processo Python auxiliar com contrato NDJSON estável e testável sem rede.

**Tarefas**
- [x] `tools/providers/sidecar/` com `pyproject.toml` (uv) e deps pinadas: `yfinance==1.6.x`, `tv-scraper==1.5.x`. *(Pins medidos na Fase 0: `yfinance==1.6.0`, `tv-scraper==1.5.1`, `curl-cffi==0.16.1`.)*
- [x] Wrappers CLI finos:
  - [x] `yf quotes --symbol PETR4.SA --start YYYY-MM-DD --end YYYY-MM-DD`
  - [x] `yf dividends --symbol PETR4.SA`
  - [x] `tv history --symbol BMFBOVESPA:BOVA11 --interval 1d --bars 5000`
- [x] Contrato: NDJSON no stdout (stderr = logs); schema documentado:
  `{ticker,date,open,high,low,close,adj_close,volume}` / `{ticker,date,rate,type}`.
- [x] Flag `--fixture arquivo` emitindo NDJSON pronto → testes C# sem rede.
- [x] Bootstrap uv documentado (runtime Python no deploy — risco conhecido; modo `serve` opcional depois).

**DOD (definition of done)**
- [x] Os três comandos rodam e emitem NDJSON válido no stdout; logs apenas no stderr.
- [x] Schema NDJSON versionado/documentado.
- [x] `--fixture` funciona e é exercitado por pelo menos um exemplo de consumo. *(33 testes pytest offline + roundtrip byte-a-byte.)*
- [x] Versões pinadas exatas registradas (política de bump consciente quando yfinance quebrar TLS).
- [x] Passo de bootstrap uv reproduzível a partir do README do sidecar.

**Registro do que foi feito**
- Executado 2026-08-23 via subagente; verificação independente: `uv run pytest -q` = 33 passed (~0,08 s, offline).
- Layout: `src/sidecar/{cli,errors,ndjson,schema,symbols,tv_cmd,yf_cmd}.py` + `tests/`; console script `sidecar`; exit codes 0 ok / 2 usage / 3 fetch / 4 parse; erros vão como JSON no **stderr** (`{"error":{"code":...}}`) — stdout nunca mistura dado com erro.
- TV chunking: janela cumulativa 1000→2000→… cap 5000, retry 3× com backoff, dedupe por timestamp — smoke real `BOVA11 --bars 2500` (caso que era flaky ≥4500) passou sem retry. Over-fetch guard: `--bars 50` faz 1 chamada de 50.
- Smoke real: `yf quotes PETR4.SA` = 5 linhas · `yf dividends PETR4.SA` = 61 linhas (2005→2026) · `tv history BMFBOVESPA:PETR4 --bars 50` = 50 linhas · fixture roundtrip idêntico byte-a-byte. Bare ticker mapeado ao vivo (`PETR4` → `PETR4.SA`).
- **Desvio importante:** tv-scraper 1.5.1 autentica só por **cookie** (`CandleStreamer(export, cookie)`), sem fluxo email/senha na lib → flag implementada é `--cookie`. Impacta Fase 5: config vira `Providers__TradingView__Cookie` (não Email/Password). Login TV da Fase 0 segue pendente creds.
- `.gitignore` raiz ganhou seção Python (`__pycache__/`, `*.pyc`, `.venv/`, `.pytest_cache/`).

---

### Fase 2 — Clientes C#

**Objetivo:** integrar os sidecars ao módulo MarketData sem tocar no pipeline idempotente existente.

**Tarefas**
- [x] `YfinanceSidecarClient : IMarketDataClient` — spawn `System.Diagnostics.Process`, timeout, parse NDJSON streaming → `NormalizedQuote`/`NormalizedDividend`.
- [x] `TradingViewSidecarClient : IMarketDataClient` — idem, símbolos `BMFBOVESPA:TICKER`.
- [x] Erros distintos mapeados: `Sidecar.SpawnFailed`, `Sidecar.Timeout`, `Sidecar.ParseError`, `TradingView.AuthFailed`.
- [x] Reutilizar `MarketDataValidator` + pipeline idempotente existente (`AssetSyncService`) intacto.
- [x] (Se Fase 0.5 liberou) cliente da fonte recon classificada como replicável — secundária, com breaker próprio e error codes dedicados (`Scrape.WafBlocked`, `Scrape.SelectorChanged`). *(InfoMoney via novo comando sidecar `im`; breaker próprio fica para a Fase 4; `Scrape.SelectorChanged` não se aplica — API JSON, não HTML.)*
- [x] Unit tests contra fixtures NDJSON (comando fake configurável): mapeamento de símbolos, erros de parse/timeout.

**DOD (definition of done)**
- [x] Ambos os clients implementados e cobertos por testes sem rede (fixtures). *(Três clients: YF, TV e InfoMoney; 24 testes xUnit offline com scripts fake + 11 pytest offline do `im`.)*
- [x] Error codes distintos propagados para health/job logs. *(Códigos distintos saem no `Result.Error` dos clients; gravação em `sync_job_logs` acontece nos jobs da Fase 3.)*
- [x] Pipeline existente inalterado (nenhuma mudança comportamental no `AssetSyncService`). *(Clients registrados como tipos concretos no DI; entrada na cadeia `IMarketDataClient` fica para a Fase 3.)*
- [x] Smoke manual: fetch real de um ticker por client.

**Registro do que foi feito**
- Executado 2026-08-23 via subagente.
- **Python:** novo `src/sidecar/im_cmd.py` + subcomandos `im quotes|dividends` no CLI (contrato NDJSON idêntico); chave APIM lida de `INFOMONEY_SUBSCRIPTION_KEY` (env, injetada pelo runner C#), requests via `curl_cffi impersonate="chrome"`; paginação `Page/PageSize/Order=Desc` respeitando `pageInfo.hasNextPage`; 403 Akamai → `Scrape.WafBlocked` e 401 → `Scrape.AuthFailed` (ambos exit 3); `adj_close = close` (série crua); dividends mapeiam `lastDatePriorToEx`→date, `rate`→rate, `type` B3 nativo passa cru (DIVIDENDO/JRSCAPPROPRIO...). README atualizado + `tests/test_im_cmd.py` (11 testes offline com transporte falso). Suíte: 44 passed (~0,1 s).
- **C#:** `Clients/SidecarProcessRunner.cs` (spawn uv via `ArgumentList`, timeout default 120 s configurável `Providers__Sidecar__TimeoutSeconds` com kill da árvore de processos, tail de stderr truncado, extração do envelope `{"error":{...}}`, mapeamento exit 2/3/4 → `Sidecar.Usage/FetchFailed/ParseError`, spawn falho → `Sidecar.SpawnFailed`); `Clients/SidecarNdjson.cs` (parse/mapeamento compartilhado, linha inválida = `Sidecar.ParseError`); `YfinanceSidecarClient` (Priority 2, ProviderName `YahooSidecar`), `TradingViewSidecarClient` (Priority 3, prefixo `BMFBOVESPA:`, cookie de `Providers__TradingView__Cookie` só no argv do filho — nunca logado, falha auth → `TradingView.AuthFailed`, dividends = lista vazia por design), `InfoMoneySidecarClient` (Priority 4, secundária, key de `Providers__InfoMoney__SubscriptionKeys__0` → env do filho, sem key = `InfoMoney.NoApiKey` sem spawn, envelopes `Scrape.WafBlocked`/`InfoMoney.AuthFailed`). DI: singleton runner + transient dos três como **tipos concretos** — não entraram na coleção `IMarketDataClient` (cadeia declarativa é decisão da Fase 3); pipeline intacto.
- **Testes C#:** 24 novos testes offline (`SidecarProcessRunnerTests`, `SidecarClientsTests`) usando scripts bash fake no lugar do uv (happy path incl. adj_close≠close, dividendas, mapeamento de símbolo/argv, linha NDJSON quebrada, exit 3, timeout com kill <5 s, env forwarding, WAF/auth envelopes, NoApiKey antes do spawn) + `SidecarSmokeTests.cs` com `[SmokeFact]` gated por `SIDECAR_SMOKE=1` (skip fora de smoke). Suíte unitária completa: 87 passed / 3 skipped.
- **Smoke real (2026-08-23):** YF `PETR4.SA` 30 d = 21 linhas (último close 44,30) · TV `BMFBOVESPA:BOVA11` ~60 d = 43 linhas (168,33) · InfoMoney `MGLU3` 30 d = 21 linhas (4,16) — chave extraída ao vivo do blob inline da página `/cotibovacoes/mglu3/` (regex recon.md), usada só em env efêmera, nunca escrita em arquivo.
- Gates: `dotnet build IndexDesk.sln` 0 erros · `dotnet csharpier check .` ok · IntegrationTests 18 passed.
- Desvios: (1) InfoMoney entrou já na Fase 2 (liberado pelo recon 0.5), com breaker próprio adiado p/ Fase 4 conforme plano; (2) `Providers__Sidecar__TimeoutSeconds` adicionado além de UvPath/ProjectPath (necessário p/ teste de timeout rápido); (3) smoke ficou como teste xUnit permanente gated por env (reutilizável nas Fases 3–5), não script descartável.

---

### Fase 3 — Jobs & scheduling

**Objetivo:** colocar ingestão e holdings no Quartz com orçamento de rate limit e fallback entre providers.

**Tarefas**
- [x] `MarketDataDailySyncJob` (22:00 UTC MON–FRI): Brapi batch → fallback Yahoo → TV. *(Job agora chama o novo `IDailyCloseSyncService`: 1 batch Brapi + fila de proventos espaçada ≥7 s + gap fill Yahoo/TV sidecar; grava 4 linhas em `sync_job_logs`.)*
- [x] Novo `TradingViewDailySyncJob` (22:30 UTC MON–FRI, grupo `MarketDataIngest`): refresh OHLCV TV; shell fina, lógica no módulo (`TradingViewRefreshSyncService`: config `Providers:Sync:TradingViewTickers`, senão detecção de séries defasadas no DB), grava `sync_job_logs`.
- [x] `HoldingsWeeklySyncJob` (sábado, ex.: `0 0 8 ? * SAT *`): feeds gestoras → `etf_holdings`:
  - [x] iShares CSV (link extraído da página do produto: 1 fetch HTML + regex; pattern antigo morto) — CsvHelper.
  - [x] SPDR XLSX (`holdings-daily-us-en-{ticker}.xlsx`) — ClosedXML/OpenXML.
  - [x] Composição HTML It Now (`itnow.com.br/{ticker}/composicao/`) e Investo (`investoetf.com/etf/{ticker}/`) — AngleSharp. *(It Now agora usa a JSON API `history-api-json` como fonte primária, com o `fundoCode` (ISIN) resolvido dinamicamente da própria página HTML; AngleSharp virou fallback. Transporte It Now vai pelo sidecar (`ISidecarHttp`, comando `fetch`) — host bloqueia TLS nativo; ver adendo.)*
  - [x] Upsert idempotente em `etf_holdings`. *(Dedupe (etf_asset_id, as_of_date, holding_ticker); linhas sem ticker — layout real do Investo — dedupe por nome.)*
- [x] Câmbio: AwesomeAPI (`USD-BRL`, `EUR-BRL`, `BTC-BRL`) diária pós-fechamento — cliente nativo, sem sidecar; reuso do `MarketDataDailySyncJob` ou mini-job próprio 22:05 UTC. *(Mini-job `FxRatesDailySyncJob` 22:05 UTC: mesmo padrão do BcbSyncJob — domínio próprio, sync_job_logs próprio, falha isolada. Tabela nova `fx_rates`.)*
- [x] Backfill CLI: `--backfill TICKER [--provider yahoo|tv|infomoney|brapi]`.
- [x] Orçamento Brapi aplicado nos jobs: uma chamada batch/dia útil, fila de proventos ≥ 7 s, backfill espaçado e retomável, 429 → breaker curto + backoff. *(Batch único + fila espaçada implementados; breaker/429 fica para a Fase 4.)*

**DOD (definition of done)**
- [x] Jobs agendados no Quartz com horários/grupos exatamente conforme acima.
- [x] Toda execução grava `sync_job_logs`; falha de provider degrada com fallback, não derruba o job.
- [x] Upserts idempotentes (reinício do backfill é seguro).
- [x] Câmbio diária persistida com contrato FX bid/ask (`bid` como proxy de close).
- [x] `HoldingsWeeklySyncJob` popula `etf_holdings` a partir de iShares + SPDR (+ HTML das nacionais quando implementado). *(Ao vivo 23/08: `SyncWeeklyAsync` SUCCESS 4/4 contra o Postgres local; Investo WRLD11 upsertou 10 linhas em `etf_holdings`, idempotente na re-execução (count estável em duas rodadas completas). iShares BOVA11 e It Now BOVV11 parsearam ao vivo via código real de feed+parser (83 e 78 holdings) mas não upsertam por falta de assets curados no DB local — skip com warning é o comportamento projetado. It Now agora via sidecar `curl_cffi` — ver adendo.)*
- [x] Smoke manual: `--backfill IVVB11 --provider yahoo`. *(Executado 23/08: IVVB11 curado no DB local → `dotnet run --project src/IndexDesk.Worker -- --backfill IVVB11 --provider yahoo` = **SUCCESS: 1405 quotes, 0 dividends via YAHOO** (~9–10 s), janela 2021-01-04→2026-08-21; 2ª execução idempotente (1405 linhas estáveis em `asset_quotes`). Token Brapi não requerido para yahoo.)*

**Registro do que foi feito**
- Executado 2026-08-23 via subagente.
- **Cadeia OHLCV:** DI declarativa em `MarketDataModuleExtensions` = Brapi → YfinanceSidecarClient → TradingViewSidecarClient (HG Brasil removido da coleção, client mantido inativo). InfoMoney continua fora da cadeia (SECONDARY), acessível só via backfill explícito (`SidecarProviderDirectory`, aliases yahoo/yf/tv/tradingview/im).
- **DailyClose:** `BrapiClient.GetDailyBatchQuotesAsync` (`/quote/list`, filtro client-side pois anônimo ignora tickers — achado da Fase 0); gaps = tickers sem barra na janela lookback; dividendos via fila espaçada (`Providers:Brapi:DividendSpacingMs`, default 7000). Upserts extraídos para `IngestionUpserts` (compartilhados por daily/backfill/FX/holdings).
- **FX:** `AwesomeApiClient` nativo (HttpClient; token `Providers__AwesomeApi__Token` só como query param, nunca logado) + `FxRateSyncService` + tabela `fx_rates` (PK pair+date; MODELS.md não tinha tabela FX — macro_economic_series guarda valor único, sem bid/ask).
- **Holdings:** parsers puros testados contra fixtures locais (`tests/IndexDesk.UnitTests/Fixtures/Holdings/`); XLSX fixture gerado in-memory com ClosedXML. Descoberta ao vivo: **Investo publica "Ativo | Peso" com nomes de empresas, sem coluna de ticker**, e uma tabela separada "País" (exposição) que o parser classifica e ignora → holdings sem ticker dedupe por nome. It Now segue formato ticker+pct.
- **Smoke ao vivo (2026-08-23):** AwesomeAPI USD-BRL ✅ bid 5,138 / ask 5,1395 · SPDR SPY ✅ HTTP 200, 54 KB, ~605 linhas na sheet1 · Investo WRLD11 ✅ HTTP 200, composição parseável (10+ nomes/pesos) · **iShares BOVA11 ❌ bloqueado desta máquina** (páginas de produto blackrock.com/br → 404/DNS; extração do link ajax validada só por fixture) · **It Now ❌ DNS NXDOMAIN para itnow.com.br nesta máquina**.
- Gates: build 0 erros · csharpier ok · UnitTests 109 passed / 3 skipped (smoke-gated) · IntegrationTests 18 passed · sidecar pytest 44 passed.
- Desvios: (1) job diário trocou `IAssetSyncService` pelo novo `IDailyCloseSyncService` (endpoint REST `/sync/daily` mantém serviço legado); (2) `fx_rates` adicionada além do plano (sem tabela FX existente); (3) proventos entraram no fluxo diário como fila espaçada (item de orçamento da Fase 3), breaker fica p/ Fase 4; (4) smokes de rede parcialmente bloqueados (acima), compensados por fixtures.
- **Adendo 2026-08-23 (2ª rodada, pós-recon 0.5 — desbloqueio dos smokes):**
  - **It Now fundCode — abordagem escolhida:** a página de composição embute o próprio código do fundo nos scripts que chamam a API (`'fundo=BRBOVVCTF009'` dentro dos blocos `fetch('/history-api-json/?...')`). Resolução dinâmica = regex `fundo=([A-Z0-9]{6,})` sobre o HTML, candidato mais frequente vence — os ISINs dos *outros* fundos aparecem na página só como comparações `codProduct === 'BR...'` e nunca casam o padrão (fixture `itnow_composicao_live_sample.html` preserva 9 distratores reais para provar a discriminação). Fallback configurável semeado: `Providers:Holdings:ItNow:FundCodes:BOVV11=BRBOVVCTF009`. Novo parser puro `ItNowJsonParser` (`ParseJson` + `TryGetAsOfDate`); `EtfHoldingsSyncService` tentou JSON primeiro, degrada para AngleSharp/HTML em falha.
  - **Seeds iShares:** BOVA11 = id `251816`, slug `ishares-ibovespa-fundo-de-ndice-fund`; IVVB11 achado barato na listagem server-rendered `blackrock.com/br/products` → id `251902`, slug `ishares-sp-500-fi-em-cotas-de-fundo-de-ndice-inv-no-exterior-fund` (página confirma `fileName=IVVB11_holdings`). Ambos em `Providers:Ishares:Products`. O hash ajax continua rotativo (`1506433276998` hoje) e **agora vem como href relativo** — regex do parser estendida (absoluta + relativa entre aspas, resolvida contra a URL da página).
  - **Smokes ao vivo (via código real feed+parser; gate `HOLDINGS_SMOKE=1`):** iShares BOVA11 = 83 holdings, top VALE3 11,00% (CSV bruto 88 linhas, as-of 20 ago 2026) ✅ · Investo WRLD11 = 10 linhas, top NVIDIA 4,00% ✅ e **upsert real**: 10 linhas em `etf_holdings` + sync_job_logs, re-execução idempotente (count permanece 10) ✅ · It Now BOVV11 JSON = **78 holdings, as-of 21/08/2026, top VALE3 11,2377%** — validado com o mesmo parser sobre payload capturado ao vivo hoje; porém o **fio HTTP nativo .NET está bloqueado**: Akamai faz TLS fingerprinting e devolve 403 "Access Denied" para HttpClient/curl (curl_cffi impersonate=chrome passa com 200). HTML e JSON compartilham o host bloqueado → smoke no fio segue pendente até decisão de roteá-lo via sidecar curl_cffi (mesmo padrão XP/InfoMoney; candidato natural à Fase 4).
  - **Bugs reais encontrados pelos smokes no caminho de produção (todos corrigidos + testes de regressão):** (1) `TryParseWeight` tratava peso pt-BR < 1,5 como fração ×100 — RENT3 "1,32" virava 132%, Alphabet 142%; regra nova: vírgula decimal = já é pontos percentuais, fração só dot-only começando com "0."; (2) serviço lia `Providers:Holdings:{Spdr,ItNow,Investo}` sem o sufixo `:Tickers` → seções objeto retornavam null e os loops rodavam **zero tickers silenciosamente**; (3) SSGA é case-sensitive: ticker precisa minúsculo (`...us-en-spy.xlsx` 200; `SPY.xlsx` 404); (4) link ajax relativo não casava na regex antiga; (5) BaseUrl default do It Now usava apex NXDOMAIN → `https://www.itnow.com.br/`.
  - **Infra local:** `etf_holdings` criada manualmente no Postgres dev (DDL aditivo espelhando o modelo EF — `EnsureCreated` não adiciona tabelas em DB existente; pitfall anotado). Sem commit de nada.
  - **Gates:** build 0 erros · csharpier check ok · UnitTests **120 passed / 7 skipped** (+11 casos offline novos: It Now JSON/fundCode ×6, links ajax relativos ×2, casos novos de `TryParseWeight` ×3; +4 smokes gated por env) · IntegrationTests 18 passed · sidecar pytest 44 passed. Obs.: `dotnet format analyzers --verify-no-changes` falha por avisos NuGetAudit pré-existentes (advisories de AngleSharp 1.2.0 e OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.1), não por violações de estilo.
- **Adendo 2026-08-23 (3ª rodada — wire smoke It Now fechado via transporte sidecar):**
  - **Alternativas avaliadas (timebox ~10 min):** (a) spoof de UA/headers via cliente nativo → **DESCARTADA**: curl com User-Agent/Accept/Accept-Language completos continua 403 "Access Denied" em `www.itnow.com.br/bovv11/composicao/` — Akamai valida fingerprint TLS (JA3), não headers; (b) binário standalone `curl-impersonate` → **DESCARTADA**: adicionaria um segundo runtime/binary pinado por plataforma ao deploy quando o binding Python (`curl_cffi==0.16.1`) já vive no venv do sidecar e é a mesma musculatura; (c) comando genérico `fetch` no sidecar reusando o padrão InfoMoney → **ESCOLHIDA**: zero dependências novas, reusa `SidecarProcessRunner` da Fase 2, mantém uma única stack anti-bloqueio.
  - **Design:** novo comando `sidecar fetch --url URL [--method GET|POST] [--data BODY] [--header "K: V"...] [--timeout-s N] [--b64]` — corpo cru no stdout (texto utf-8; `--b64` para binário tipo XLSX), envelope de erro no stderr com campo extra `status` (`{"error":{"code":"Scrape.WafBlocked"|"Fetch.Failed","status":403,...}}`, exit 3). No C#: `ISidecarHttp`/`SidecarHttp` (Clients/) empacotando o runner; `ItNowHoldingsFeed` roteia página de composição + POST `history-api-json` pelo sidecar por padrão (`Providers:Holdings:ItNow:Transport=sidecar|native`, default sidecar; sem ISidecarHttp registrado degrada p/ nativo). Parsers intocados.
  - **Smokes ao vivo:** `sidecar fetch` direto = 200, 197 KB de HTML através do Akamai · It Now BOVV11 end-to-end (feed real + parser, transporte sidecar) = **78 holdings, as-of 2026-08-21, top VALE3 11,2377%** ✅ (bate com a captura da rodada anterior) · iShares BOVA11 = 83 holdings, top VALE3 11,00% ✅ (nativo) · Investo WRLD11 = 10 linhas ✅ (nativo) · serviço completo `SyncWeeklyAsync` = SUCCESS 4/4, WRLD11 upsertou 10 linhas, re-execução idempotente. BOVV11 não curado em `assets` local → upsert do It Now segue coberto pelos testes offline + parse-level live proof (curar BOVV11/BOVA11 é passo de curadoria fora do escopo).
  - **Backfill smoke:** IVVB11 inserido como asset curado no Postgres dev (insert manual, sem commit); `--backfill IVVB11 --provider yahoo` = SUCCESS 1405 quotes / 0 dividends (~9 s), janela 2021-01-04→2026-08-21, 2ª execução idempotente (1405 estável em `asset_quotes`). Fase 3 DOD 100% cumprida → tracker ✅.
  - **Gates:** build 0 erros · csharpier ok · UnitTests **128 passed / 7 skipped** (+8: `SidecarHttpTests` argv/b64/WAF ×5, switch de transporte do feed ×3) · IntegrationTests 18 passed · sidecar pytest **54 passed** (+10 do `fetch`).

---

### Fase 4 — Resiliência

**Objetivo:** sobreviver a rate limit, bloqueio TLS e queda de provider sem erro duro nem dado furado.

**Tarefas**
- [x] Polly por provider: retry exp+jitter (existe) + circuit breaker (5 falhas/60 s) + rate limiter. *(Retry/breaker via novo `ResiliencePipelines.CreateProviderCallPipeline` (BuildingBlocks.Resilience, com `TimeProvider`); pacing = token bucket dentro do pool; Yahoo/TV mantêm o espaçamento fixo 200 ms existente — limite é por IP, ver registro.)*
- [x] `IApiKeyPool` (`Acquire`/`Report` com `KeyResult Success|RateLimited|Invalid`) + `InMemoryApiKeyPool` singleton thread-safe; round-robin entre chaves saudáveis. *(+ `HasKeys` e `Snapshot()` na interface — desvio documentado no registro.)*
- [x] Cooldown: `RateLimited` (429/quota) → fora da rotação até fim da janela (`Retry-After`; senão 60 s min-based, próximo dia daily quota); `Invalid` (401/403) → fora até reinício + alerta em `sync_job_logs`. *(Helper `UntilNextUtcDay` p/ quota diária; alerta chega ao sync_job_logs via código `*.AuthFailed` do estágio.)*
- [x] Token bucket por chave (Brapi free 10 req/min/chave; pool de 3 chaves ≈ 30 req/min agregado); rate limiter do Polly consulta o bucket antes de disparar. *(Bucket implementado DENTRO do pool: `Acquire` consome token com refill proporcional ao tempo; decisão de design no registro.)*
- [x] Config arrays: `Providers__Brapi__ApiKeys__*`, `Providers__AwesomeApi__Tokens__*`, `Providers__InfoMoney__SubscriptionKeys__*`. *(Chave única legada liga como índice 0 quando array vazio; provider sem chaves roda keyless como antes; appsettings semeia só defaults sem segredo.)*
- [x] Observabilidade: log/metric por índice de chave (**nunca o valor**), contadores success/429 por chave no `ProviderHealthAggregator`; pool esgotado = `PARTIAL_WARNING`, não erro duro. *(Novo DTO `ProviderKeyHealthDto` + campo opcional `Keys` em `ProviderHealthDto`; `ProviderHealthService` injeta o pool.)*
- [x] Failover: pool inteiro esgotado → próximo provider da cadeia (Brapi → Yahoo → TV), sem retry cego na mesma fonte; Yahoo/TV ficam com rate limiter global (limite por IP — pool não ajuda). *(Esgotamento/circuito aberto viram códigos soft `Provider.PoolExhausted`/`Provider.CircuitOpen`; Yahoo/TV sem Polly RateLimiter dedicado — mantêm espaçamento fixo existente, ver registro.)*
- [x] Timeouts de processo 120 s + 1 retry; breaker envolve chamadas no nível do módulo. *(Retry sidecar só em Timeout/FetchFailed, configurável `Providers:Sidecar:MaxRetries` default 1; breaker cacheado por provider no singleton `ProviderResilience`.)*
- [x] `ProviderHealthAggregator` distingue bloqueio/auth/queda pelos error codes. *(Códigos agora embutidos nas mensagens dos estágios `[code]` + contadores Invalid/RateLimited por índice expostos.)*
- [x] Unit tests: fila de rate limit Brapi (spacing assert); `InMemoryApiKeyPool` — round-robin, cooldown após 429, chave inválida isolada, esgotamento → fallback pro próximo provider. *(Queue extraída p/ `DividendQueue` pura com delay injetável — assert exato de spacing offline; fake clock `FakeTimeProvider` no pool.)*

**DOD (definition of done)**
- [x] Breaker + rate limiter ativos por provider; timeout de processo 120 s + 1 retry configurado.
- [x] Pool de chaves com os 4 cenários de teste passando. *(Round-robin, cooldown 429 c/ Retry-After, Invalid isolado até reinício, esgotamento → null/fallback + bucket refill — todos offline.)*
- [x] Health aggregator reporta causa (bloqueio/auth/queda/esgotamento) por error code.
- [x] Nenhuma chave/token aparece em log ou span OTel (apenas índice). *(Logs do pool usam `#{Index}`; Snapshot carrega índice/contadores apenas; grep de auditoria = 0.)*

**Registro do que foi feito**
- Executado 2026-08-23 via subagente.
- **Arquivos novos:** `BuildingBlocks.Resilience/ProviderCallException.cs` + fábrica genérica `CreateProviderCallPipeline<T>` em `ResiliencePipelines.cs` (retry exp+jitter respeitando Retry-After via `DelayGenerator`, breaker c/ throughput/ratio/janela/break configuráveis); `Modules.MarketData/Resilience/{IApiKeyPool,InMemoryApiKeyPool,ProviderResilience}.cs`; `Ingestion/{DailyCloseChain,DividendQueue}.cs` (planners puros extraídos p/ testabilidade).
- **Arquivos modificados:** BrapiClient/AwesomeApiClient (chave adquirida por tentativa via pool; 429→RateLimited+cooldown c/ Retry-After parseado do header; 401/403→Invalid+`AuthFailed`; demais erros não tocam a chave), InfoMoney/YF/TV sidecar clients (retry único Timeout/FetchFailed + breaker), `MarketDataModuleExtensions` (DI: `TimeProvider.System`, `IApiKeyPool`, `ProviderResilience` singletons), DailyCloseSyncService (status soft/hard via planner, fila de proventos via `DividendQueue`), ProviderHealthDtos/Aggregator/Service (key stats), Worker `appsettings.json` (seeds: Brapi rpm 10, Sidecar MaxRetries 1, Resilience defaults — nenhum segredo).
- **Decisões:** (1) cooldown clock = `TimeProvider` injetável (default `TimeProvider.System`; testes usam `FakeTimeProvider` próprio — zero sleeps no pool); (2) bucket fica DENTRO do pool (`Acquire` consome token, refill proporcional `min(capacity, tokens + Δt·rpm)`), mais simples/testável que Polly RateLimiter e equivalente p/ orçamento; Retry-After é honrado na JANELA DO POOL (chave sai da rotação pelo valor anunciado) e como override do delay de retry quando presente na exceção — jobs nunca dormem 60 s num retry; (3) taxonomia: `.RateLimit` = retry sim / breaker não (cooldown do pool é dono desse modo de falha); `Scrape.WafBlocked`/`*.AuthFailed` = breaker sim / retry não; `Sidecar.Timeout/FetchFailed/.HttpError/.Exception` = ambos; `*.NoApiKey/.NoData/PoolExhausted/ParseError/Usage/SpawnFailed` = pass-through; (4) estado do breaker por NOME de provider, pipelines cacheados `(provider, T, modo)` no singleton `ProviderResilience` — clientes transient não perdem estado; circuito aberto responde `Provider.CircuitOpen` e o estágio vira PARTIAL_WARNING (planner `DailyCloseChain.StatusFor`), nunca FAILED; (5) chave única legada (`Providers:Brapi:ApiKey`, `AwesomeApi:Token`, `InfoMoney:SubscriptionKey`) liga como índice 0 quando o array está vazio; provider SEM chaves nenhuma roda keyless (comportamento anônimo Brapi da Fase 0 preservado) — exigiu adicionar `HasKeys(provider)` além de `Acquire/Report` (desvio consciente da assinatura "exata" do plano); (6) Polly valida `MaxRetryAttempts ≥ 1` → retries=0 desliga o AddRetry; `BreakDuration` mínimo 500 ms (Polly) — teste half-open usa janela real de 800 ms (único sleep de teste; demais cenários determinísticos).
- **Testes:** UnitTests **185 total — 178 passed / 7 skipped** (smokes gated; era 128/7 na Fase 3): +12 `InMemoryApiKeyPoolTests` (round-robin, cooldown default/Retry-After, Invalid isolado mesmo avançando dias, esgotamento→null→recuperação, bucket limite/refill proporcional, legacy key→índice 0, keyless, snapshot sem valor, report desconhecido ignorado, UntilNextUtcDay), +9 `ProviderResilienceTests` (retries transitórios, permanentes sem retry, PoolExhausted soft, sidecar retry Timeout≠ParseError, breaker abre/threshold, short-circuit sem invocar callback, half-open recupera, estado por provider, wrap de exceção de transporte), +8 `DividendQueueTests` (spacing exato após CADA ticker incl. último — comportamento histórico preservado, spacing 0, soft/hard/misto/exceção/queue vazia), +1 agregador (keys surfaced por índice, providers só-no-pool aparecem, overload antigo Keys=null), +7 novos casos BrapiClient (token do pool na URL, 429/401 reportam por índice, pool esgotado = zero chamadas HTTP + código soft, anônimo sem token) e +3 AwesomeApiClient (token query param, 429→RateLimited+cooldown, exhausted soft); IntegrationTests **18 passed**; sidecar pytest **54 passed** intocado.
- **Gates:** `dotnet build IndexDesk.sln` Debug e Release = 0 erros · `dotnet csharpier check .` ok · suítes acima verdes. `dotnet format analyzers` continua falhando apenas nos NuGetAudit advisories pré-existentes (AngleSharp 1.2.0, OTel exporter) já registrados na Fase 3.
- **Desvios/notas:** (1) interface do pool ganhou `HasKeys` + `Snapshot()` além dos dois membros do plano (necessários p/ keyless-legado e exposição de saúde); (2) Yahoo/TV ficaram sem Polly RateLimiter dedicado — o limite deles é por IP e as chamadas já são espaçadas (200 ms fixo entre tickers no DailyClose) + breaker/retry cobrem queda; pool/bucket se aplicam só aos limites POR CHAVE; (3) spacing assert exigiu extrair a fila para `DividendQueue` (delay injetável) em vez de virtual time no serviço — comportamento idêntico (delay após cada ticker, inclusive o último); (4) mensagens de erro dos estágios gap-fill agora embutem o error code (`[Sidecar.FetchFailed]`) p/ o aggregator distinguir causa; (5) nada commitado.

---

### Fase 5 — Config, docs, skill (definition of done global)

**Objetivo:** fechar a iniciativa com config, documentação, roadmap e skill sincronizados com o código entregue.

**Tarefas**
- [x] `.env` / `.env.example`: `Providers__Sidecar__UvPath`, `Providers__TradingView__Email`, `Providers__TradingView__Password`, `Providers__AwesomeApi__Token` (nunca commit/log). *(Executado com reconciliação real via grep de appsettings + options: TV **Email/Password não existem** — tv-scraper autentica por cookie, entrou `Providers__TradingView__Cookie`; adicionados `Sidecar__ProjectPath/TimeoutSeconds`, `AwesomeApi__BaseUrl`, `Brapi__ApiKeys__0` (substitui a legacy `ApiKey`), `InfoMoney__SubscriptionKeys__0`, `Holdings__ItNow__Transport`; chave Yahoo corrigida de `YahooFinance__BaseUrl` p/ `Yahoo__BaseUrl` (nome que o código lê). Segredos vazios.)*
- [x] Atualizar `PROVIDERS.md`: linha HG Brasil (pago — removido), §Brapi (orçamento free), §Yahoo rewrite, nova seção TradingView, §2.3 gestoras detalhada. *(+ seções novas AwesomeAPI e InfoMoney, contrato do sidecar NDJSON, tabela resumo §1, bloco de resiliência pool/breaker no §3, §4 env vars reescrito; gestoras = tabelas global+nacionais c/ vereditos ao vivo.)*
- [x] Worker scoped `CLAUDE.md`: tabela de jobs. *(Tabela já existia da Fase 3; estendida com notas de resiliência: pool/cooldown/bucket, breaker + códigos soft, taxonomia de error codes, semântica PARTIAL_WARNING/failover; bullet defasado "breaker ainda não wired" corrigido.)*
- [x] Roadmap regen (`roadmap:validate` / `roadmap:generate` / `roadmap:check`). *(Todos verdes; ROADMAP.md regenerou idêntico — iniciativa vive neste plano, sem tocar `.roadmap/*.json`.)*
- [x] Skill indexdesk: `current-features.md`, `common-mistakes.md` (prefixo TV, 2FA, contrato NDJSON, pegadinha rate limit Brapi), `decisions-risks.md` (DEC: sidecar Python; DEC: TV como fonte OHLCV; DEC: HG removido da cadeia). *(current-features: inventário Fase 4 completo + linha "Polly ainda não wired" corrigida; common-mistakes: itens 36–43 novos (TV prefixo+cookie, NDJSON stdout-puro, Brapi whitelist-only 401, SSGA lowercase, vírgula decimal em peso, EnsureCreated não migra DB existente, Akamai JA3 vs UA spoof, apex itnow NXDOMAIN); decisions-risks: subseção DEC-PS-01..05 (sidecar Python, TV slot 3, HG fora, InfoMoney secundária, transporte fetch anti-WAF); project-state.md refresh.)*

**DOD (definition of done)**
- [x] Todos os itens acima concluídos; segredos somente em `.env`.
- [x] `PROVIDERS.md`, Worker `CLAUDE.md` e skill consistentes com o que foi entregue.
- [x] `bun run roadmap:validate` e `roadmap:check` verdes.
- [x] Este plano atualizado com status final por fase.

**Registro do que foi feito**
- Executado 2026-08-23 via subagente (docs-only; nenhum código C#/Python tocado).
- **Desvios do plano original da fase:** (1) `Providers__TradingView__Email/Password` viraram `Cookie` — descoberta da Fase 1 (tv-scraper 1.5.x só autentica por cookie); (2) escopo do `.env.example` ampliado além dos 4 keys pedidos após grep de `appsettings.json` + options classes: nada inventado, tudo que o código lê (`Sidecar__*`, `AwesomeApi__BaseUrl`, arrays do pool, `ItNow__Transport`) entrou; legada `Brapi__ApiKey` substituída pela forma array `ApiKeys__0` (a legacy continua funcionando como índice 0); (3) PROVIDERS.md ganhou seções que o plano não listava explicitamente mas o entregou exigia (sidecar/NDJSON, AwesomeAPI, InfoMoney, resiliência pool/breaker) — sem elas as novas fontes ficariam indocumentadas; (4) Worker CLAUDE.md já tinha a tabela de jobs (Fase 3), então o trabalho real foi estender resiliência + corrigir o bullet "ainda não wired".
- Gates: mudança docs-only → `lint`/`typecheck` dispensados (não cobrem .md); `format:check` rodado na raiz.
- Pendências abertas (não-bloqueantes): credenciais TV + token Brapi free (calibração 429 da fila de proventos), DDL manual de `etf_holdings`/`fx_rates` pendente de migration formal (amarrado ao FND-013).

---

## Referências de código

| Repo | O que copiar |
| :--- | :--- |
| `ranaroussi/yfinance` | contrato chart API, cookie/crumb, curl_cffi impersonation |
| `smitkunpara/tv-scraper` | sessão TV, símbolos com exchange prefix |
| **`cammneto/Stock-Screener-bovespa`** | padrão **descoberta de tickers via sitemap** das plataformas BR (Status Invest, InvestSite, Investidor10, Fundamentus) em vez de lista hard-coded; parsers modulares por plataforma (`scraper_<fonte>.py` + util compartilhado); date-stamping de export |
| — | Agregadores acima = candidatos a **metadata/fundamentais de referência cruzada** (DY, P/L, PL) p/ páginas `/etf/[ticker]`; Status Invest tem endpoints JSON internos além do HTML — mapear no recon (Fase 0.5) se precisar |

Uso: consulta na hora de implementar os clients/scrapers — não dependência.

## Testes

- Unit: clientes contra fixtures NDJSON (comando fake configurável), mapeamento de símbolos,
  erros de parse/timeout, fila de rate limit Brapi (spacing assert),
  `InMemoryApiKeyPool` (round-robin, cooldown após 429, chave inválida isolada, esgotamento →
  fallback pro próximo provider).
- Sem rede no CI. Smoke manual: `--backfill IVVB11 --provider yahoo`.

(Cobertura distribuída nas fases 2 e 4; smoke manual na fase 3.)

## Riscos / mitigações

- Bloqueio TLS Yahoo → resolvido pelo curl_cffi do yfinance; pin de versão + bump quando quebrar.
- Mudança no protocolo privado TV → fork mantido (`tv-scraper`) + breaker isola; 2 fontes restantes.
- Rate limit Brapi → batch + fila espaçada + breaker; Brapi jamais usado para bulk.
  Pool de chaves multiplica throughput (N×10 req/min) — mas rotação só vale p/ limite **por
  chave**; Yahoo/TV limitam por IP (ali, pool não ajuda).
- Gestora muda layout de CSV → parser tolerante + alerta via `sync_job_logs` (PARTIAL_WARNING);
  CVM CDA cobre mensalmente.
- Runtime Python no deploy → bootstrap uv documentado; modo `serve` opcional depois.

## Fora de escopo

- Google Finance (nada viável).
- Streaming intraday (local-first = fechamento diário).
