# Tesouro Direto

Títulos públicos federais vendidos via B3 (programa Tesouro Direto). Tributação = tabela regressiva
de renda fixa + IOF <30d — ver [imposto-renda-fixa.md](imposto-renda-fixa.md).

## Tipos de título

| Título | Mecânica | Detalhes |
| :--- | :--- | :--- |
| **Tesouro Selic (LFT)** | Pós-fixado: acompanha a **taxa Selic diária** (não o CDI) | Possui redutor pequeno definido em regulamento (~0,02% a.a. conforme emissão); risco de marcação ~zero; usado como "caixa" |
| **Tesouro IPCA+ (NTN-B)** | Híbrido: **IPCA + cupom fixo** (tipicamente 6% a.a.) | Cupons semestrais na versão com juros; existe versão sem juros semestrais. Marca a mercado |
| **Tesouro Prefixado (LTN)** | Taxa fixada na compra, sem cupom | Paga 100% no vencimento; **marca a mercado** (preço oscila antes do vencimento) |

- NTN-F: prefixado **com cupons semestrais** (versão antiga de referência).
- Marcação a mercado: prefixados e IPCA+ oscilam com expectativa de juros → backtests/posições devem usar
  preço de mercado diário, não só taxa contratada.

## Custos

- **Taxa de custódia B3:** ~0,20% a.a. sobre o valor (cobrança semestral; há isenção para saldo até R$10 mil
  no Tesouro Selic, conforme regra vigente — confirmar antes de publicar valores).
- Taxa 0% do Tesouro Direto em si (compra direta sem corretagem).

## Regras de implementação (motor de accrual — PORT-004)

- Indexadores suportados no modelo (`fixed_income_indexer_enum`): `SELIC`, `IPCA_PLUS`, `PREFIXED`
  (+ `CDI_PERCENT`/`CDI_PLUS` p/ CDB/LCI/LCA privados).
- Capitalização **somente em dias úteis** aplicáveis usando as séries locais:
  - Selic/LFT e CDI: fator diário `(1+taxa_aa)^(1/252)` por dia útil ([fórmulas](calculos-financeiros.md)).
  - IPCA+: capitaliza o IPCA mensal divulgado + cupom pro-rata.
- Job do Worker idempotente: guarda `last_accrual_date` e `current_value` auditáveis; reexecução não duplica juros.
- Tributação regressiva projetada por prazo (22,5→15%) + IOF <30d; resultado sempre mostra premissas/competência.
- `maturity_date`, `initial_amount`, `rate_percentage` em `portfolio_fixed_income_positions`.
