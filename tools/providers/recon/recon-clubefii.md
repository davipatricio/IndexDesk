# Recon clubefii.com.br — FIIs + APIs internas

Data: 2026-08-26 · Read-only · Transporte: sidecar curl_cffi `impersonate="chrome"` · ~10 requests no alvo

## a) Fontes de dado encontradas

### Stack
ASP.NET WebForms/MVC (fragments `.aspx`-like, erro "Runtime Error" padrão IIS/Verdana), jQuery no front. **Não é WordPress** — zero `wp-json`, zero `admin-ajax.php`. AJAX same-origin sob `/` retornando **HTML fragment** (não JSON), injetado em `#conteudo_principal`.

### Página de detalhe — `https://www.clubefii.com.br/fiis/{TICKER}` (ex.: `/fiis/MXRF11`)
Shell server-side (165 KB): título, metas SEO por aba, e globals JS:

```js
window.cod_fii_numerico = '61';      // id interno do fundo
window.query_cod_neg    = 'MXRF11';
window.fiiLiberado      = 'False';   // False p/ anônimo
window.val_ipo          = 10;
```

Nenhum dado tabular server-side no shell (sem `__NEXT_DATA__`; único JSON-LD = BreadcrumbList na lista). Todo conteúdo carrega via XHR. Endpoints mapeados nos scripts da página:

| Endpoint | Auth | Observado |
| :--- | :--- | :--- |
| `/fundo_basico?cod={TICKER}&fiiLiberado=False&fiiLiberadoLogado=False` | **PÚBLICO** | 200, ~87 KB |
| `/fundo_cotacao?cod={TICKER}&...` | **PÚBLICO** | 200, ~783 KB |
| `/ajax/imoveis-de-fundos-imobiliarios?cod_neg={TICKER}` | PÚBLICO | 200, tabela de imóveis (vazio p/ MXRF11 — fundo de recebíveis) |
| `/pega_cotacao` (POST) | PÚBLICO? | cotação/data-hora atuais (`data.split(';')`) — não testado |
| `/fundo_proventos?cod={TICKER}&...` | **LOGIN-GATED** | 200 mas corpo = "Você precisa estar logado para acessar esta sessão" |
| `/fundo_vacancia?cod={TICKER}&...` | LOGIN-GATED | idem |
| Demais (não testados, mesmíssimo padrão): `fundo_valor_patrimonial`, `fundo_passivo`, `fundo_cotistas`, `fundo_liquidez`, `fundo_caixa`, `fundo_assembleias`, `fundo_relatorios`, `fundo_imoveis`… | provável gate | URLs extraídas do JS |

**Shape `/fundo_basico` (MXRF11, exemplo truncado):**
```
SEGMENTO Recebíveis Imobiliários | DY último/12m 1,08% e 12,95% | P/VP 1,00 |
GESTOR XP VISTA | ALAVANCAGEM 2,11% | Liquidez média diária 30d R$ 15.922.694 |
Nº COTISTAS 1.509.087 | % IFIX 3,52% | VALOR PATRIMONIAL R$ 5.253.747.540 |
DATA IPO 20/09/2011 | VALOR IPO R$ 10,00 | QTD COTAS 567.206.273 |
CNPJ 97.521.225/0001-25 | ADMINISTRADOR BTG PACTUAL | AUDITOR ERNST & YOUNG |
CUSTÓDIA BTG PACTUAL | TIPO GESTÃO ATIVA | ESCRITURAÇÃO BTG PACTUAL |
taxa adm ano: R$ 36.966.072,08 (0,86% PL contábil) | VPA R$ 9,26 | cota R$ 9,23 |
YIELD 1m/3m/6m/12m: 1,08/3,25/6,45/12,95%
```
HTML fragment com classes próprias — parsing por rótulos de texto (ex.: `BeautifulSoup` buscando label→valor), não JSON.

**Shape `/fundo_cotacao`:** histórico completo desde o IPO — anos 2011–2026 na tabela de rentabilidade mensal + ~370 pontos para gráfico (Highcharts, arrays inline no fragment).

### Lista — `https://www.clubefii.com.br/fundo_imobiliario_lista`
Uma página só, **server-side renderizada**, sem paginação: **825 FIIs** (837 `<tr>`), links `href="/fiis/{TICKER}"`. Colunas por linha:

`CÓDIGO | NOME | VALOR COTA (+var% +timestamp ex.: "25/08/2026 17:05:00") | DATA IPO | VALOR IPO | SEGMENTO | ADMINISTRADOR | RELATÓRIOS | FEED`

Exemplo (MXRF11): `MXRF11 | Maxi Renda | 9,23 | +0,32% | 25/08/2026 17:05:00 | 20/09/2011 | R$ 10,00 | Recebíveis Imobiliários | BTG PACTUAL`.

**Não há DY/P/VP/PL/liquidez na lista** — só na página de detalhe por fundo.

## b) Cobertura

| Dado | Cobertura |
| :--- | :--- |
| Catálogo completo (ticker, nome, segmento, adm, IPO) | ✅ 825 FIIs numa request |
| Cotação atual + variação + timestamp | ✅ lista (todas de uma vez) |
| Histórico de cotações mensal desde IPO | ✅ público, por fundo (`fundo_cotacao`) |
| Fundamentais por fundo (DY agregados, P/VP, liquidez 30d, cotistas, VP, taxa adm, CNPJ, auditor, custódia, gestor) | ✅ público (`fundo_basico`) |
| **Histórico de rendimentos (valor/cota, data-com/pagamento, meses)** | ❌ **login-gated** (`fundo_proventos`). Público só DY agregado 1/3/6/12m |
| Vacância, cotistas detalhados, passivo, valor patrimonial série | ❌ gated (padrão idêntico) |

## c) Anti-bot observado

- **Nenhum desafio**: todas as requests 200 com `curl_cffi chrome` + sessão com cookies (warm-up na página de detalhe antes dos XHR).
- Sem Cloudflare turnstile/challenge visível nas respostas; GTM/server-side tagging apenas (`sgtm.clubefii.com.br`).
- Fragilidade: params faltando dão **HTTP 500 Runtime Error** genérico (IIS) — ex.: omitir `fiiLiberadoLogado` em `/fundo_proventos`. `cod` aceita ticker OU id numérico (61 = MXRF11), mas ticker retornou payload cheio e numérico retornou parcial errado — usar **ticker**.
- Gate de conteúdo é server-side por flag `fiiLiberado/fiiLiberadoLogado` — não há dado escondido no HTML público esperando parse.

## d) Veredicto

**Viabilidade: MÉDIO-ALTO** para catálogo + fundamentais + histórico de cotações; **BAIXO** para séries históricas de rendimentos sem conta.

- Ingestão viável hoje: lista (1 req) → para cada FII `/fundo_basico` + `/fundo_cotacao` (~2 req/FII; 825 FIIs ≈ 1.651 reqs — respeitar throttle, fragmentos grandes).
- **Riscos**
  1. Rendimentos históricos (dado central p/ IndexDesk) atrás de login gratuito — exigiria cadastro+sessão (ToS/robôs: avaliar antes).
  2. Fragmentos HTML sem contrato estável (classes/rótulos podem mudar sem aviso; sem versionamento de API).
  3. 500 silencioso em params errados — parser precisa validar payload mínimo.
  4. Site pequeno/médio — rate limit agressivo pode derrubar disponibilidade; sem documentação pública dos endpoints.
- Alternativa complementar p/ rendimentos: dados oficiais CVM/B3 (já no escopo do repo) — ClubeFII ficaria como fonte de fundamentais agregadas (liquidez média, nº cotistas, % IFIX, taxa adm) que CVM não expõe pronta.

### Artefatos
`/tmp/opencode/cf-mxrf11.html` (shell), `cf-lista.html` (lista completa), `cf-basico2.html` (fundo_basico MXRF11), `cf-cotacao.html` (histórico cotações), `cf-proventos.html`/`cf-vacancia.html` (gated), `cf-basico.txt`/`cf-imoveis.html`.
