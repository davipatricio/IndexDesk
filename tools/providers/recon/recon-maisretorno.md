# Recon maisretorno.com — IndexDesk ingestão

Data: 2026-08-26 · 18 requests · transporte `tools/providers/sidecar` (curl_cffi chrome) · read-only

## TL;DR

Next.js SSR com dados completos embutidos em `__NEXT_DATA__` + rotas `_next/data/<buildId>/...` públicas (JSON limpo, sem auth). API interna `data.maisretorno.com/api/v3` existe mas exige credencial (401). **Viabilidade: ALTA** para séries de rentabilidade mensal/anual completa + cadastro (CNPJ/ISIN/setor/CVM), gestoras e administradoras. Cobertura nula para proventos/DY/PVP/PL/taxa adm.

---

## (a) Endpoints e dados descobertos

### Canal principal — `_next/data` (público, sem cookie/auth)

BuildId atual: `gUhXo-UNCu8LNMvapdPrm` (muda a cada deploy — extrair do HTML a cada run).

| Método | URL | Uso |
| :--- | :--- | :--- |
| GET | `/_next/data/{buildId}/etf/{slug}.json` | Detalhe ETF |
| GET | `/_next/data/{buildId}/acoes/{slug}.json` | Detalhe ação |
| GET | `/_next/data/{buildId}/fii/{slug}.json` | Detalhe FII |
| GET | `/_next/data/{buildId}/indice/{slug}.json` | Detalhe índice |
| GET | `/_next/data/{buildId}/lista-acoes.json` | Lista ações p.1 (50/req) |
| GET | `/_next/data/{buildId}/lista-acoes/page/{n}.json` | Lista ações paginada |
| GET | `/_next/data/{buildId}/gestores/page/{n}.json` | Gestoras paginada (100/req) |
| GET | `/_next/data/{buildId}/administradores/page/{n}.json` | Administradoras paginada |

Exemplo real (`/_next/data/gUhXo-UNCu8LNMvapdPrm/etf/wrld11.json`, truncado ~300):

```json
{"pageProps":{"slug":"wrld11","nicename":"WRLD11","cnpj":"42280262000105","pageTitle":"WRLD11 - INVESTO FTSE GLOBAL EQUITIES ETF FDO INDICE - IE","stats":{"stats":{"best_monthly_return":8.9818,"worst_monthly_return":-11.7183,"positive_months":32,"negative_months":26,"timeframe":{"last_3_months":{"pr
```

Observações:
- Query string `?page=N` é **ignorada** — paginação só por path `/page/N`.
- HTML da página contém exatamente o mesmo JSON em `<script id="__NEXT_DATA__">`; uma única fonte serve para ambos os modos (fetch JSON direto ou parse HTML).

### APIs internas (achadas no bundle `_app-*.js`) — NÃO usáveis diretamente

Base URLs axios registradas:

- `https://data.maisretorno.com/api/v3` → **401** em tudo (`/etf/wrld11`, `/stocks/petr4`, `/search?q=petr`, `/search?query=petr`)
- `https://api.maisretorno.com/v3` → FastAPI vivo; `/v3/search?q=petr` → `404 {"detail":"Not Found"}`; paths reais desconhecidos
- `https://userdata.maisretorno.com` (BFF usuário)
- `https://portal.maisretorno.com` (portal assinatura)
- `https://data.maisretorno.com/portfolio-relation`

SSR injeta credenciais server-side; cliente browser não chama essas bases nas páginas testadas.

### Busca/autocomplete

Nenhum endpoint público encontrado. `/api/search?q=` → 404 (HTML 404 do Next); chunk `_app` só expõe `.get("/news")`, `.get("/roadmap")`. Registro de endpoints vive em chunk compartilhado minificado (`u.ZS.etfList` etc.) — não mapeado dentro do orçamento.

### Shape dos dados

**Detalhe (ETF/FII/ação/índice)** — `pageProps`:

- `slug`, `nicename`, `cnpj` (sem máscara), `pageTitle`, `canonical_url`
- `formattedHeaders`: razão social, setor/subsetor/segmento de atuação, CNPJ mascarado, ISIN
- `formattedStats[]`: `{slug, displayName, value, type}` — `profitability_begin/volatility_begin/sharpe_ratio_begin` + variantes `12M`
- `stats.stats`:
  - `best_monthly_return`, `worst_monthly_return`, `positive_months`, `negative_months`
  - `timeframe`: `mtd, ytd, last_3/6/12/24/36/48/60_months, begin` (rentab/vol/sharpe por janela)
  - `first_quote_date`, `last_quote_date` (epoch ms)
- `stats.years`: histórico completo — `{ano: {"1".."12": %, "year": %ano, "accrued": %acum}}`. Profundidade = vida toda do papel: PETR4 desde **1994** (33 anos), MXRF11 desde 2012, WRLD11 desde IPO 2021, IBOV desde 1994.

**`lista-acoes`** — `pageProps.rawList[]` (50/pág, 528 total, 11 págs), campos por linha:

`cnpj, code_cvm, company_name, actuation_segment, actuation_sector, actuation_subsector, issuer_company, ticker, asset, situation, asset_description, canonical_url, type, trading_currency, has_quotes, isin`

(`list[]` é versão display; usar `rawList`.)

**`gestores`** — 100/pág, **3.236 registros**, 33 págs. Item: `{cnpj, nicename, slug, networth, quota_holders, canonical_url}`.

**`administradores`** — 100/pág, **334 registros**, 4 págs. Item: `{id (=CNPJ numérico), slug, nicename, networth, quota_holders, canonical_url}`.

### JSON-LD

Apenas metadata SEO (`WebPage`, `NewsMediaOrganization`, `ItemList`). Sem dado financeiro.

## (b) Cobertura por tipo de página

| Dado | ETF | Ação | FII | Índice | Gestora | Admin. |
| :--- | :-: | :-: | :-: | :-: | :-: | :-: |
| Rentab. mensal/anual histórica completa | ✅ | ✅ | ✅ | ✅ (desde 1994) | — | — |
| Rentab./Vol/Sharpe janelas (MTD..total) | ✅ | ✅ | ✅ | ✅ | — | — |
| CNPJ / ISIN | ✅/✅ | ✅/✅ | ✅/✅ | — | ✅ | ✅ |
| Setor/subsetor/segmento | ⚠️ vazio p/ ETF | ✅ | ⚠️ vazio p/ MXRF11 | — | — | — |
| Razão social | ✅ | ✅ | ✅ | — | ✅ nicename | ✅ nicename |
| PL/net worth | ❌ | ❌ | ❌ | — | ✅ (R$) | ⚠️ null |
| Cota holders | — | — | — | — | ✅ | ⚠️ null |
| Proventos/dividendos | ❌ | ❌ | ❌ | — | — | — |
| DY / PVP | — | — | ❌ | — | — | — |
| Taxa adm | ❌ | — | ❌ | — | — | — |
| code_cvm / situation / issuer | — | ✅ (via rawList lista-acoes) | — | — | — | — |

Páginas-filhas não mapeadas (prováveis): `/etf/[slug]/...` subpáginas citadas na meta description ("resultados, análises, notícias"), `/gestores/[slug]` individual, `/lista-ativos`, `/comparacao-fundos`.

## (c) Notas anti-bot

- 18/18 requests HTTP 200 (ou 401/404 legítimos). Nada de Cloudflare challenge, rate limit ou captcha.
- curl_cffi chrome impersonation + sessão com cookies suficiente.
- `_next/data/*.json` acessível **sem** header especial nem cookie de primeira visita (testado via sidecar stateless).
- 401 do `data.*` é auth de aplicação, não bloqueio.

## (d) Veredicto

**ALTO** para: catálogo B3 (528 ações c/ CNPJ+CVM+ISIN+situação), séries históricas mensal/anual desde a origem de ETFs/FIIs/ações/índices, diretórios de gestoras (3.236) e administradoras (334) com CNPJ e PL.

**Riscos / lacunas**

1. BuildId muda por deploy — resolver dinamicamente (GET home ou qualquer página, extrair `"buildId"`).
2. Zero dado de proventos/DY/PVP/PL/taxa adm — complementar com outra fonte para fiscal/corporate.
3. Slug discovery: não há sitemap mapeado nesta sessão; lista-acoes cobre ações, mas enumeração completa de ETFs/FIIs precisa de `/lista-etfs`-equivalente ou sitemap.xml (não testado).
4. Sem contrato estável — Next.js pode mudar shape/buildId sem aviso; tratar como fonte best-effort, nunca canônica.
5. API interna v3 autenticada: possível chave pública embutida em algum chunk, custo/benefício baixo vs. `_next/data`.
6. Volume: ~528 ações × 1 req + 15 págs de listas + ~dezenas ETF/FII = poucas centenas requests por full sync; respeitar pacing mesmo sem 429 observado.

Artefatos crus: `/tmp/opencode/mr-{etf,acoes,fii,lista,indice,gestores,admins}.html|.json`, `mr-nd-{etf,gest2,lista2}.json`, `mr-app.js`, `mr-etf-chunk.js`.
