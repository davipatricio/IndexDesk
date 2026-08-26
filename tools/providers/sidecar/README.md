# IndexDesk Provider Sidecar

Python helper process spawned by `IndexDesk.Worker` (.NET) to fetch market data
from **Yahoo Finance** (`yfinance`) and **TradingView** (`tv-scraper`). The
contract between the two processes is **versioned NDJSON on stdout**; stderr
carries logs only. This exists because the Python libs inherit working
anti-blocking stacks (curl_cffi TLS impersonation for Yahoo, TradingView's
websocket session) that are impractical to replicate from C#.

## Contract v1 (NDJSON)

One JSON object per line on **stdout**. All logging/warnings go to **stderr**.
Errors print a single envelope line to **stderr** (never stdout), so the C#
side never confuses errors with data:

```json
{"error":{"code":"Fetch.Failed","message":"..."}}
```

Quote line (all three providers; TV and InfoMoney set `adj_close = close`
because their series are raw/unadjusted prices):

```json
{"ticker":"PETR4.SA","date":"2026-08-21","open":30.1,"high":31.0,"low":29.5,"close":30.7,"adj_close":30.7,"volume":12345600}
```

Dividend line (yfinance and im):

```json
{"ticker":"PETR4.SA","date":"2026-08-01","rate":0.52,"type":"DIVIDEND"}
```

Catalog lines (v2, `sidecar b3 ...` only):

```json
{"cnpj":"33000167000101","code_cvm":"12345","issuing_company":"PETROBRAS S.A.","trading_name":"PETROBRAS","market_indicator":"99","date_listing":"31/12/9999"}
```

```json
{"ticker":"HGLG","name":"CSHG LOGÍSTICA FII","fund_type":"FII"}
```

Rules:

- `date` is ISO-8601 `YYYY-MM-DD`; numeric fields are JSON numbers (never
  strings/bools/NaN).
- Catalog records (`company`/`fund`) carry only strings; `date_listing`
  stays verbatim B3 format (`dd/mm/yyyy`) — it is metadata, not a quote date.
- **Empty output is valid** (zero lines, exit 0) - e.g. BOVA11 has no
  dividends on Yahoo; that is data absence, not an error.
- Exit codes: `0` ok · `2` spawn/usage error · `3` fetch failure · `4` parse
  error.
- Error codes: `Usage.Invalid` (2), `Fetch.Failed` (3), `Parse.Invalid` (4),
  plus the fetch-specific codes `Scrape.WafBlocked` (403 - TLS fingerprint
  rejected by a WAF such as Akamai) and `Scrape.AuthFailed` (401 - bad
  subscription key), both exit 3. Envelopes may carry extra fields after
  `message` (e.g. `status`) — consumers key off `error.code`.

## Commands

```text
sidecar yf quotes    --symbol PETR4.SA [--start YYYY-MM-DD] [--end YYYY-MM-DD] [--fixture FILE]
sidecar yf dividends --symbol PETR4.SA [--fixture FILE]
sidecar tv history   --symbol BMFBOVESPA:BOVA11 [--interval 1d] [--bars N] [--cookie COOKIE] [--fixture FILE]
sidecar im quotes    --symbol MGLU3 [--bars N] [--fixture FILE]
sidecar im dividends --symbol MGLU3 [--fixture FILE]
sidecar b3 companies [--page-size N] [--max-records N] [--fixture FILE]
sidecar b3 fiis      [--type FII] [--page-size N] [--max-records N] [--fixture FILE]
sidecar fetch        --url URL [--method GET|POST] [--data BODY] [--header "K: V" ...] [--timeout-s N] [--b64]
```

Symbol normalization (built in):

- `yf`: bare B3 ticker gets `.SA` appended (`PETR4` becomes `PETR4.SA`);
  anything containing `.`, `^` or `=` passes through untouched
  (`^BVSP`, `USDBRL=X`, `GC=F` stay as-is).
- `tv`: bare ticker gets the `BMFBOVESPA:` prefix; explicit pairs
  (`NASDAQ:AAPL`) pass through.
- `im`: bare B3 tickers only (`MGLU3`); benchmarks/FX pairs have no
  equivalent on the API and are rejected as usage errors.

### InfoMoney (`im`) specifics

- Auth = header `ocp-apim-subscription-key` (public frontend key). Resolution
  order: `INFOMONEY_SUBSCRIPTION_KEY` env (injected by the .NET runner from
  `Providers__InfoMoney__SubscriptionKeys__0`) → **auto-discovered** from the
  quote page HTML, where the WordPress theme inlines
  `window.InfoMoneyPage.api_marketdata.ocp_apim_subscription_key`. Never an
  argv flag, never logged. Re-measured 26/08/2026: Akamai now rejects stateless
  Chrome-TLS calls to the API host too — a warm-up GET of
  `www.infomoney.com.br/mercados/acoes/petrobras-petr4/` (same session) is
  mandatory before any API call; that one response also carries the key, so
  bootstrap costs a single GET.
- Requests use `curl_cffi` with `impersonate="chrome"` because Akamai blocks
  non-browser TLS before validating the key.
- `im quotes` walks the paginated `b3/quotes/daily/{ticker}` endpoint
  (`Page`/`PageSize`/`Order=Desc`) until `--bars` rows or the last page;
  emits oldest-first with `adj_close = close` (series is unadjusted).
- `im dividends` reads `b3/corporate-events/cash-dividends/{ticker}`:
  `lastDatePriorToEx` becomes `date`, `rate` passes through (BRL per share)
  and `type` keeps the native B3 vocabulary (`DIVIDENDO`,
  `JRSCAPPROPRIO`, ... - relevant for IR withholding rules).

### B3 catalog (`b3`) specifics

Scrapes the official listed-company and FII catalogs behind
`www.b3.com.br/.../empresas-listadas.htm` and `.../fiis-listados/`. Those pages
are SPA shells embedding Angular apps at `sistemaswebb3-listados.b3.com.br`;
the scraper talks to their JSON proxies directly:

- `GET /listedCompaniesProxy/CompanyCall/GetInitialCompanies/{base64(filter)}`
  → ~3.5k companies (CNPJ, CVM code, trading name).
- `GET /fundsListedProxy/Search/GetListFunds/{base64(filter)}` with
  `typeFund: FII` → ~530 funds (ticker + full name). Other `typeFund`
  values come from the app's own `assets/funds.json` (FIDC, FIP, ...).

Akamai front-door requirements baked into the command: TLS must impersonate
Chrome **and** a warm-up GET of the app page must run first — without those
session cookies the proxy answers HTTP 200 with an empty body. No captcha
solving is attempted; a hard challenge surfaces as `Fetch.Failed`/403
`Scrape.WafBlocked`.

### Generic fetch (`fetch`) — WAF-safe raw HTTP

One-off transport for hosts that Akamai-style TLS-fingerprint non-browser
clients (measured 2026-08-23 on `www.itnow.com.br`: plain curl and .NET
`HttpClient` get 403 "Access Denied" even with full browser headers;
`curl_cffi impersonate="chrome"` gets 200). Unlike the NDJSON commands,
**stdout is the raw response body**: UTF-8 text by default, base64 of the
bytes with `--b64` (binary payloads such as XLSX). One trailing newline is
appended. stderr keeps the usual logs plus the error envelope on failure —
HTTP 403 maps to `Scrape.WafBlocked`, any other status >= 400 or a connection
error to `Fetch.Failed`, both exit 3, both carrying `"status"` in the
envelope when known.

```bash
# HTML page (It Now composition, fund-code extraction happens on the C# side)
uv run sidecar fetch --url "https://www.itnow.com.br/bovv11/composicao/" --timeout-s 20

# JSON POST (history-api-json holdings API)
uv run sidecar fetch --url "https://www.itnow.com.br/history-api-json/?type=composicoes-indices&fundo=BRBOVVCTF009" --method POST --timeout-s 20

# Binary XLSX via base64 (decode on the consumer side)
uv run sidecar fetch --url "https://x.test/f.xlsx" --b64
```

The .NET wrapper is `ISidecarHttp` / `SidecarHttp`
(`Modules/IndexDesk.Modules.MarketData/Clients/`) — feeds opt in per source
via config, e.g. `Providers:Holdings:ItNow:Transport=sidecar|native`
(default `sidecar`).

`--interval` accepts `1m 5m 15m 30m 1h 2h 4h 1d 1w 1M` (default `1d`).

### Fixture mode (`--fixture FILE`)

Reads a file of ready-made NDJSON lines, validates each against the schema
and emits them verbatim on stdout (exit 4 on any invalid line). Lets .NET
integration tests run without network:

```bash
uv run sidecar yf quotes --symbol PETR4.SA --fixture fixtures/petr4.ndjson
```

## Bootstrap (uv)

Requires [uv](https://docs.astral.sh/uv/) on PATH (Python >= 3.12 managed by uv):

```bash
cd tools/providers/sidecar
uv sync                 # create .venv + uv.lock from pinned deps
uv run sidecar --help
```

### How .NET invokes it

The worker spawns (timeout 120 s, see Fase 4) something equivalent to:

```bash
uv run --project tools/providers/sidecar sidecar yf quotes --symbol PETR4.SA --start 2026-01-01
uv run --project tools/providers/sidecar sidecar tv history --symbol BMFBOVESPA:BOVA11 --bars 5000
```

Read stdout line-by-line as NDJSON; treat any stderr line starting with `{`
containing `"error"` as a failure envelope; everything else on stderr is a log.

## Pinning policy

Deps are pinned to versions proven together in the Fase 0 spike
(CPython 3.13/3.14):

| Package | Pin | Note |
| :--- | :--- | :--- |
| `yfinance` | `==1.6.0` | Bump **consciously**: its cookie/crumb/TLS stack breaks often upstream; bump only when Yahoo blocks us and the new version demonstrably fixes it |
| `tv-scraper` | `==1.5.1` | Fork-maintained lib; API changed drastically before 1.5 (not TvDatafeed-style) |
| `curl-cffi` | `==0.16.1` | The actual anti-TLS-fingerprinting muscle underneath yfinance |

After any pin change: `uv sync`, `uv run pytest`, then one live smoke per
provider before committing the new `uv.lock`.

## Known limitations / design notes

- **TV big payloads are flaky.** Measured in Fase 0: `BOVA11` with
  `numb_candles >= 4500` failed 3/3 (`WebSocketTimeoutException`) while `IBOV`
  at 5000 passed. The CLI therefore never asks for one huge window: it walks
  backwards in growing chunks of ~1000 bars (the library has no offset API,
  so each step requests a larger cumulative window and dedupes by timestamp),
  retrying each call up to 3 times with small backoff. Partial results below
  `--bars` are emitted anyway (with a stderr warning) because downstream
  upserts are idempotent; zero candles is exit 3.
- **TV auth**: tv-scraper 1.5.1 authenticates via a session **cookie**
  (`--cookie`); there is no email/password login flow in this version. The
  anonymous session works for B3 data. Credentials, when used, come from
  `.env` only - never code or commits.
- **Yahoo ETF dividends are often empty** for national B3 ETFs (BOVA11 n=0);
  proventos for those tickers need Brapi/CVM instead.
- A long-lived `serve` mode (FastAPI) is deliberately out of scope until spawn
  cost per job proves annoying.

## Layout

```text
src/sidecar/
├── cli.py      argparse tree + process-level contract (exit codes, error envelopes)
├── errors.py   SidecarError taxonomy (code + exit code + envelope details)
├── fetch_cmd.py generic curl_cffi fetch (raw body on stdout; WAF-safe transport)
├── im_cmd.py   InfoMoney API wrapper (curl_cffi chrome, paginated daily/dividends)
├── ndjson.py   stdout/stderr discipline, fixture reader, error/log emitters
├── schema.py   quote/dividend schemas + validation
├── symbols.py  ticker normalization (.SA suffix, BMFBOVESPA prefix)
├── tv_cmd.py   tv-scraper wrapper + chunked/retried/deduped history walk
└── yf_cmd.py   yfinance wrappers (quotes, dividends)
tests/          offline pytest suite (fixtures, schema, symbols, chunk logic)
```
