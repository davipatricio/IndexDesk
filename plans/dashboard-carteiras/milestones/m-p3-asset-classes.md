# Marco M-P3 — Cobertura Completa de Classes

Objetivo: todas as 12 classes do grill funcionando com valuation correto, especialmente renda fixa
com accrual local-first.

## Tarefas

- [x] DDL `portfolio_fixed_income_positions` aplicado (`docker/init-db/05-portfolio-fixed-income.sql`)
[x] `FixedIncomeAccrualCalculator` puro (CDI%/CDI+/Selic/IPCA+/Prefixado) + 9 testes unitários
- [x] Accrual integrado ao valuation de caixa sintético no resumo (parâmetros RF por carteira+índice)
- [ ] Vetores de teste com séries reais do `macro_economic_series` (validação financeira)
- [ ] `PortfolioAccrualDailyJob` idempotente (`last_accrual_date` auditável)
- [ ] Wizard: ramo de formulário por classe (RF: indexador/taxa/vencimento/liquidez; Tesouro:
      título+vencimento; fundos: cota manual; previdência: regime)
- [ ] CDI/Selic sintético como posição (caixa rendendo)
- [ ] Moedas estrangeiras USD/EUR em espécie (valuation via `fx_rates`)
- [ ] Fundos de investimento com cota manual por ativo+data ⛔ feed automático bloqueado em MVP-003
- [ ] Previdência tipo próprio (regressiva default, toggle progressiva, fase acumulação)
- [ ] Cripto top 100 ⛔ BLOCKED: escolher provider (sidecar yfinance vs AwesomeAPI estendida);
      enquanto isso preço manual
- [ ] Catálogo Tesouro Direto (títulos específicos) ⛔ BLOCKED: seed manual vs raspagem oficial
- [ ] Corp actions no wizard (split/inpc/bonificação/subscrição com fator)
- [ ] Timeline de vencimentos/carências na carteira
- [ ] Agrupamento por corretora com sub-totais na tabela

## Aceite

- Accrual re-executado no mesmo dia não muda valor (idempotência testada).
- Fórmulas validadas contra fontes oficiais (revisão financeira antes de merge).
