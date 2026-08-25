# Marco M-P2 — Performance & Gráficos

Objetivo: série histórica confiável do patrimônio e métricas de retorno corretas com benchmarks.

## Tarefas

- [ ] DDL + hypertable `portfolio_daily_snapshots` (TimescaleDB) aplicado
- [ ] `PortfolioSnapshotDailyJob` no Worker (upsert idempotente por dia)
- [ ] Recalculador on-demand de série a partir das transações (backfill retroativo)
- [ ] `WealthAreaChart` (lightweight-charts) com marcações de aporte
- [ ] Presets de período (1M default/3M/6M/YTD/1A/Max) + data início/fim livre (nuqs na URL)
- [ ] `TwrCalculator` puro (encadeamento sub-períodos, Dietz modificado) + vetores de teste
- [ ] `MwrCalculator` puro (Newton/bisseção) + casos sem fluxo e fluxo no mesmo dia
- [ ] Endpoint `/performance?from&to&benchmark=...` retornando simples+TWR+MWR
- [ ] Benchmarks sobrepostos no chart (CDI/IPCA/IBOV/S&P via `macro-series` + cotações locais)
- [ ] Coluna contribuição (%) na tabela de posições
- [ ] Aba "Análise": vol anualizada, Sharpe, drawdown máximo da carteira
- [ ] `PortfolioValuationRefreshJob` básico (reprojeção pós-sync)

## Aceite

- TWR neutro a aportes; MWR reflete datas dos fluxos (testes provam os dois).
- Série reconstruída do log bate com snapshots diários (teste de reconciliação).
