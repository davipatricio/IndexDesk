# Recon investidor10.com.br — IndexDesk / etfhub-b3

Data: 2026-08-26 · Somente leitura · 21 requests totais ao site (5 páginas + 15 APIs + 1 JS) · Transporte: sidecar `curl_cffi` impersonate=chrome · Nenhum bloqueio WAF.

Páginas analisadas (todas 200 OK):
- `/fiis/mxrf11/` (1.64 MB) · id interno **56**
- `/etfs/vwra11/` (1.08 MB) · id interno **233935**
- `/acoes/vale3/` (1.89 MB) · ids internos **18** (ticker/indicadores) e **21** (balanços)
- `/indices/ibov/` (1.30 MB) · id interno **1**
- `/stocks/tsla/` (2.86 MB) · id interno **3**

## 0. Arquitetura geral do site

Laravel + jQuery + ECharts (não é Next.js; **não há `__NEXT_DATA__`**). Duas fontes de dados:

1. **HTML server-rendered**: cards de cotação atual, indicadores-chave (DY, P/VP, P/L…), setores, FAQ (JSON-LD `FAQPage`, `Article`, `Organization`).
2. **AJAX JSON** para séries temporais: gráficos de cotação, dividendos, DY, vacância, patrimônio, indicadores históricos e balanços. Divs ficam vazias no HTML e são preenchidas por `$.get`.

IDs numéricos internos aparecem nas URLs dos endpoints (data-* e scripts inline).

## (a) Endpoints descobertos

Base: `https://investidor10.com.br`. Todos GET, sem cookie/auth. Nenhum header especial necessário nos testes diretos (sidecar chrome). `✓` = testado com 200 real.

| Endpoint | Params obrigatórios | Notas |
|---|---|---|
| ✓ `/api/cotacoes/batch?tickers=MXRF11,VALE3,PETR4,HGLG11` | `tickers` CSV | Cotação atual em lote, qualquer classe B3. Melhor endpoint p/ ingestão diária. |
| ✓ `/api/fii/cotacoes/chart/{idFII}/{dias}/{ajustado}` | id, dias (ex. 365, 1825), ajustado `true\|false` | Default sem params = só últimos ~3 dias. `1825/false` = 5 anos daily (1525 pts). Séries `real`, `dolar`, `euro`. |
| ✓ `/api/fii/dividendos/chart/{id}/{dias}/{grupo}` | grupo `mes\|ano` | Histórico mensal de rendimentos (60 itens p/ 1825d). |
| ✓ `/api/fii/dividend-yield/chart/{id}/{dias}/{grupo}` | idem | DY mensal (%). |
| `/api/fii/historico-taxa-vacancia/{id}/` | provavelmente `{dias}` também | Retornou **HTTP 500** sem período (não re-testado p/ poupar budget). |
| `/api/fii/valor-patrimonial/chart/{id}/` | id | Patrimônio líquido/cota (referenciado como `netWorth` no JS da página). |
| ✓ `/api/cotacoes/acao/chart/vale3/` | ticker (aceita sem id) | 360 pts (~1 ano) default; mesmo padrão `{dias}/{ajustado}` esperado. Preços fracionários ⇒ ajustados por proventos. Séries `real/dolar/euro`. |
| ✓ `/api/historico-indicadores/{tickerId}/{dias}/?v=2` | id do ticker, dias (testado 10) | **31 indicadores** × anos (2016→hoje): P/L, EV/EBITDA, ROE, ROIC, margens, DY, payout, dívida, CAGR… |
| ✓ `/api/balancos/balancoresultados/chart/{companyId}/{periodo}/{categoria}/` | ver inline JS da página | DRE. Mesmo padrão para `balancopatrimonial`, `ativospassivos`, `receitaliquida`. |
| `/api/cotacao-lucro/vale3/` | ticker | Evolução preço × lucro. |
| `/api/acoes/payout-chart/` e `/api/acoes/dividends-map` | ? (não testados) | Payout/mapa de proventos. |
| ✓ `/api/etfs/cotacoes/chart/{idETF}/{dias}?` | id ETF | 307 pts default (~11m), campo `last_update`. |
| ✓ `/api/stock/cotacoes/chart/{idStock}/` | id stock EUA | 349 pts (~1 ano), close-only. |
| `/api/stock/dividendos/chart/{id}/` e `/api/stock/dividend-yield/chart/{id}/` | id | TSLA retornou `[]` (não paga); endpoint existe. |
| `/api/international/...` (balancoresultados, balanco-patrimonial, receitaliquida, fluxo-caixa, evolucao-patrimonial, cotacao-lucro) | id stock | Balanços DRE/balanço/fluxo de empresas EUA. |
| ✓ `/api/indices/cotacoes/{idIndice}/` | id índice (ibov=1) | Pontos diários (~344 pts default). Outros ids: SELIC, IPCA, IFIX, CDI, SMLL, IBXL, SPX, IDIV… |
| ✓ `/api/quotations/one-day/{TICKER}/` | ticker | Intraday 5-min (16 KB/dia), timestamp `YYYY-MM-DD HH:MM:SS`. |
| `/api/quotations/seven-days/{TICKER}/` | ticker | Mesma família (não testado). |
| ✓ `/api/fii/comparador/table/{idFII}/{modo}/` | modo `all\|type\|segment\|segment_type` | Tabela comparativa de FIIs: DY, P/VP, PL (net_worth), segmento, tipo. Útil p/ rankings de FIIs. |

Não explorados (fora do budget): `/api/search-news/%QUERY`, `/api/lista-comentarios/{assetId}/?page=N` (paginada), rotas `seguir-*` (POST, exigem login CSRF).

## (b) Shapes (exemplos reais truncados)

```
GET /api/cotacoes/batch?tickers=MXRF11,VALE3,PETR4,HGLG11
{"MXRF11":{"price":9.24,"last_update":"2026-08-25 18:55:00"},"VALE3":{"price":78.79,...}}

GET /api/fii/cotacoes/chart/56/1825/false
{"real":[{"price":9.26,"created_at":"21\/08\/2026"},...],"dolar":[...],"euro":[...]}

GET /api/cotacoes/acao/chart/vale3/
{"real":[{"price":52.346031434584,"created_at":"25\/08\/2025 00:00"},...]}

GET /api/fii/dividendos/chart/56/1825/mes
[{"price":0.1,"created_at":"02\/2026"},...]   // MM/YYYY agregado por mês

GET /api/fii/dividend-yield/chart/56/
[{"price":1,"created_at":"02\/2026"}]          // % DY mensal

GET /api/historico-indicadores/18/10/?v=2
{"P\/L":[{"year":"Atual","key":"p_l","value":33.69,"type":"decimal"},
 {"id":2,"indicator_id":2,"ticker_id":18,"value":23.124506714487,"year":"2025",...}], ...}

GET /api/etfs/cotacoes/chart/233935/
[{"price":101.52,"last_update":"01\/10\/2025"},...]

GET /api/stock/cotacoes/chart/3/
[{"created_at":"26\/08\/2025","price":346.6},...]

GET /api/indices/cotacoes/1/
[{"points":"137428.69","last_update":"26\/08\/2025"},...]

GET /api/quotations/one-day/MXRF11/
{"real":[{"price":9.19,"created_at":"2026-08-26 10:06:00"},...]}

GET /api/fii/comparador/table/56/all/
{"data":[{"title":"MXRF11","full_name":"MAXI RENDA","price":9.23,"variation_12m":9.88,
 "dividend_yield":12.93,"p_vp":1,"net_worth":5253747540,"segment":"H\u00edbrido",
 "type":"Fundo de Papel",...}]}
```

Observações de shape: datas em `DD/MM/YYYY` (ou `MM/YYYY`) como string — normalizar. Chaves variam entre endpoints (`price`+`created_at` vs `price`+`last_update` vs `points`). Sem OHLCV em nenhum gráfico — apenas **close**.

## (c) Cobertura por classe de ativo

| Dado | FII | ETF (BDR) | Ação | Índice | Stock EUA |
|---|---|---|---|---|---|
| Cotação atual (batch API) | ✓ | ✓ | ✓ | pontos | ✓ (via stock chart) |
| Intraday 1d/7d | ✓ | ✓ | ✓ | ? | ✓ |
| Histórico close (ajustável p/ ações/FII) | ✓ até 15y (`5475`) | ✓ | ✓ | ✓ | ✓ |
| OHLCV volume | ✗ | ✗ | ✗ | ✗ | ✗ |
| Proventos histórico | ✓ mensal/anual | parcial (pág. tem seção dividends) | via página/endpoints próprios | n/a | ✓ (se pagar) |
| DY histórico | ✓ | ? | ✓ (histind) | n/a | ✓ |
| Indicadores valuation (P/L, EV/EBITDA…) | P/VP via comparador | básicos na página | ✓ 31 indicadores × 10 anos | n/a | via international/* |
| Vacância / imóveis (FII) | endpoint existe (500 s/ params) | n/a | n/a | n/a | n/a |
| PL / valor patrimonial | ✓ endpoint | página | balancos/* | n/a | evolucao-patrimonial |
| Balanço/DRE/fluxo | n/a | n/a | ✓ `/api/balancos/*` | n/a | ✓ `/api/international/*` |
| Gestão/administração | página (HTML) | página (nome gestora) | limitado | n/a | página |
| Composição/carteira | não visto na pág. alvo | não visto (possível subpágina) | n/a | composição não exposta na página testada | n/a |
| Comparador/ranking | ✓ comparador table | ? | rankings web | n/a | n/a |

## (d) Anti-bot / WAF observado

- Cloudflare presente (beacon insights) mas **nenhum challenge** nas 21 requests.
- Todas as páginas e APIs: HTTP 200 com `curl_cffi` chrome simples, sem cookies prévios, sem `Referer`/`X-Requested-With`.
- APIs respondem sem sessão — nem CSRF. Apenas rotas de escrita (`seguir`, comentários POST) exigiriam auth.
- Único erro: 500 do próprio Laravel em endpoint chamado sem parâmetro esperado (vacância).
- Rate-limit desconhecido — não testado deliberadamente. Volume alto pode acionar CF; usar pacing baixo.

## (e) Veredicto

**VIABILIDADE: ALTA** como fonte secundária/enriquecimento; **BAIXA-MÉDIA como fonte primária de cotações** (sem OHLCV/volume, só close).

Prós: APIs públicas sem auth, batch quotes, histórico longo ajustável, 31 indicadores fundamentamentais com 10 anos (ouro p/ ações), comparador FIIs quase pronto p/ rankings, cobre FII+ETF-BDR+ação+B3+EUA+índices/macros (CDI/IPCA/SELIC).

Riscos:
1. **ToS/legal** — dados fundamentalistas proprietários; checar ToS antes de ingestão em produção (site comercializa Investidor10 Pro; scraping em massa pode violar termos).
2. Close-only, sem volume — backtests de volume/liquidez impossíveis com esta fonte.
3. Formatos inconsistentes entre endpoints (nomes de campos, formatos de data BR).
4. IDs internos instáveis (56/18/21/233935) — precisam de resolução ticker→id (descobrir rota de busca: `/api/component-list/layout.search.result-item?_source=search/%QUERY` é candidata, não testada).
5. Vacância precisa de params corretos (500 sem eles).
6. Depêndencia de JS obfuscado para descobrir mudanças de contrato.

Arquivos crus em `/tmp/opencode/i10-*.html`, `/tmp/opencode/api-*.json`, `/tmp/opencode/i10-fii-show.js`.
