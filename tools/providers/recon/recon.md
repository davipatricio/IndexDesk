# Fase 0.5 — Recon de requests (headless) — Spec de fontes

> Data: 2026-08-23 · Executado via subagente · Ambiente: WSL2, `curl_cffi==0.16.1` (`impersonate="chrome"`).
> Atualização tarde de 2026-08-23: sudo liberado → deps do Chromium instaladas e **HARs capturados**
> (ver subseções "HAR (2026-08-23)" por fonte e [`hars/`](hars/README.md)). Detalhe operacional:
> XP Asset, It Now e Investing.com **bloqueiam Chromium headless** (UA `HeadlessChrome` → 403);
> nesses casos a captura foi **headed sob xvfb**, enquanto o replay de validação segue curl_cffi.
>
> Objetivo: transformar endpoints privados em spec replicável e classificar cada fonte
> como **REPLICAVEL** (curl_cffi, sem browser), **PRECISA_BROWSER** (precisa JS real/HAR)
> ou **DESCARTAR**. Alimenta os clients da Fase 2.

## Resumo das classificações

| Fonte | Classificação | Cliente recomendado | Evidência-chave |
| :--- | :--- | :--- | :--- |
| InfoMoney (XP Inc) | ✅ **REPLICAVEL** | Sidecar Python (`curl_cffi`) | 3/3 endpoints → 200 com chave + TLS chrome; sem chave → 403 Akamai; chave errada → 401 APIM |
| XP Asset | ✅ **REPLICAVEL** (JSON, upgrade pós-HAR) | Sidecar Python (`curl_cffi`) | novo: `wp-json/composicao_carteira/v1/etf/{T}` (holdings JSON) e `wp-json/quotas_table/v1/etf/{T}` (413 cotas JSON) — 200 via replay; XLSX `cotas/v1` segue 200 como fallback |
| StatusInvest | ✅ **REPLICAVEL** (via HTML) | C# nativo (AngleSharp) ou sidecar | `/etfs/bova11` server-rendered 200 via curl_cffi; API JSON interna viva mas params não documentados *(sem HAR nesta rodada)* |
| Investing.com | ⚠️ **REPLICAVEL parcial** (upgrade pós-HAR; segue fora da cadeia por redundância) | Sidecar Python (`curl_cffi`) se um dia precisar | HAR revelou `api.investing.com/api/financialdata/{pairId}/historical/chart/?interval=P1D&pointscount=N` → 200 JSON OHLCV; replay curl_cffi chrome = 200 |
| Invesco | ❌ **PRECISA_BROWSER p/ holdings** (mantém descarte) | — skip até ter endpoint real | HAR: `product-detail?ticker=QQQ` redireciona p/ site de marketing SPA (`qqq-etf/en/home.html`); só `.model.json` de conteúdo + API de performance (`dng-api.invesco.com`, replicável mas irrelevante); zero XHR de holdings/download |
| It Now (Itaú Asset) | ✅ **REPLICAVEL** (descoberta pós-HAR — fecha pendência da Fase 3) | C# nativo (HttpClient JSON) ou sidecar | domínio real `www.itnow.com.br`; API estruturada `POST /history-api-json/?type=composicoes-indices&fundo=BRBOVVCTF009` → 200 JSON; replay curl_cffi = 200 |
| BlackRock iShares BR | ✅ **REPLICAVEL** (fecha pendência da Fase 3) | C# nativo (1 fetch HTML + regex → CSV) | página de produto correta `blackrock.com/br/products/251816/...`; ajax CSV extraído do HTML e baixado: 200, 88 linhas, as-of 20/08/2026 |

---

## Recon 26/08/2026 — sites de catálogo/fundamentalistas (5 fontes, subagentes paralelos)

Relatórios completos: [`recon-investidor10.md`](recon-investidor10.md) · [`recon-maisretorno.md`](recon-maisretorno.md) · [`recon-fundsexplorer.md`](recon-fundsexplorer.md) · [`recon-clubefii.md`](recon-clubefii.md) · [`recon-advfn.md`](recon-advfn.md). Spec destilada: `PROVIDERS.md` §2.14–§2.18.

| Fonte | Classificação | Cliente recomendado | Evidência-chave |
| :--- | :--- | :--- | :--- |
| Investidor10 | ✅ **REPLICAVEL** — ALTO p/ enriquecimento, BAIXO-MÉDIO como preço primário | Sidecar ou C# nativo (APIs GET sem auth) | `/api/cotacoes/batch?tickers=…` 200; históricos close ajustável até 15y; 31 indicadores ×10y; **sem OHLCV/volume**; IDs internos numéricos precisam resolução ticker→id |
| MaisRetorno | ✅ **REPLICAVEL** — ALTO p/ séries de rentabilidade + cadastro + gestoras | Sidecar/C# (`__NEXT_DATA__` ou `_next/data/{buildId}`) | PETR4 mensal desde 1994; lista-acoes 528 tickers c/ CNPJ+code_cvm+ISIN; gestores 3.236 c/ CNPJ; buildId muda por deploy; zero proventos/DY/PVP |
| FundsExplorer | ✅ **REPLICAVEL** — ALTO p/ FIIs | Sidecar/C# (`POST admin-ajax.php` + nonce do HTML) | `funds-get-income` rendimentos desde 2016-06; `funds-get-quotations` diária ~5y; patrimonial mensal desde 2016-01; lista ~696 FIIs; API legada `/api/v1` morta (500) |
| ClubeFII | ✅ **REPLICAVEL** — MÉDIO-ALTO (fundamentais), BAIXO (rendimentos gated) | Sidecar/C# (XHR HTML fragment) | `/fundo_basico?cod=TICKER` público c/ CNPJ/DY/PVP/cotistas/taxa adm; cotações mensais 2011–2026; lista 825 FIIs; rendimentos detalhados atrás de login |
| ADVFN Brasil | ⚠️ **REPLICAVEL parcial** — ALTO só p/ proventos cross-check | C# nativo (HTML server-rendered) | `/balanco/dividendos` histórico completo grátis (PETR4 JCP+div, KNCR11 120 linhas); OHLCV via WebSocket/ag-grid — nada por HTTP simples |

---

## 1. InfoMoney (XP Inc) — REPLICAVEL

### Página que dispara as chamadas
- `https://www.infomoney.com.br/cotibovacoes/mglu3/` → **200** via curl_cffi chrome.
- ⚠️ URL `/cotacoes/b3/mglu3/` (usada na Fase 0 como referência) → **404** hoje. A rota real de cotação é `/cotibovacoes/{ticker}/`.

### Onde a chave está (descoberta em runtime)
A chave **não está mais num bundle separado** — vem inline no HTML da página, num blob de config:

```json
"...services-marketdata/v1/api/v1/","ocp_apim_subscription_key":"9d461117...e4695"
```

- Chave marketdata: prefixo `9d461117`, sufixo `e4695` (**redacted** — valor completo só em `.env`: `Providers__InfoMoney__SubscriptionKeys__0`).
- Extração programática (fallback se a chave rotacionar): `GET /cotibovacoes/{ticker}` + regex
  `"ocp_apim_subscription_key":"([0-9a-f]{32})"` **dentro do bloco cuja URL base contenha `services-marketdata`**
  (a página tem ~10 chaves APIM de serviços diferentes; pegar a certa pelo contexto).

### API

| Campo | Valor |
| :--- | :--- |
| Host base | `https://api-infomoney.xpi.com.br/infomoney-services-marketdata/v1/api/v1/` |
| Auth | header `ocp-apim-subscription-key` (Azure APIM) |
| WAF | Akamai — bloqueia TLS não-browser **antes** de validar a chave |

Headers recomendados no replay: `ocp-apim-subscription-key`, `Origin: https://www.infomoney.com.br`,
`Referer: https://www.infomoney.com.br/`, `Accept: application/json`.

### Endpoints testados ao vivo (2026-08-23, curl_cffi impersonate=chrome)

| Endpoint | Resultado | Observações |
| :--- | :--- | :--- |
| `b3/quotes/daily/MGLU3?Page=1&PageSize=5&Order=Desc` | **200** `application/json` | `{result:[...], pageInfo:{hasNextPage}}`; barra: `symbol, exchange:"B3", tradeDate, open, high, low, close, change(%), changeMonth, changeYear, change52w, tradeVolume(unidades), financialVolume(R$)` |
| `b3/quotes/intraday/leaderboard?Property=Change&Order=Desc&Page=1&PageSize=10&SecurityType=Quote` | **200** | mesmo shape; campos extras `price`, `msgSeqNum`. `PageSize` grande (~995) = snapshot B3 inteiro numa chamada |
| `b3/index/composition/IBOV?Page=1&PageSize=10&Order=Asc` | **200** | campos: `symbol, name, indexPct, companyName, economicSector, indexSymbol, securityType, lastUpdate`; paginado |
| idem **sem** `Page`/`Order` | **400** | `"O campo Page deve ser informada com valor maior que 0"`, `"O campo Order não é válido. Valores válidos: Asc,Desc"` — composição **exige** paginação explícita |
| qualquer endpoint com chave inválida | **401** | `"Access denied due to invalid subscription key..."` |
| qualquer endpoint **sem** chave | **403** | Akamai `Access Denied` (HTML) — o bloqueio é TLS, não auth |

### Contrato confirmado (`daily`)
- Query: `Page`, `PageSize`, `Order=Desc|Asc`, opcionais `StartDate`/`EndDate`
  (ISO-8601 UTC c/ offset BRT embutido, ex. `2021-08-23T03:00:00.000Z`). Paginação por `pageInfo.hasNextPage`.
- Sem adjusted close (série crua) — não substitui Brapi/Yahoo p/ backtest; papel = validação cruzada + leaderboard.

### Cliente recomendado
Sidecar Python `curl_cffi` (mesmo processo do yfinance). Comando sugerido:
`im quotes --symbol MGLU3 --start ... --end ...` e `im leaderboard`. Chave lida de env,
nunca logada. Breaker próprio + error codes `Scrape.WafBlocked` (403 Akamai) / `Scrape.AuthFailed` (401 APIM).
Mapa completo dos demais endpoints (corporate-events, FIIs, currency, crypto) já mapeado no plano — mesma base/auth.

### HAR (2026-08-23) — `hars/infomoney_mglu3_2026-08-23.har.gz`
Página `/cotibovacoes/mglu3/` (headless ok) confirma o mapa: 4 chamadas a
`api-infomoney.xpi.com.br/infomoney-services-marketdata/v1/api/v1/` com header
`Ocp-Apim-Subscription-Key` (redigido no HAR):
`b3/quotes/intraday/last?symbols=IBOV,IFIX,MGLU3,PETR4,VALE3,ITUB4,ABEV3,GGBR4...`,
`currency/quote/intraday/usd`, `cryptocurrency/quote/intraday/last?Symbols=bitcoin`
(status `-1` no HAR = captura encerrada antes da resposta; endpoint vivo). Nada novo além do
mapa conhecido — chave segue inline no HTML (regex de extração válida, re-extraída ao vivo nesta rodada).

---

## 2. XP Asset — REPLICAVEL

| Request (curl_cffi chrome) | Status | Nota |
| :--- | :--- | :--- |
| `https://xpasset.com.br/fundos-etfs/` | **200** | listing renderizado server-side |
| `https://xpasset.com.br/fundos-etfs/xina11/` | **200** | detalhe; canonical interno = `/fundos/xina11/` (ref oembed/wp-json) |
| `https://www.xpasset.com.br/wp-json/cotas/v1/etf/XINA11` | **200** | `content-type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, 24 558 bytes, magic `PK\x03\x04` |

Contrato do XLSX já validado na Fase 0: sheet "Cotas" (~413 linhas diárias): Ticker/Fundo/Indexador/Data/Cota Patrimonial/PL/pontos do índice.

Cliente recomendado: sidecar Python (`curl_cffi`). CLI: `xp cotas --etf XINA11`.

### HAR (2026-08-23) — `hars/xp_xina11_2026-08-23.har.gz` — **novos endpoints JSON**
Chromium headless = **403 "Acesso Bloqueado"** (WAF flagra UA HeadlessChrome); headed sob xvfb = 200.
A página dispara (e o replay curl_cffi chrome confirma 200 em todos):

| Endpoint (`www.xpasset.com.br`) | Conteúdo | Valor |
| :--- | :--- | :--- |
| `GET /wp-json/composicao_carteira/v1/etf/XINA11` | JSON holdings: `portfolios[].date`, `assets[]{ticker ("MCHI US"), isin, quantity, price, value, proportion, category}` | **composição diária estruturada** — melhor que XLSX; cobre It Now-like parsing sem HTML |
| `GET /wp-json/quotas_table/v1/etf/XINA11` | JSON 413 cotas: `date, equityQuota, equityQuotaYields{monthly,yearly}` | substitui o parser XLSX do `cotas/v1` (que segue 200 como fallback) |
| `GET /wp-json/mz-api/v1/funds/resume` | resumo do fundo | metadata |
| `api.mziq.com/mzstockinfo/{uuid}/graphic?from&to&tickers&adjusted=false` | série de cota (500 na sessão capturada) | infra MZiQ; não confiável |

Replays ao vivo (curl_cffi chrome): composicao_carteira 200 (687 B, posição 21/08/2026);
quotas_table 200 (253 KB). WAF XP continua TLS-based: curl puro bloqueia, curl_cffi passa.

---

## 3. StatusInvest — REPLICAVEL (via HTML); API JSON interna = PRECISA_BROWSER p/ spec

| Request (curl_cffi chrome) | Status | Nota |
| :--- | :--- | :--- |
| `https://statusinvest.com.br/` | **200** | home ok |
| `https://statusinvest.com.br/etfs/bova11` | **200** | página de ticker **server-rendered** (title "BOVA11: ETF do Índice Ibovespa..."); caminho certo é `/etfs/{ticker}` (`/etf/{t}` = 404) |
| `POST /category/advancedsearchresult` (`CategoryType=8`, com/sem `search`) | **200** `{}` | endpoint JSON vivo e sem challenge, mas payloads testados voltam vazios — params exatos ficam em JS ofuscado (`a_x_a_x_i.min.js` etc.) |
| `GET /api/main/etf/BOVA11`, `/api/etf/tickerindicator` | **404** HTML | rotas não existem mais com esses nomes |
| `https://statusinvest.com.br/etfs` | **404** | listing correto: `/etf/eua` (ETFs EUA) — BR vai direto pro ticker |

Classificação: para metadata/fundamentais, **parse HTML server-rendered resolve** sem browser.
Só vale abrir HAR se quisermos o screener JSON (`advancedsearchresult`) — baixa prioridade
(redundante com catálogo próprio + CVM).

---

## 4. Invesco — PRECISA_BROWSER p/ holdings (recomendação prática: DESCARTAR)

### HAR (2026-08-23) — `hars/invesco_qqq_us_2026-08-23.har.gz` + `invesco_br` (falha DNS)
- `www.invesco.com.br` segue **DNS NXDOMAIN** nesta rede (Playwright: `ERR_NAME_NOT_RESOLVED`).
- Com browser real, a URL antiga de produto
  `/us/financial-products/etfs/product-detail?audienceType=Investor&ticker=QQQ`
  **redireciona para o site de marketing** `www.invesco.com/qqq-etf/en/home.html`.
  O HAR (180 entradas) mostra só JSONs de conteúdo (`*.model.json`, i18n), tag manager e
  **nenhum XHR de holdings/download** — o app de produto não existe mais nessa rota.
- Único endpoint de dados exposto: `GET https://dng-api.invesco.com/cache/v1/accounts/en_US/shareclasses/QQQ/performance/standard?idType=ticker&performanceSubType=cumulative&productType=ETF`
  → 200 JSON de performance acumulada (m1/m3/ytd/...); replay curl_cffi chrome = 200
  (content-type mentiroso `text/html`, body é JSON). Irrelevante p/ holdings.
- Alguns requests usam header `x-api-key` (valor no HAR de sessão anônima; sem ele os GETs de cache respondem).

Evidência anterior (curl_cffi chrome):

| Request | Resultado |
| :--- | :--- |
| `www.invesco.com.br` | **DNS NXDOMAIN** nesta rede (domínio não resolve nem via `getent`) |
| `www.invesco.com/us/financial-products/etfs/product-detail?...&ticker=QQQ` | **200**, mas SPA shell (262 KB, **zero** endpoints no HTML estático) |
| legado `.../etfs/holdings/main/holdings/0?audienceType=Investor&action=download&ticker=QQQ` | **200** `text/html` — devolve o app shell, **não** XLSX/CSV (endpoint migrou) |
| `www.invesco.com/us/rest/contentfunddata/v1/dailyholdings|funddata/QQQ` | **404** |
| `schwab.wallst.com` (mirror citado no plano) | **DNS NXDOMAIN** nesta rede |

Sem browser/HAR não há como descobrir o novo endpoint de download (site Next.js/Akamai carrega tudo por JS).
Como iShares CSV + SPDR XLSX já cobrem os ETFs globais que interessam (BDRs), custo > benefício:
**deixar fora da cadeia**; reabrir só se um ETF Invesco específico virar requisito (aí, rodar Playwright
em máquina com deps e versionar o HAR aqui).

---

## 5. Investing.com — DESCARTAR como fonte de dados (acesso parcialmente replicável)

Mudança importante vs Fase 0 (que registrou 403 via curl puro):

| Request (curl_cffi chrome) | Status | Nota |
| :--- | :--- | :--- |
| `https://br.investing.com/indices/bovespa` | **200** (1.46 MB) | Cloudflare passa com TLS fingerprint chrome; página contém `__NEXT_DATA__` (dados de cotação embarcados no HTML) |
| `https://tvc4.investing.com/feed.php` (com/sem params simples) | **400** | `["This url cannot be resolved"]` — vivo, exige contrato assinado/params corretos |
| `https://api.investing.com/api/editions`, `/api/financialdata/8830` | **404** | `{"@errors":["Endpoint in not found in service"]}` — serviço vivo, rotas exigem contrato exato (GraphQL + headers próprios) |

### HAR (2026-08-23) — `hars/investing_bova11_2026-08-23.har.gz` — **upgrade de classificação**
Headless = 403; headed sob xvfb = 200. O widget gráfico dispara:

| Request | Resultado | Nota |
| :--- | :--- | :--- |
| `GET https://api.investing.com/api/financialdata/17920/historical/chart/?interval=P1D&pointscount=160` | **200** `application/json` | `data[] = [epoch_ms, open, high, low, close, volume, 0]`; `17920` = par IBOV. **Replay curl_cffi chrome = 200** (9,2 KB, ~160 barras) com `Origin/Referer` br.investing.com — primeiro endpoint OHLCV real encontrado nesta fonte |
| `GET sbcharts.investing.com/charts_xml/{md5}_1day.json` | 200 | cache de série por hash opaco (sem descoberta de hash) |
| `GET br.investing.com/localized-content/top-strip?user_type=guest&is_mobile=0&geo_country_code=BR` | 200 | conteúdo localizado |
| `tvc4.investing.com/feed.php` | não apareceu no HAR | segue sem contrato |

Veredito revisado: **REPLICAVEL parcial** (índices via `financialdata/{pairId}/historical/chart/`),
mas dado 100% redundante com Brapi/Yahoo/TV → segue fora da cadeia; reabrir só como contingência
(seria preciso mapear pairIds por ticker).

---

## 6. It Now (Itaú Asset) — REPLICAVEL — descoberta pós-HAR (fecha pendência da Fase 3)

### Domínio
- `itnow.com.br` (apex) = **DNS NXDOMAIN** nesta rede; o domínio real é **`www.itnow.com.br`** (Akamai).
- Página de composição HTML existe: `https://www.itnow.com.br/bovv11/composicao/` → **200** com 6 tabelas
  (rota `/{ticker}/composicao/`; `/etf/...` e `/etfs/...` = 404). Listing em `/nossos-etfs/{categoria}/`.
- Chromium headless = 403; headed sob xvfb = 200 (mesmo padrão WAF do XP).

### API estruturada revelada pelo HAR — `hars/itnow_bovv11_2026-08-23.har.gz`
A página não precisa de parse de tabela: ela mesma consome JSON interno:

| Request | Resultado |
| :--- | :--- |
| `POST https://www.itnow.com.br/history-api-json/?type=composicoes-indices&fundo=BRBOVVCTF009` | **200** JSON, ~60 KB: `dados[]{nome_ticker_fundo ("VALE3"...), descricao_ticker_fundo, peso..., data_hora_posicao_carteira_fundo}` — posição 21/08/2026 |
| idem `type=informacoes-gerais \| setores-maiores-pesos \| sinteticos \| analiticos \| emprestimos-titulos \| rentabilidade` | **200** JSON cada |

- Parâmetro `fundo` = código tipo-ISIN do fundo (`BRBOVVCTF009` p/ BOVV11), **não o ticker** —
  mapear via página/listing do ETF (aparece no HTML da página do ticker).
- **Replay curl_cffi chrome = 200** nos dois tipos testados (`composicoes-indices`, `analiticos`),
  com `Origin/Referer` da própria página. Sem auth, sem challenge.
- Cliente recomendado: **C# nativo** (HttpClient POST + System.Text.Json) — sem sidecar; fallback
  AngleSharp na tabela HTML se a API mudar.

---

## 7. BlackRock iShares Brasil — REPLICAVEL (fecha pendência da Fase 3)

### HARs — `hars/blackrock_br_productlist_2026-08-23.har.gz` e `blackrock_bova11_2026-08-23.har.gz`
- Rotas mortas confirmadas: `blackrock.com/br/intermediarios{,/produtos}` e `/br/products` (sem slug) = **404**;
  `ishares.com.br` resolve DNS mas **connection timeout**; `blackrockam.com.br` NXDOMAIN.
- Rota viva: **`https://www.blackrock.com/br/products/product-list`** (200) — listing JS alimenta-se de
  `GET /br/product-list/product-screener-v3.1.jsn?dcrPath=templatedata/config/product-screener-v3/data/pt/br-one/...` (config).
- Página de produto correta do BOVA11 (link extraído do DOM após render):
  **`https://www.blackrock.com/br/products/251816/ishares-ibovespa-fundo-de-ndice-fund`** (200).
- Link ajax CSV presente no próprio HTML estático (regex vale sem browser):
  `/br/products/251816/ishares-ibovespa-fundo-de-ndice-fund/**1506433276998**.ajax?fileType=csv&fileName=BOVA11_holdings&dataType=fund`
  — o `{hash}` antigo documentado no PROVIDERS.md (`1495093766861`) segue morto; o hash é por produto
  e pode rotacionar → extrair da página (1 fetch HTML + regex `[^"'\s]+\.ajax\?fileType=csv[^"'\s]*`).
- O produto também dispara variante JSON: `.../{hash}.ajax?tab=all&fileType=json&asOfDate=20260820` (200).
- **CSV baixado ao vivo (curl_cffi chrome): 200** `text/csv`, 14.105 bytes, 88 linhas.
  Layout: linha 1 `Sep=,`; linha 2 `Fund Holdings as of,"20 ago. 2026"`; header
  `Ticker,Name,Sector,Asset Class,Market Value,Weight (%),Notional Value,Shares,Price,Location,Exchange,Currency,FX Rate,Market Currency`;
  ~84 linhas de posições (VALE3 11%,00 · PETR4 8,21% · ITUB4 7,96%...). Números pt-BR (`.` milhar, `,` decimal).

---

## Desvios

1. **Playwright — RESOLVIDO na mesma data** (desvio original do passo 1 do plano):
   - Estado inicial: launch ❌ (`libglib-2.0.so.0` ausente) e `sudo` indisponível → primeira rodada 100% curl_cffi, sem HAR.
   - Com sudo liberado: `playwright install-deps chromium` ✅ (85 pacotes via apt; repo terceiro
     `griffo.io` falha SSL mas é irrelevante) e headless launch ✅ (Chromium 151).
   - **Novo achado**: WAFs de XP Asset, It Now e Investing.com rejeitam o UA `HeadlessChrome` (403
     "Acesso Bloqueado") mesmo com browser real — solução: captura **headed sob `xvfb-run`**
     (200 nas três). InfoMoney/BlackRock/Invesco-US passam até headless.
   - DOD "HARs versionados" **cumprido**: 7 HARs em `hars/`, redigidos (chave APIM) e gzip.
2. **URL InfoMoney corrigida**: `/cotacoes/b3/{ticker}/` (Fase 0/plano) hoje dá 404; rota real `/cotibovacoes/{ticker}/`.
3. **Investing.com deixou de bloquear curl_cffi**: evidência da Fase 0 (403 plain curl) continua válida para curl puro; com TLS fingerprint chrome passou 200.
4. `www.invesco.com.br` e `schwab.wallst.com` não resolvem DNS nesta rede (NXDOMAIN) — pode ser restrição local de resolver, não conclusão global sobre os domínios.

## Como reproduzir

- **HARs (2026-08-23, tarde):** `/tmp/opencode/recon2/` — `capture_hars.py` (headless),
  `capture_headed.py` (xvfb p/ XP/ItNow/Investing), `redact_hars.py` (redação + resumo →
  `har_summary.json`), `replay_har.py` (validação curl_cffi dos achados). Venv:
  `cd /tmp/opencode/fase05 && xvfb-run -a .venv/bin/python <script>`.
- **Recon curl_cffi (manhã):** `/tmp/opencode/fase05/` — `probe_im2.py`, `probe_im3.py`
  (InfoMoney API), `recon_nobrowser.py` (XP/Invesco/StatusInvest/Investing), `recon_r4.py`/`recon_r5.py`
  (StatusInvest/Invesco/Investing follow-ups); esta rodada: `probe_taskb.py` (It Now/iShares DNS+rotas),
  `probe_ishares.py`, `probe_health.py` (CSV BlackRock + health XP/InfoMoney).
- Ambiente: `uv venv && uv pip install curl-cffi==0.16.1 playwright` + `playwright install chromium`,
  `impersonate="chrome"`, timeout ≤20 s por chamada.
