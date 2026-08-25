# Marco M-P3 — Cobertura Completa de Classes

Objetivo: todas as 12 classes do grill funcionando com valuation correto, especialmente renda fixa
com accrual local-first.

## Tarefas

- [ ] DDL `portfolio_fixed_income_positions` aplicado
- [ ] `FixedIncomeAccrualCalculator` puro (CDI%/CDI+/Selic/IPCA+/Prefixado, base 252, dias úteis B3)
      + vetores de teste com séries reais do `macro_economic_series`
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
