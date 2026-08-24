# MarketData/Services — Leitura e saúde expostas via API (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md). Este doc detalha o que o mapa de pastas do módulo resume para `Services/`.

## Responsabilidade

Casca de leitura do módulo: consulta Postgres/Redis local e monta os DTOs de `Domain/` servidos pelos
endpoints `/api/v1/assets` e `/api/v1/providers`. Nenhum contato com provedor externo — request path
é estritamente local-first.

## Inventário

- `IAssetQueryService.cs` / `AssetQueryService.cs` — catálogo e séries:
  - `ListAsync` — catálogo paginado/screener (busca, tipo, moeda, ordenação);
  - `GetRankingsAsync` — ranking por métrica whitelistada (`sharpe`, `retorno12m`, volume…); ativos
    sem dados da métrica vão pro fim independente da direção;
  - `GetDetailAsync`, `GetQuotesAsync`, `GetQuotesBatchAsync` (sparklines batch; tickers upper-cased,
    dedup, desconhecidos simplesmente ausentes), `GetDividendsAsync` (eventos + trailing 12m),
    `GetPerformanceAsync` (retorno total/anualizado + benchmarks), `GetMarketIndicatorsAsync`
    (CDI/Selic/IPCA + acumulado 12m), `GetMacroRateSeriesAsync` (janelas cruas p/ curvas client-side).
  - Matemática delegada a `Calculators/PerformanceCalculators` e `Calculators/DividendCalculators`.
- `IProviderHealthService.cs` / `ProviderHealthService.cs` — endpoint `GET /api/v1/providers/health`:
  carrega `SyncJobLogs` (`AsNoTracking`), junta `IApiKeyPool.Snapshot()` agrupado por provider e
  delega toda a derivação a `Health/ProviderHealthAggregator` (status global + summary + issues).

## Regras locais

- Cache via `ICacheService.GetOrCreateAsync`: dividends ~30 min, macro-séries 24 h; resultado vazio
  de macro **não** é cacheado (evita cristalizar outage). Respeitar TTLs ao tocar aqui.
- Serviços scoped; queries sempre `AsNoTracking` quando só leitura.
- Ticker desconhecido → `null`/lista vazia conforme contrato; nunca lançar exceção para "não achou".
- Nada de lógica numérica inline: nova fórmula entra em `Calculators/` com teste unitário.
- Métrica nova de ranking exige whitelist correspondente no endpoint (`RankingMetrics`) — não aceitar
  string arbitrária de ordenação.
- Health não deve virar heavy query: se `sync_job_logs` crescer demais, mover retenção/pruning para o
  Worker em vez de filtrar aqui.
