# Recon — fundsexplorer.com.br (FIIs / FI-Infra / FI-Agro)

Data: 2026-08-26 · Read-only · Transporte: sidecar `curl_cffi` (impersonate=chrome) + `curl_cffi.Session`
Total de requests: **15** (orçamento respeitado). Artefatos brutos em `/tmp/opencode/fe-*`.

---

## TL;DR

- A antiga API REST `/api/v1/funds/{ticker}` está **morta na prática**: responde **HTTP 500** (não 404) para maiúsculo e minúsculo. Existe rota no backend, mas quebrada.
- O site é **WordPress** (`wp-content/themes/fundsexplorer`, jQuery, admin-ajax), não Next/Nuxt. Sem `__NEXT_DATA__`/`__NUXT__`.
- **Descoberta principal**: endpoints WordPress AJAX em `POST /wp-admin/admin-ajax.php` com actions `funds-get-income`, `funds-get-quotations`, `funds-get-patrimonials` — JSON limpo, sem cookies obrigatórios, nonce extraído do HTML da página.
- Histórico profundo: rendimentos mensais desde **2016-06** (122 meses), cotação diária **5 anos** (~1.248 pontos), patrimônio/cota mensal desde 2016-01.
- Detalhe do fundo é **server-side rendered** com CNPJ, segmento, P/VP, DY, liquidez etc. direto no HTML.
- Listas `/funds` (≈696 tickers), `/fiinfras` (30), `/fiagros` (51) vêm completas no HTML inicial, sem paginação XHR.
- Veredicto: **viabilidade ALTA** para ingestão local-first (Worker), com ressalvas (nonce, sem contrato formal, Cloudflare presente mas branda com fingerprint Chrome).

---

## (a) Endpoints vivos

### a.1 — POST `/wp-admin/admin-ajax.php` (WordPress AJAX) — **PRINCIPAL**

Autenticação: nenhuma (anônimo). Requisitos observados:

| Header | Valor | Obrigatório? |
| :--- | :--- | :--- |
| `Content-Type` | `application/x-www-form-urlencoded` (FormData) | sim |
| `X-CSRF-TOKEN` | nonce de 10 chars, embutido no HTML da página de detalhe (`data-nonce`) | sim (sem ele não testado; com nonce válido funcionou **sem cookies**) |
| `X-Requested-With` | `XMLHttpRequest` | recomendado |
| Cookies | **não obrigatórios** — POST sem sessão/cookies retornou 200 idêntico | não |

Body: `action=<action>&fund=<TICKER>` (ticker maiúsculo testado; o JS envia o valor de `data-fund`, ex. `KNCR11`).

Nonces observados (estáveis entre loads consecutivos; nonce WP anônimo costuma valer 12–24h):

```
funds-get-income         → data-nonce="1dc2b34a5a"  (#dividends-container)
funds-get-quotations     → data-nonce="43c0dbc1c4"  (#quotations-chart-container)
funds-get-patrimonials   → data-nonce="aa5f325950"  (#patrimonials-chart)
funds-get-simulator-values → data-nonce="d52a13fd11"
funds-get-all-properties → data-nonce="3c21f3734c"
```

Os nonces ficam em atributos `data-action`/`data-nonce`/`data-fund` no HTML da página `/funds/<ticker>` — um parse de HTML por lote dá os tokens.

#### `action=funds-get-income` — rendimentos mensais (65 KB p/ KNCR11)

```json
{"success":true,"data":[
  {"id":"26007912","ticker":"KNCR11","valor":"1.2500","tipo":"Rendimento",
   "data_pagamento":"2026-08-13","data_base":"2026-07-31","yeld":"1.1600",
   "rentabilidade":"18.2383","cotacao_fechamento":"108.0700",
   "variacao_cotacao_mes":"0.1668","mes":"8","ano":"2026",
   "rentabilidade_mes":"1.3288",
   "media_yield_3m":"1.0700","media_yield_6m":"1.0533","media_yield_12m":"1.1725",
   "soma_yield_3m":"3.2100","soma_yield_6m":"6.3200","soma_yield_12m":"14.0700",
   "soma_yield_ano_corrente":"8.7700",
   "created_at":"2026-08-25 21:03:13","updated_at":"2026-08-25 21:03:13"}, ...]}
```

Cobertura KNCR11: **122 linhas**, `data_base` de `2016-06-30` a `2026-07-31` (**~10 anos**). `tipo` observado: `Rendimento` (outros fundos podem ter `Amortização`/`Rendimento de Capital`). Todos os valores como **string decimal com ponto**. Campos agregados (médias/somas 3m/6m/12m) são deriváveis — ingerir bruto e recalcular.

#### `action=funds-get-quotations` — série diária de cotação (65 KB)

```json
{"success":true,"data":[{
  "post_title":"KNCR11",
  "post_excerpt":"Kinea Rendimentos Imobiliários",
  "quotations":"[{\"price\":51.55,\"date\":\"26\\/08\\/21 00:00\"},{...}]"}]}
```

Atenção ao shape: `quotations` é **string JSON duplamente serializada** (array dentro de string, datas `dd/mm/yy HH:MM` com escapes `\/`). KNCR11: **1.248 pontos**, `26/08/21` a `25/08/26` (**janela fixa ~5 anos**, sem param de período). Preço numérico real (51.55 → 107.08).

#### `action=funds-get-patrimonials` — patrimônio por cota mensal (5 KB)

```json
{"success":true,"data":[{"periodo":"2016-01-01","valor":"100.2409"},...,{"periodo":"2026-06-01","valor":"102.4800"}]}
```

121 linhas, mensal desde 2016-01. É o **valor patrimonial por cota** (base do P/VP), não o PL absoluto.

#### Outras actions (não testadas, orçamento)

- `funds-get-all-properties` — propriedades/imóveis do fundo (presente só em fundos de tijolo).
- `funds-get-simulator-values` — dados do simulador da página.
- Ambas seguem o mesmo padrão `POST action+fund(+nonce)`; baixo risco de integração.

### a.2 — GET `/api/v1/funds/{ticker}` — **LEGADO/MORTO**

```
GET https://www.fundsexplorer.com.br/api/v1/funds/KNCR11  → HTTP 500
GET https://www.fundsexplorer.com.br/api/v1/funds/kncr11  → HTTP 500
```

500 (não 404/403) sugere rota ainda mapeada com erro interno — não usar. O front atual não a referencia (grep nos bundles: zero menções).

### a.3 — GET páginas HTML (server-side)

| URL | Status | Tamanho | Conteúdo |
| :--- | :--- | :--- | :--- |
| `/funds/kncr11` | 200 | 333 KB | detalhe completo SSR |
| `/funds` | 200 | 566 KB | índice A–Z com ~696 tickers |
| `/fiinfras` | 200 | 83 KB | 30 cards FI-Infra |
| `/fiagros` | 200 | 100 KB | 51 cards FI-Agro |

### a.4 — Arquivos estáticos

- `wp-content/themes/fundsexplorer/dist/single.min.js` (página de detalhe, 432 KB) — contém a lógica dos charts ECharts e das chamadas admin-ajax citadas.
- `dist/frontend.min.js` (listas/filtros).
- Nenhum outro host de API além do próprio domínio (+ `files.fundsexplorer.com.br` para imagens/PDFs, CDN de assets).

---

## (b) Cobertura por página

### Detalhe `/funds/<ticker>` (SSR, campos extraíveis por parse HTML + JSON-LD)

JSON-LD (`application/ld+json`, tipo `InvestmentFund`):

```json
{"@type":"InvestmentFund","name":"Kinea Rendimentos Imobiliários",
 "category":"Papel",
 "amount":{"@type":"MonetaryAmount","name":"Patrimônio líquido","currency":"BRL",
           "value":"10974808568.74"}}
```

HTML server-side (labels encontrados no DOM de KNCR11):

| Campo | Exemplo KNCR11 |
| :--- | :--- |
| Ticker/nome | KNCR11 — Kinea Rendimentos Imobiliários |
| Cotação / variação dia | R$ 107,08 / 0,09% |
| CNPJ | 16.706.958/0001-32 |
| Segmento / Segmento ANBIMA | Papéis / Títulos e Valores Mobiliários |
| Público alvo | Investidores em Geral |
| Tipo de gestão | (presente no bloco) |
| Cotas emitidas | 107.089.622 |
| Número de cotistas | 592.231 |
| P/VP | 1,05 |
| Rentab. no mês | 1,00% |
| Rentab. 12 meses | 13,36% |
| Dividend Yield 12m | 13,36% |
| Último rendimento | R$ 1,25 |
| Liquidez média diária | R$ 19,8 M |
| Patrimônio líquido | R$ 10,97 bi (JSON-LD) |
| Eventos/agenda (assembleias, informes) | presentes com datas |

**FFO/vacância**: ausentes na página de KNCR11 (FFO = 0 ocorrências; vacância = 0). Podem existir em fundos de lajes/tijolo (`funds-get-all-properties` cobre imóveis). Não confirmado neste recon.

Gráficos: cotação/rendimentos/patrimônio renderizados client-side via **ECharts** com dados vindos exclusivamente dos endpoints admin-ajax (seção a.1) — nada de série embutida no HTML além dos indicadores pontuais.

### Lista `/funds`

- Índice alfabético A–Z completo server-side: **~696 tickers únicos** (regex `/funds/[a-z0-9]{4,6}`).
- Card: ticker, nome, classificação de tipo (ex.: `Fip:`), link. **Sem colunas de métricas** (P/VP/DY/liquidez não estão nesta página — 0 ocorrências).
- Filtros por letra são client-side sobre a lista já renderizada. Sem paginação XHR.
- Uso ideal: **descoberta de universo de tickers** (seed para crawlear detalhes).

### Lista `/fiinfras`

- **30 cards** server-side (swiper/carousel). Card: tipo (`FIInfra: Indefinido` — classificação pouco preenchida), ticker, nome, cotação, DY (vazio `-` em geral).
- Links apontam para `/fiinfras/<ticker>` (variação da página de detalhe específica FI-Infra).
- Cobertura aparentemente parcial vs. mercado real (~80 FI-Infra na B3) — validar antes de usar como fonte de universo.

### Lista `/fiagros`

- **51 cards** server-side, mesmo formato (`Fiagro: <segmento>`), links para `/fiagros/<ticker>`.
- Universo FI-Agro B3 ≈ 60–70; cobertura razoável mas também possivelmente parcial.

### Observação transversal

As listas não trazem métricas; para dataset rico o caminho é: lista `/funds` como seed → 1 request de detalhe por ticker (HTML + nonces) → 3 POSTs ajax por ticker (income/quotations/patrimonials). Para ~700 FIIs: ~2.800 requests por ciclo completo de ingestão — factível no Worker com throttle, mas pesado; alternar com fontes oficiais (B3/CVM) para séries históricas completas.

---

## (c) Anti-bot

- **Cloudflare presente** (`window.__CF`, script `cdn-cgi/...`), porém **sem challenge** nas rotas testadas: todos os GETs via `curl_cffi impersonate="chrome"` retornaram 200 de primeira, inclusive sem warm-up de cookies.
- Nenhum Akamai/PerimeterX detectado. Sem rate-limit explícito observado (15 requests espaçados, sem 429).
- `admin-ajax.php` aceitou POST **sem cookies e sem sessão** — apenas nonce + headers. Isso indica proteção leve; manter fingerprint Chrome e pacing educado mesmo assim.
- `/api/v1/*` retorna 500 — comportamento legado, não bloqueio de WAF (erro interno, não challenge).
- robots.txt não foi consultado (fora do orçamento cirúrgico); checar antes de produção.

---

## (d) Veredicto

**Viabilidade: ALTA** para ingestão Worker (local-first), com design defensivo.

Pontos fortes:
- Endpoints AJAX JSON estáveis e ricos: income mensal 10 anos, cotações diárias 5 anos, VP/cota mensal 10 anos — cobre backtest, DY e P/VP histórico.
- Sem auth real; nonce público no HTML; cookies dispensáveis; transporte Chrome resolve Cloudflare.
- Detalhe SSR denso (CNPJ, segmento ANBIMA, cotistas, liquidez) — bom para metadata de catálogo.
- Listas server-side servem de seed de universo.

Riscos / mitigação:
1. **Sem contrato formal** — endpoints WordPress internos podem mudar sem aviso (shape string-decimal, `quotations` double-encoded, datas `dd/mm/yy`). Mitigar com parser tolerante + testes de contrato + alerta de drift.
2. **Nonce acoplado ao HTML** — exige 1 GET de detalhe antes dos POSTs; se o nonce expirar entre fetch e POST (12–24h), refazer. Não paralelizar agressivamente.
3. **Cloudflare pode endurecer** — qualquer mudança de modo de segurança derruba curl_cffi; manter fallback (Playwright) fora do caminho crítico.
4. **Janela de cotações fixa em ~5 anos** — histórico mais antigo deve vir de outra fonte (B3/CVM), FundsExplorer não serve série completa.
5. **Cobertura FI-Infra/FI-Agro parcial** nas listas (30/51) — cruzar com CVM/B3 para universo completo.
6. **Ética/legalidade**: dados públicos não autenticados, mas ToS do site pode proibir scraping; ingestão de baixa frequência (diária), cache agressivo em Postgres/Redis conforme `PROVIDERS_SYNC.md`, identificar User-Agent honesto se possível.

Artefatos salvos: `/tmp/opencode/fe-pagina.html` (detalhe KNCR11), `fe-list-{funds,fiinfras,fiagros}.html`, `fe-single.js` (bundle do tema), `fe-{income,quotations,patrimonials}.json` (respostas completas KNCR11), `recon-fundsexplorer.md` (este relatório).
