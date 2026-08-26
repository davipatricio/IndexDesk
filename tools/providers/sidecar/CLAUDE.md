# tools/providers/sidecar — Instruções scoped

> Suplemento scoped ao [`CLAUDE.md`](../../../CLAUDE.md) raiz. A **referência do contrato** entre o
> processo Python e o Worker .NET é o [`README.md`](README.md): NDJSON v1, envelopes de erro,
> flags de cada comando, modo `--fixture` e limitações conhecidas. **Este documento não duplica o
> contrato** — cobre as convenções locais de quem vai mexer no código da pasta.
>
> Mudou comando, contrato ou pin de dependência? Atualize o `README.md` desta pasta **e**, se a mudança
> afeta features consumidas pelo backend, o repo skill
> `.agents/skills/indexdesk/references/process/current-features.md` (ver seção "Repo Skill" do CLAUDE.md raiz).

## 1. O que é

Processo Python auxiliar (gerenciado por **uv**, Python ≥ 3.12) spawnado pelo `IndexDesk.Worker` (.NET)
para buscar dados de **Yahoo Finance** (`yfinance`), **TradingView** (`tv-scraper`), **InfoMoney**
(`curl_cffi`) e do **catálogo oficial da B3** (`sistemaswebb3-listados`, empresas/FIIs), além de um
transporte HTTP genérico WAF-safe (`fetch`). Existe porque essas libs carregam
stacks anti-bloqueio (TLS impersonation, sessão websocket) impraticáveis de replicar em C#. A fronteira
entre os dois processos é o NDJSON versionado do stdout — nada de estado compartilhado.

## 2. Layout e responsabilidade por arquivo

```text
src/sidecar/
├── cli.py      árvore argparse + contrato de processo (exit codes, envelopes)
├── errors.py   taxonomia SidecarError (code + exit code + detalhes do envelope)
├── ndjson.py   disciplina stdout/stderr, leitor de fixtures, emissores de dado/log/erro
├── schema.py   schemas quote/dividend + validação
├── symbols.py  normalização de tickers (.SA, BMFBOVESPA:) — único lugar que conhece sufixos
├── yf_cmd.py   wrappers yfinance (quotes, dividends)
├── tv_cmd.py   tv-scraper + caminhada chunked/retry/dedupe do histórico
├── im_cmd.py   API InfoMoney (curl_cffi chrome, paginação daily/dividends)
├── b3_cmd.py   catálogo oficial B3 (empresas/FIIs, curl_cffi chrome + warm-up de sessão)
└── fetch_cmd.py fetch genérico curl_cffi (body cru no stdout; transporte WAF-safe)
tests/          suíte pytest offline (fixtures, schema, símbolos, chunks, CLI)
```

Convenções de módulo:

- Cada comando vive num `*_cmd.py` fino: parsing já foi feito pelo `cli.py`, o módulo só orquestra a
  lib e emite registros via `ndjson.py`.
- Normalização de símbolo acontece **antes** de qualquer chamada externa e só dentro de `symbols.py` —
  nunca espalhe regex de ticker pelos comandos.
- Todo erro passa pela taxonomia de `errors.py` (`Usage.Invalid`, `Fetch.Failed`, `Parse.Invalid`,
  `Scrape.WafBlocked`, `Scrape.AuthFailed`). Nunca faça `sys.exit()` direto nem imprima envelope à mão.
- **stdout é sagrado**: só linhas NDJSON de dados. Logs, warnings e o envelope de erro vão para stderr.
  Qualquer `print` acidental no stdout corrompe o parse do lado C#.

## 3. Comandos e ciclo de execução

```bash
cd tools/providers/sidecar
uv sync                                  # cria .venv + uv.lock dos pins
uv run sidecar yf quotes --symbol PETR4.SA --start 2026-01-01
uv run sidecar tv history --symbol BMFBOVESPA:BOVA11 --bars 2500
uv run sidecar im quotes --symbol MGLU3 --bars 500
uv run sidecar fetch --url "https://exemplo.test/pagina/" --timeout-s 20
```

Ciclo de todo comando NDJSON (o `fetch` é a exceção documentada — body cru no stdout):

1. `cli.py` valida argv → erro de uso sai como envelope `Usage.Invalid`, exit `2`.
2. O módulo busca os dados; falha de rede/bloqueio/auth vira envelope `Fetch.Failed`,
   `Scrape.WafBlocked` (403) ou `Scrape.AuthFailed` (401), exit `3`.
3. Cada registro validado contra `schema.py` é emitido no stdout; saída **vazia com exit 0 é válida**
   (ausência de dado ≠ erro).
4. Linha interna inválida → `Parse.Invalid`, exit `4`.

Exit codes na íntegra: `0` ok · `2` uso · `3` fetch · `4` parse.

## 4. Padrão de testes (offline, transportes falsos)

A suíte inteira roda sem rede (`uv run pytest`, < 1 s). O padrão é substituir **apenas a borda de
transporte** por fakes tipados — a lógica de negócio (chunking, dedupe, paginação, mapeamento de campos)
roda de verdade:

- `tv_cmd` define o `Protocol` `CandleSource`; os testes injetam streamers falsos que simulam janela
  cumulativa, timeouts tipo `WebSocketTimeoutException` e envelopes `status="failed"`
  (ver `tests/test_tv_chunks.py`).
- `im_cmd`/`fetch_cmd` recebem a função de transporte injetável; os testes usam um `Recorder` que captura
  url/headers/params e devolve respostas enlatadas, inclusive status 403/401 para exercitar os códigos
  `Scrape.*` (ver `tests/test_im_cmd.py`, `tests/test_fetch_cmd.py`).
- `helpers.py` fábrica registros (`quote`, `dividend`) e grava arquivos de fixture; o teste
  `test_fixture_cli.py` garante roundtrip byte-a-byte do modo `--fixture`.

Regras ao adicionar teste: nenhum acesso a rede, nenhuma chave real, nenhum sleep longo (o retry usa
backoff injetável — passe um `sleep` fake em vez de esperar). Novo comando novo transporte ⇒ defina
Protocol/injeção primeiro, depois o teste.

## 5. Chunking do TradingView (por que existe e como manter)

Payload grande de candles é flaky upstream (medido na Fase 0: BOVA11 ≥ 4500 bars falhava 3/3).
O `tv_cmd` portanto **nunca pede uma janela enorme de uma vez**:

- Caminha de trás pra frente em passos crescentes de ~1000 bars (1000 → 2000 → …), porque a lib não tem
  API de offset — cada passo pede uma janela cumulativa maior.
- Cap duro de 5000 candles por chamada (limite da própria lib); retry por passo até 3 tentativas com
  backoff curto (~1 s).
- Dedupe por `timestamp` entre passos (`merge_batch`); parada quando atingiu `--bars` ou quando um passo
  não traz linha nova.
- Resultado parcial abaixo de `--bars` é emitido mesmo assim (warning no stderr) porque os upserts
  downstream são idempotentes; zero candles é exit `3`.

Constantes do algoritmo (`CHUNK_SIZE`, `MAX_PER_CALL`, `MAX_ATTEMPTS`, `BACKOFF_SECONDS`) vivem no topo
de `tv_cmd.py` — mude-as apenas com evidência medida, registrando o número no plano/README.

## 6. Credenciais: argv/env, nunca log nem commit

- **TV cookie**: chega como flag `--cookie` no processo filho, injetada pelo runner C# a partir de
  `Providers__TradingView__Cookie` (.env). Sessão anônima funciona para dados B3; cookie autenticado só
  amplia janelas. Expirou → client reporta `TradingView.AuthFailed`.
- **InfoMoney key**: OPCIONAL. Se `INFOMONEY_SUBSCRIPTION_KEY` vier no env
  (injetada pelo runner a partir de `Providers__InfoMoney__SubscriptionKeys__0`),
  ela tem precedência; sem chave, o sidecar **descobre sozinha** a key pública do
  frontend embutida no HTML da página de cotação (`window.InfoMoneyPage`,
  warm-up obrigatório — mesma resposta traz cookie e chave). Nunca é flag argv,
  nunca aparece em log.
- Regra geral: o sidecar não lê `.env` próprio nem conhece nomes de config do .NET — quem injeta é o
  runner. Nada de credencial hardcoded, default ou exemplo com valor real nesta pasta.

## 7. Política de pins

Deps fixadas exatas em `pyproject.toml` (versões provadas juntas no spike da Fase 0):

| Pacote | Pin | Quando mexer |
| :--- | :--- | :--- |
| `yfinance` | `==1.6.0` | Só quando Yahoo bloquear e a versão nova comprovar correção (stack de cookie/crumb/TLS quebra com frequência upstream) |
| `tv-scraper` | `==1.5.1` | Fork mantido; API mudou radicalmente pré-1.5 |
| `curl-cffi` | `==0.16.1` | É o músculo anti-TLS-fingerprinting sob yfinance/im/fetch |

Fluxo obrigatório após qualquer mudança de pin: `uv sync` → `uv run pytest` → **um smoke real por
provider** → só então commitar o `uv.lock` novo junto com a justificativa.

## 8. Integração com o Worker (.NET)

Quem spawna é o `SidecarProcessRunner`
(`apps/backend/src/Modules/IndexDesk.Modules.MarketData/Clients/SidecarProcessRunner.cs`):
monta argv via `ArgumentList` (sem shell), timeout configurável (`Providers__Sidecar__TimeoutSeconds`,
default 120 s, kill da árvore), lê stdout linha a linha e trata linha `{` com `"error"` no stderr como
envelope de falha; exit 2/3/4 mapeiam para `Sidecar.Usage/FetchFailed/ParseError`. Os clients
(`YfinanceSidecarClient`, `TradingViewSidecarClient`, `InfoMoneySidecarClient`) e o transporte HTTP
(`ISidecarHttp`/`SidecarHttp`) consomem esse runner — o sidecar Python não sabe que eles existem.

```mermaid
flowchart LR
    A["Job Quartz<br>(IndexDesk.Worker)"] --> B["SidecarProcessRunner<br>spawn + timeout 120 s"]
    B -->|spawn uv run sidecar| C["processo Python<br>tools/providers/sidecar"]
    C -->|stdout NDJSON| D["parse linha a linha<br>NormalizedQuote / NormalizedDividend"]
    C -->|stderr logs + envelope de erro| B
```
