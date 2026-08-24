# IndexDesk.Modules.Analytics — Cálculos financeiros & backtest (scoped)

> Companion to [`../../CLAUDE.md`](../../CLAUDE.md) (convenções de `src/`) and
> [`../../../CLAUDE.md`](../../../CLAUDE.md) (workspace backend). Regras específicas deste módulo.
> **Do not modify code** when only instruction updates are requested.

## Responsabilidade

Métricas e simulações sobre dados **já persistidos** localmente: backtest de carteira com
aportes/rebalanceamento, Sharpe, drawdown máximo e rendimento real (equação de Fisher).
É o módulo referência para o padrão de duas extensões descrito em `src/CLAUDE.md`
(`AddAnalyticsModule` + `MapAnalyticsEndpoints` em `AnalyticsModuleExtensions.cs`).

## Rotas expostas (`/api/v1/analytics`, grupo `Analytics`) — mapeadas apenas no host `IndexDesk.Api`

| Método | Rota | Função (`WithName`) | Contrato |
| :--- | :--- | :--- | :--- |
| POST | `/backtest` | `RunBacktest` | 200 `BacktestResponse` · 404 `Analytics.AssetNotFound` · 400 `Analytics.BacktestInvalid` · 422 `Analytics.BacktestUnavailable` / `Analytics.BenchmarkUnavailable` / `Analytics.BacktestInvalid` |
| GET | `/real-yield?nominalRate=&inflationRate=` | `GetRealYield` | 200 `{ nominalRate, inflationRate, realYieldPercent }` · 400 se inflação ≤ -100% |

Erros nunca estouram como exceção não tratada: o handler do `/backtest` converte o `BacktestFailure`
por **código** via `switch`, e captura `ArgumentException`/`InvalidOperationException` dos cálculos
transformando-os em 422 com código. Manter esse mapeamento ao adicionar rotas novas.

## DI (`AddAnalyticsModule`)

- `IBacktestService` → `BacktestService` (**scoped**, depende de `IndexDeskDbContext`).
- Nenhum outro serviço registrado hoje. `MapAnalyticsEndpoints` é chamado somente no
  `Program.cs` do `IndexDesk.Api`; o Worker não expõe analytics.

## Calculadoras puras (`Calculators/FinancialCalculators.cs`)

Static class `FinancialCalculators` — funções estáticas puras, sem I/O, sem DbContext, determinísticas:

- `CalculateRealYield(nominal, inflation)` — Fisher exato: `((1+n)/(1+i)) - 1`. Aceita percentual
  (>1) ou decimal (≤1); arredonda a 4 casas; lança `ArgumentOutOfRangeException` se `i ≤ -1`.
- `CalculateMaxDrawdown(equityCurve)` — pico-vale sobre a curva de capital; retorna % negativo,
  2 casas; lista vazia/nula devolve `0m`.
- `CalculateSharpeRatio(annualizedReturn, riskFreeRate, volatility)` — `(R - Rf) / σ`, tolera
  entrada em % ou decimal; volatilidade ≤ 0 devolve `0m`.

Regra do módulo: **toda matemática nova entra aqui** como static pure function (mesmo padrão de
`MarketData/Calculators`). Correlação entre séries ainda não existe no código — quando for pedida,
nasce nesta pasta com teste unitário antes de qualquer endpoint.

## Serviço de aplicação (`BacktestService.cs`)

`internal sealed class BacktestService` implementa `IBacktestService.RunAsync` retornando
`BacktestResult` (record com fábricas `Success`/`Unavailable`). Fluxo:

1. Valida request (montante > 0, aporte ≥ 0, ≥1 alocação, pesos somam 100% com tolerância
   `±0.01`, datas ordenadas) — falha vira `Analytics.BacktestInvalid`.
2. Consulta `Assets` e `AssetQuotes` (`AsNoTracking`) **do Postgres local** por ticker; ticker
   ausente → `Analytics.AssetNotFound`; <2 cotações ou <2 datas comuns → `Analytics.BacktestUnavailable`.
3. Interseção das datas entre ativos; preço usado = `AdjClose` (fallback `Close`); não-positivo
   lança `InvalidOperationException` → 422 `Analytics.BacktestUnavailable`.
4. Benchmark: `"CDI"` (SeriesCode 12, default), `"SELIC"` (11), `"NONE"` desativa; série vem de
   `MacroEconomicSeries`; dia sem taxa usa a média do período. Falha → `Analytics.BenchmarkUnavailable`.
5. Simulação diária: aportes mensais na virada do mês, rebalanceamento
   `none|monthly|quarterly|semiannual|annual` (default `annual`), retorno diário exclui o aporte do dia.
6. Métricas finais: retorno total, anualização por dias corridos/365.25, volatilidade amostral
   anualizada ×√252, Sharpe (risk-free = retorno anualizado do benchmark), drawdown máximo.

## DTOs

Records `sealed` imutáveis, sem lógica: `AllocationItem`, `BacktestRequest`, `EquityPoint`,
`BacktestResponse` (em `AnalyticsModuleExtensions.cs`); `BacktestResult`, `BacktestFailure`
(em `BacktestService.cs`). Novos contratos seguem esse formato — nada de classes mutáveis.

## Dados & cache

- Fonte de dados é **sempre** Postgres/Redis local (regra Local-First): nenhuma rota deste módulo
  chama provider externo; séries chegam via jobs do `IndexDesk.Worker`.
- O `.csproj` referencia `BuildingBlocks.Cache`, mas **hoje não há uso de Redis** no módulo.
  Quando métricas pré-computadas forem cacheadas: usar `ICacheService` (nunca cliente inline),
  TTL conforme `PROVIDERS.md`, e invalidação consumindo os eventos de ingestão do RabbitMQ
  (Worker publica; este módulo consome e re-warma do Postgres).

## Testes

- Unit: `tests/IndexDesk.UnitTests/Calculators/FinancialCalculatorsTests.cs` — tabela Fisher,
  drawdown (-25% esperado) e Sharpe (0.50 esperado). Toda função nova de `Calculators/` exige teste
  equivalente aqui.
- Integração: `tests/IndexDesk.IntegrationTests/Api/ApiEndpointsTests.cs` —
  `Backtest_WhenAssetIsMissing_ReturnsNotFound` (404 + código `Analytics.AssetNotFound`) e
  `RealYield_CalculatesCorrectly` (`7.6923` para 12%/4%). Endpoint novo = teste no grupo
  `/api/v1/analytics`.
