# MarketData/Pipeline — Validação e fallback entre providers (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md). Este doc detalha o que o mapa de pastas do módulo resume para `Pipeline/`.

## Responsabilidade

Qualidade de dados e failover genérico: sanitiza barras/proventos vindos de qualquer fonte e caminha a
coleção de `IMarketDataClient` por `Priority` até uma fonte responder com dados válidos.

## Inventário

- `MarketDataValidator.cs` — singleton sem I/O (logger opcional):
  - `IsValidQuote`: ticker presente; data entre 1990-01-01 e amanhã (margem UTC); OHLC + AdjClose > 0;
    `High < Low - epsilon` rejeitado (epsilon `0.0001`); volume ≥ 0.
  - `IsValidDividend`: ticker presente, `Rate > 0`, `ComDate` ≥ 1990-01-01.
  - `SanitizeQuotes`: filtra inválidas (log `[Validator:QuoteRejected]` com motivo), dedupe por `Date`
    mantendo **a última** buscada, saída ordenada por data.
  - `SanitizeDividends`: filtra inválidos, dedupe por `(ComDate, Rate)` mantendo o primeiro.
- `IFallbackMarketDataService.cs` / `FallbackMarketDataService.cs` — injeta a coleção
  `IMarketDataClient` ordenada por `Priority`; para cada chamada:
  - filtra candidatos por `SupportsTicker(ticker)`; nenhum elegível = `Provider.NoEligible`;
  - tenta em ordem; sucesso só se resultado passa no `SanitizeQuotes/SanitizeDividends`;
    sanitização que zera tudo vira `Provider.ValidationFailed` e segue pro próximo;
  - falha/exceção de um provider é acumulada (`{Provider}.Exception`) e o loop continua;
  - quotes esgotadas = `FallbackEngine.Exhausted` com todos os erros agregados;
  - dividends esgotadas = **sucesso com lista vazia** (ativo que nunca pagou é caso normal).
  - Logs marcados `[FallbackEngine:*]` (Attempt/Success/SanitizationFailed/Degraded/Exhausted).

## Regras locais

- Este fallback é o motor do backfill (`Ingestion/AssetBackfillService` via
  `SidecarProviderDirectory`/`--provider`); o daily close tem cadeia própria orquestrada em
  `Ingestion/DailyCloseSyncService` — não duplicar estágios aqui.
- Validador nunca "conserta" dado: ou aceita, ou rejeita com motivo logado. Ajuste de valor sujo
  (ex.: adj_close faltante) é responsabilidade da camada de client/normalização.
- Sanitização sempre antes de persistir — nenhum upsert recebe barras sem passar pelo validador.
- Exceção crua de provider não escapa: capturada e transformada em `Error.Failure("Provider.Exception", …)`.
- Singleton registrado na DI (`MarketDataModuleExtensions`); stateless — pode ser reaproveitado por
  qualquer fluxo novo sem coordenação.
