# HARs — Fase 0.5

**7 HARs capturados em 2026-08-23** com Playwright (Chromium) headless/headed neste WSL,
após instalação das deps de sistema via sudo (`playwright install-deps chromium`).

## Arquivos (gzip por excederem ~5 MB)

| Arquivo | Página navegada | Entradas |
| :--- | :--- | :--- |
| `infomoney_mglu3_2026-08-23.har.gz` | `infomoney.com.br/cotibovacoes/mglu3/` | 157 |
| `xp_xina11_2026-08-23.har.gz` | `xpasset.com.br/fundos-etfs/xina11/` | 81 |
| `itnow_bovv11_2026-08-23.har.gz` | `itnow.com.br/bovv11/composicao/` | 76 |
| `investing_bova11_2026-08-23.har.gz` | `br.investing.com/indices/bovespa` | 520 |
| `invesco_qqq_us_2026-08-23.har.gz` | `invesco.com/us/financial-products/etfs/product-detail?...ticker=QQQ` (redireciona p/ `qqq-etf/en/home.html`) | 180 |
| `blackrock_br_productlist_2026-08-23.har.gz` | `blackrock.com/br/products/product-list` | 65 |
| `blackrock_bova11_2026-08-23.har.gz` | produto BOVA11 `blackrock.com/br/products/251816/ishares-ibovespa-fundo-de-ndice-fund` | 77 |

Análise dos endpoints extraídos dos HARs: ver as subseções "HAR (2026-08-23)" em
[`../recon.md`](../recon.md).

## Redação (segredos)

Antes de salvar aqui, cada HAR passou por redação automatizada
(`/tmp/opencode/recon2/redact_hars.py`): valores de headers `ocp-apim-subscription-key`
e ocorrências da chave marketdata do InfoMoney em bodies/postData foram substituídos por
`9d461117...REDACTED...e4695`. Cookies de sessão permanecem como capturados (sessões
anônimas descartáveis). Re-checar leaks antes de commit se capturar novos HARs.

## Convenção de captura (como reproduzir)

- Um **contexto novo por site** (`record_har_path`, modo full), viewport 1366×900, `locale=pt-BR`.
- Navegação `wait_until="domcontentloaded"` + espera ≥10 s (+ scroll para lazy-load).
- **Importante**: XP Asset, It Now e Investing.com **bloqueiam Chromium headless**
  (403 "Acesso Bloqueado" — UA `HeadlessChrome` é flagrado). Capturar **headed sob xvfb**:
  `xvfb-run -a python capture.py` com `launch(headless=False)`. InfoMoney/BlackRock/Invesco-US
  funcionam até headless.
- Scripts usados nesta execução (scratch): `/tmp/opencode/recon2/{capture_hars,capture_headed,redact_hars,replay_har}.py`
  (venv: `/tmp/opencode/fase05/.venv`, Playwright + curl_cffi). Resumo por HAR:
  `/tmp/opencode/recon2/har_summary.json`.
- Replay de validação pós-HAR: curl_cffi `impersonate="chrome"` — resultados registrados no recon.md.
