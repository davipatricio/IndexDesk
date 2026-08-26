# Recon br.advfn.com — ETFHub B3 (2026-08-26)

Transporte: sidecar `curl_cffi` (`impersonate="chrome"`), sessão com warm-up na home, delay 1.5–2s. **16 requests** totais, todos **HTTP 200**, zero challenges.

## (a) Endpoints / fontes descobertas

### A1. Página de cotação (server-side parcial)
```
GET https://br.advfn.com/bolsa-de-valores/bovespa/{slug}-{TICKER}/cotacao
```
- Entrega estática: **Último Preço**, **Preço Anterior**, fundamentais (P/L, Dividend Yield, Market Cap, Setor/Indústria, Beta), nome/símbolo.
- JSON-LD embutido: `BreadcrumbList` + `FAQPage` ("O preço atual ... é R$ 41,35", range 52 semanas). Único "estado inline" útil — não há `quoteData`/`window.*` de cotação.
- **OHLC do dia / volume / book / negócios = placeholders vazios** preenchidos por streaming:
  ```html
  data-asc-symbol="BOV^PETR4" data-asc-param="OPEN_PRICE|CUR_PRICE|VOLUME|BID_PRICE|OFFER_PRICE|CHANGE_PC|TRADE_SIZE..."
  data-asc-render="$AFNStreamingRenderer.renderPrice"
  ```
  Alimentados por WebSocket `wss://streamws-lo.advfn.com` (client: `/lib/streamer-client/396ff94/streamer-client.bundle.js`). Sem login = dados atrasados (CTAs `trades_delayed`, `delayed_indicator` nas páginas).

### A2. Slug
- URL curta `/bovespa/PETR4/cotacao` → **200 com redirect server-side** para canônica `/bovespa/petrobras-pn-PETR4/cotacao`. Ticker puro basta; slug não precisa ser conhecido a priori.

### A3. Histórico OHLCV — ❌ não acessível em HTML/endpoint público encontrado
```
GET .../{slug}/historico   # 200, mas tabela é client-side (ag-grid)
```
- Zero datas/linhas no HTML (`\d{2}/\d{2}/\d{4}` = 0 matches). Endpoint XHR do grid **não aparece** no HTML nem em `PageQuoteComponents.bundle.js` nem no generatedJS `_Common`.
- Mesmo comportamento no layout antigo `uk.advfn.com/stock-market/bovespa/PETR4/share-price-history` (também vazio, client-side).
- Ferramenta de download: `GET /ferramentas/bovespa-historico/download` → **redirect para landing paga** `/ferramentas-de-investimento/data-downloads`. CSV = feature paga/login. reCAPTCHA v3 na página de histórico.

### A4. Proventos — ✅ melhor achado
```
GET https://br.advfn.com/bolsa-de-valores/bovespa/{slug}-{TICKER}/balanco/dividendos
```
- Tabela **100% server-rendered, gratuita, histórico completo**: colunas `Data-Ex | Valor | Data Registrada | Data de Pagamento`.
- PETR4: JCP+dividendos desde ~2025 visíveis na amostra (múltiplas linhas por data-ex).
- KNCR11 (FII): **120 linhas** de rendimentos mensais. Ex.: `[03 Ago 2026, R$1,2500, 31 Jul 2026, 13 Ago 2026]`.

### A5. Outros
- Chart estático (imagem PNG): `/p.php?pid=staticchart&s=BOV:PETR4&p=5&t=52` — não é dado.
- Times&sales `.../{slug}/negocios`: client-side + gating (`cta_element_trades_delayed` / `trades_upgrade`). Inútil sem conta.
- Símbolo interno de mercado: `BOV:{TICKER}` / path Datalayer `/exchanges/historical/BOV/PETR4`.

## (b) Cobertura por classe

| Classe | Tick | Preço static | Prev close | OHLC/vol dia | Fundamentais | Proventos |
|---|---|---|---|---|---|---|
| Ação PN | PETR4 | ✅ 41,35 | ✅ | ❌ (WS) | ✅ P/L 4,28, DY 7,22%, mktcap | ✅ server-side |
| FII | KNCR11 | ✅ 107,08 | ✅ | ❌ (WS) | ✅ mktcap, DY 13,36% | ✅ 120 linhas |
| ETF | WRLD11 | ✅ 149,98 | ✅ | ❌ (WS) | ⚠️ leve (JSON-LD: price + 52w) | n/t |
| BDR | BIJS39 | ✅ 89,46 | ✅ | ❌ (WS) | ⚠️ leve (JSON-LD: price + 52w) | n/t |
| Índice | IBOV | ✅ 174.576,80 | ✅ | ❌ placeholders `0,00` | n/a | n/a |

Padrão de página idêntico entre classes (mesmo template `key-stats-item`), só varia conteúdo de fundamentais.

## (c) Anti-bot observado
- **Nenhum bloqueio nesta sessão**: 16/16 requests `status=200`, sem CAPTCHA intersticial, sem 403/429. Chrome impersonation + warm-up home + cookies de sessão + ≥1.5s entre requests.
- Presentes mas passivos: Cloudflare beacon (`window.__CF`, `cloudflareinsights`), reCAPTCHA v3 (forms), redirect canônico por slug.
- Riscos residuais: sem teste de volume/concorrência; ADVFN historicamente aperta rate-limit e serve dados **atrasados (15–20 min)** sem conta; streaming WS exige auth para tempo real.

## (d) Veredito: viabilidade **MÉDIO-BAIXO** como fonte primária; **ALTO** como fonte secundária de proventos

- **OHLCV histórico (requisito principal): não obtém** por HTTP simples — grid client-side, download CSV atrás de paywall/login, times&sales gated. Obter exigiria reverter o WS/XHR privado (frágil, ToS-hostil) ou conta.
- **Proventos: excelente** — server-rendered, grátis, histórico completo, cobre ações/FII/ETF-BDR pelo mesmo padrão de URL. Bom complemento para validar CVM/proventos que já ingerimos.
- **Preço de fechamento anterior**: trivialmente extraível (static + JSON-LD) — serve como cross-check do Yahoo.
- **Vs Yahoo (já temos)**: Yahoo dá OHLCV histórico completo via API gratuita; ADVFN perde feio nesse eixo. Vantagem única ADVFN = granularidade de proventos BR (data registrada/pagamento separadas) e cobertura local B3.
- **Riscos**: ToS/scraping explícito proibido; Cloudflare pode mudar postura; dados atrasados; dependência de layout PT-BR (`key-stats-item`) frágil a redesign.

**Recomendação**: não adotar para série histórica. Adotar (opcional, baixa prioridade) `GET /bolsa-de-valores/bovespa/{ticker}/balanco/dividendos` como fonte secundária de proventos com parser de tabela simples — ticker puro na URL resolve via redirect canônico.

---
Artefatos brutos em `/tmp/opencode/adv-*.html`, bundles em `/tmp/opencode/adv-{PageQuote,common}.js`. Repo intocado.
