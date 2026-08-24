# MarketData/Calculators — Funções financeiras puras (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md). Este doc detalha o que o mapa de pastas do módulo resume para `Calculators/`.

## Responsabilidade

Matemática de performance e proventos usada pelas APIs de leitura do módulo (`Services/AssetQueryService`).
Tudo aqui é **função estática pura**: sem I/O, sem DbContext, sem DI — só entrada, saída e regra de negócio,
no padrão exigido por `apps/backend/src/CLAUDE.md` para qualquer cálculo de domínio.

## Inventário

| Arquivo | Conteúdo |
| :--- | :--- |
| `PerformanceCalculators.cs` | `PerformanceCalculators` (static): `FactorFromPercent`, `ReturnBetween`, `AccumulateRateSeries`, `TotalReturnWithDividends`, `AnnualizedReturn`, `AnnualizedVolatility`, `MaxDrawdown`, `DailyReturns`. |
| `DividendCalculators.cs` | `DividendCalculators` (static): `SumLast12Months` e `YieldPercent` — painel "quanto o ativo pagou". |

## Contratos que valem aqui

- **Sempre `decimal`** na superfície pública; conversões internas para `double` só onde a raiz/potência
  exige (`Math.Pow`, `Math.Sqrt`) e o resultado volta para `decimal`.
- **Guard clauses retornam valor neutro, não exceção**: start ≤ 0 → retorno `0m`; série com < 2 pontos
  → volatilidade `0m`; `currentPrice` nula/≤ 0 → yield `null`. Chamador decide se degrada ou oculta o campo.
- Convenções numéricas: retorno em **percent** (`* 100m`); volatilidade anualizada sobre **252 pregões**;
  anualização usa **365.25 dias/ano**; arredondamentos explícitos (`Math.Round(..., 2)` na vol/drawdown,
  `6` casas no somatório de proventos).
- `AccumulateRateSeries` compõe taxas percentuais por período (diária/mensal) via
  `product(1 + r/100) - 1` — mesma fórmula dos demais módulos; não reinventar variação local.
- `TotalReturnWithDividends` replica a visão "rendimentos + valorização" das plataformas de FII:
  `(endClose/startClose - 1 + Σdividends/startClose) * 100`.

## Consumo

- `Services/AssetQueryService` chama estas funções ao montar `AssetQuoteStatsDto`,
  `PerformanceResponseDto` e `AssetDividendsDto` — nada de lógica numérica inline nos serviços.
- Novas funções: adicionar aqui + teste unitário em `tests/IndexDesk.UnitTests`
  (`Calculators/FinancialCalculatorsTests.cs` é o padrão do repositório).

## Anti-padrões proibidos nesta pasta

- Qualquer dependência de EF/Redis/HttpClient (quebraria testabilidade offline).
- Estado estático mutável ou cache — caching é responsabilidade de `Services/`.
- Duplicar fórmula já existente em `Modules.Analytics/Calculators` sem reaproveitar/alinhar.
