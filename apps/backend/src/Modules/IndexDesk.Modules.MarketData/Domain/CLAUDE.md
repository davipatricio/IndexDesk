# MarketData/Domain — Records e DTOs normalizados (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md). Este doc detalha o que o mapa de pastas do módulo resume para `Domain/`.

## Responsabilidade

Tipos de dados compartilhados do módulo: entidades de catálogo, records normalizados que atravessam a
fronteira provider→persistência, e DTOs que definem o contrato HTTP. Pasta sem lógica: nada de I/O,
serviço ou regra — só forma.

## Inventário

| Arquivo | Conteúdo |
| :--- | :--- |
| `Asset.cs` | Entidade EF `Asset` (ticker, CNPJ, gestor, `AssetCategory` Etf/Bdr/Stock/Index/Fii, `AssetClass`, fees, benchmark default `IBOV`, curadoria via `IsActive`) + enums `AssetClass`/`AssetCategory` + records `QuoteItem`/`AssetDto`. |
| `NormalizedQuote.cs` | `readonly record struct` OHLCV normalizado: `AdjClose` obrigatório, `TradesCount` opcional, `SourceProvider` + `FetchedAtUtc` para auditoria de origem. |
| `NormalizedDividend.cs` | Evento de provento normalizado: `ComDate`, `PaymentDate?`, `Rate`, `DividendType` (vocabulário B3 cru), `Currency`, `SourceProvider`. |
| `NormalizedFxRate.cs` | Snapshot FX AwesomeAPI: bid/ask (sem volume); **`Bid` é o proxy de close** para conversões de backtest; timestamp em epoch-seconds. |
| `AssetQueryDtos.cs` | Contrato `/api/v1/assets`: `AssetSummaryDto` (catálogo/screener), `AssetDetailDto` + `AssetQuoteStatsDto` + `FiscalProfileDto` ("raio-x" fiscal), `AssetRankingDto` (`MetricValue` espelha a coluna de ordenação), `QuoteSeriesDto`, `PerformanceResponseDto`/`BenchmarkReturnDto`, `MarketIndicatorDto`, `QuoteSparkPointDto`, `AssetDividendsDto`/`AssetDividendEventDto`, `AssetQuotesBatchItemDto`, `MacroRateSeriesDto`/`MacroRatePointDto`. |
| `ProviderHealthDtos.cs` | Contrato `/api/v1/providers/health`: `ProviderHealthResponseDto` (+`Summary`, status global UNKNOWN/HEALTHY/DEGRADED/UNHEALTHY), `ProviderHealthDto` (status por provider NEVER_SYNCED/…), `ProviderKeyHealthDto`, `ProviderLastSyncDto`, `ProviderTotalsDto`, `SyncIssueDto`. |

## Regras locais

- **Records imutáveis** na superfície de API e nos tipos normalizados (`readonly record struct`
  quando pequenos e quentes). Entidade mutável só `Asset` (EF change tracking).
- `AdjClose` é campo **obrigatório** do contrato normalizado; fontes sem ajuste repetem `close`
  na camada de client — Domain nunca trata "ausente".
- Provento com data única no contrato sidecar → `PaymentDate = ComDate` é decidido em
  `Clients/SidecarNdjson`; Domain apenas carrega o par nullable.
- DTOs de health expõem chaves do pool **apenas por índice** (`ProviderKeyHealthDto.KeyIndex`) —
  nenhum tipo aqui carrega valor de chave/token/cookie.
- Mudança de shape de DTO = mudança de contrato HTTP: atualizar endpoint em `MarketDataModuleExtensions`,
  testes de integração e o client tipado do frontend na mesma PR.
- Novo tipo de dados entra aqui somente se for compartilhado entre ≥ 2 pastas do módulo
  (clients, ingestion, services, health); tipo local de um serviço fica junto dele.
