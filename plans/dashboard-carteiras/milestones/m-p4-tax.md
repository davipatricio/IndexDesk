# Marco M-P4 — Fiscal

Objetivo: camada fiscal educacional dentro do simulador, com premissas visíveis e disclaimers.

## Tarefas

- [ ] DDL `portfolio_tax_ledger` aplicado
- [ ] `TaxCalculators` puros: regressiva (fronteiras 180/360/720 testadas), IOF 0–30d,
      come-cotas semestral, isenções (R$20k ações — não ETF; R$35k FII; LCI/LCA/CRI/CRA)
- [ ] Endpoint `/simulate-redemption` (parcial por R$ ou quantidade + total) com breakdown completo
- [ ] Alerta de janela de isenção mensal dentro do simulador
- [ ] Ledger de prejuízos compensáveis por classe (ações↔BDR; RF separado)
- [ ] Come-cotas já pago explícito na posição RF ("perdeu R$ X com antecipação")
- [ ] `/tax-projection` — DARF projetado "se vender hoje" (código 6015 + vencimento quando aplicável)
- [ ] Disclaimer educacional + premissas + competência em toda resposta fiscal
- [ ] Revisão editorial dos textos fiscais antes de expor (RISK-003)

## Fora deste marco

Apuração mensal completa/export DARF (PORT-009), day trade, compensação automática em sugestões.
