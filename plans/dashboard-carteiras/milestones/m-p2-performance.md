# Marco M-P2 — Performance & Gráficos

Objetivo: série histórica confiável do patrimônio e métricas de retorno corretas com benchmarks.

## Tarefas

- [x] DDL + hypertable `portfolio_daily_snapshots` aplicado (`docker/init-db/04-portfolio-snapshots.sql`, guard para bancos sem Timescale)
- [x] `PortfolioSnapshotDailyJob` no Worker (23:30 UTC Mon-Fri, upsert idempotente; escopo via IServiceScopeFactory)
- [x] Série on-demand em `PortfolioPerformanceService`: grade = pregões ∪ transações, forward-fill de cotações/fx, poda de zeros iniciais preservando fluxos
- [ ] `WealthAreaChart` (lightweight-charts) com marcações de aporte
- [x] Presets (1M/3M/6M/1A/Tudo) + datas livres via nuqs (`p`,`de`,`ate`) — YTD fica pra iteração seguinte
- [x] `PerformanceEngine.TimeWeightedReturnPercent` (encadeamento diário; 1º ponto = baseline) + testes
- [x] `MoneyWeightedReturnAnnualPercent` XIRR (Newton + bisseção, formulação forward-compounding) + casos sem mudança de sinal
- [x] Endpoint `/performance?from&to&benchmarks=` com simples+TWR+MWR+vol/Sharpe/maxDD — smoke real: TWR=simples com fluxo único ✓, MWR anualizada coerente ✓
- [x] Benchmarks CDI (SGS 12 local) e IBOV (asset_quotes) sobrepostos base 100 → R$ inicial no `performance-panel.tsx` (recharts)
- [x] Coluna peso (%) na tabela de posições
- [ ] Contribuição verdadeira ao retorno (exige série por ativo)
- [x] Métricas de risco (vol a.a., Sharpe c/ excesso CDI, drawdown máximo) nos cards do painel
- [ ] Aba "Análise" dedicada quando houver mais métricas
- [ ] `PortfolioValuationRefreshJob` (reprojeção explícita pós-sync; resumo hoje é on-demand a cada leitura)

## Smoke E2E (2026-08-25)

- Carteira IVVB11 comprada 01/05 (feriado): poda correta → série começa 05-04 com fluxo carregado
- 80 pontos · simples 10,76% = TWR ✓ · MWR a.a. 253% coerente com janela de 114 dias ✓
- CDI normalizado 104 · IBOV 92 no fim da janela

## Aceite

- TWR neutro a aportes; MWR reflete datas dos fluxos (testes provam os dois).
- [ ] Série reconstruída do log bate com snapshots diários (teste de reconciliação).
