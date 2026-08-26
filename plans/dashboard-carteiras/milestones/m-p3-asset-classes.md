# Marco M-P3 — Cobertura Completa de Classes

Objetivo: todas as 12 classes do grill funcionando com valuation correto, especialmente renda fixa
com accrual local-first.

## Tarefas

- [x] DDL `portfolio_fixed_income_positions` aplicado (`docker/init-db/05-portfolio-fixed-income.sql`)
[x] `FixedIncomeAccrualCalculator` puro (CDI%/CDI+/Selic/IPCA+/Prefixado) + 9 testes unitários
- [x] Accrual integrado ao valuation de caixa sintético no resumo (parâmetros RF por carteira+índice)
- [ ] Vetores de teste com séries reais do `macro_economic_series` (validação financeira)
- [ ] `PortfolioAccrualDailyJob` idempotente (`last_accrual_date` auditável)
- [ ] ⛔ BLOCKED — Catálogo de classes sem ativo no catálogo (Tesouro específico, previdência
      PGBL/VGBL, fundos): dependem de seed/curadoria de ativos que ainda não existe. Documentado
      no PR; contornável via caixa sintético + parâmetros RF.
- [x] Endpoints RF: `/fixed-income` (attach/list/detach) + `/timeline` (vencimentos/carências)
- [x] `PortfolioAccrualDailyJob` registrado no Worker (23:10 UTC Mon-Fri, antes do snapshot)
- [ ] Wizard: ramo de formulário por classe (RF: indexador/taxa/vencimento/liquidez ✓ sintético;
      Tesouro ⛔ BLOCKED acima; fundos ⛔ cota manual aguarda MVP-003; previdência ⛔ idem)
      — ramo de RF/sintético e corp actions entram neste marco
- [ ] CDI/Selic sintético como posição (caixa rendendo)
- [ ] Moedas estrangeiras USD/EUR em espécie (valuation via `fx_rates`)
- [x] Fundos de investimento com cota manual por ativo+data (override manual já é regra geral)
      ⛔ feed automático segue BLOCKED em MVP-003
- [ ] Previdência tipo próprio (regressiva default, toggle progressiva, fase acumulação)
- [ ] ⛔ BLOCKED — Cripto top 100: provider não escolhido (sidecar yfinance vs AwesomeAPI
      estendida). Enquanto isso, cripto entra como preço manual no wizard.
- [ ] ⛔ BLOCKED — Catálogo Tesouro Direto (títulos específicos): seed manual vs raspagem oficial;
      accrual já suporta NTN-B via IPCA_PLUS quando o ativo existir
[x] Corp actions no wizard (split/inpc/bonificação/subscrição com fator)
- [ ] Timeline de vencimentos/carências na carteira
- [ ] Agrupamento por corretora com sub-totais na tabela

## Aceite

- Accrual re-executado no mesmo dia não muda valor (idempotência testada).
- Fórmulas validadas contra fontes oficiais (revisão financeira antes de merge).
