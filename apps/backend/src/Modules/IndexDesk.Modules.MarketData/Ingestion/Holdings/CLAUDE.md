# MarketData/Ingestion/Holdings — Feeds & parsers das gestoras (scoped)

> Companion to [`../../CLAUDE.md`](../../CLAUDE.md). Este doc detalha o que o mapa de pastas resume para `Ingestion/Holdings/`.

## Responsabilidade

Baixar e parsear as carteiras (composições) publicadas pelas gestoras — iShares, SPDR, It Now, Investo —
e persistir em `etf_holdings`. Job: `HoldingsWeeklySyncJob`, sábados 08:00 UTC, via
`IEtfHoldingsSyncService.SyncWeeklyAsync`.

## Inventário

| Arquivo | Conteúdo |
| :--- | :--- |
| `IEtfHoldingsSyncService.cs` | Contrato do sync semanal + summaries (`HoldingsSyncSummary`, `HoldingsSourceSummary`). |
| `EtfHoldingsSyncService.cs` | Orquestra os 4 feeds; tickers e mapeamento de páginas iShares vêm de config (`Providers:Holdings:*`); cada fonte gera summary próprio. |
| `HoldingsFeeds.cs` | `ISharesHoldingsFeed` (extrai link ajax do CSV da página do produto — hash rotaciona, href absoluto ou relativo), `SpdrHoldingsFeed` (`holdings-daily-us-en-{ticker}.xlsx`, ticker **minúsculo** case-sensitive), `ItNowHoldingsFeed` (JSON `history-api-json` primeiro; `fundCode` resolvido por regex `fundo=([A-Z0-9]{6,})` com seed `Providers:Holdings:ItNow:FundCodes:{TICKER}`), `InvestoHoldingsFeed` (HTML; publica nomes sem ticker). |
| `ISharesHoldingsParser.cs` / `SpdrHoldingsParser.cs` / `ItNowJsonParser.cs` | Parsers puros por formato (CSV / XLSX in-memory / JSON). |
| `HtmlCompositionParser.cs` | Parser HTML compartilhado (AngleSharp) para It Now e Investo, com distratores filtrados. |
| `HoldingsModels.cs` | `ParsedHolding` (linha crua pré-resolução) + `HoldingsLayoutException` (layout do feed quebrou). |

## Regras locais

- **Layout quebrado = `HoldingsLayoutException`**, não silent-skip: mudança silenciosa de layout é o
  modo de falha nº 1 deste tipo de scraper; a exceção aparece como FAILED/PARTIAL_WARNING no log.
- Peso pt-BR: **vírgula decimal já é pontos percentuais** (`1,5` = 1,5%); fração só dot-only começando
  com `0.` — bug real de regressão, coberto por testes.
- Upsert dedupe por `(etf_asset_id, as_of_date, holding_ticker)`; linha sem ticker dedupe por nome.
- Asset não curado = skip com warning — **nunca fabricar asset** a partir de holding.
- Transporte anti-WAF é opt-in por config: `Providers:Holdings:ItNow:Transport=sidecar|native`
  (default `sidecar`, via `Clients/SidecarHttp` — It Now faz TLS fingerprinting).
- Fixtures dos parsers ficam em `tests/IndexDesk.UnitTests/Fixtures/Holdings/` (CSV iShares, XLSX gerado
  in-memory, HTML live-sample com distratores, JSON It Now, HTML Investo). Testar parser novo = fixture nova lá.
- Feeds conhecem download+layout; parsers são puros e testáveis offline; persistência só no sync service.
