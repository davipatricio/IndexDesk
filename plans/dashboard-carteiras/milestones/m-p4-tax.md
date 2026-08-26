# Marco M-P4 — Fiscal

Objetivo: camada fiscal educacional dentro do simulador, com premissas visíveis e disclaimers.

## Tarefas

- [x] Ledger fiscal derivado on-demand das transações (tabela materializada adiada — sem job mensal ainda)
[x] `TaxCalculators` puros + **34 testes** (fronteiras exatas, IOF dia 0/15/29/30+, isenções ações vs ETF/FII/LCI, come-cotas maio/nov, venda parcial, DARF 6015 c/ vencimento e rollover dez→jan)
[x] Endpoint `/tax/redemption-simulation` (parcial por quantidade ou total) com breakdown completo — smoke E2E
- [ ] Alerta de janela de isenção mensal dentro do simulador
- [ ] Ledger de prejuízos compensáveis por classe (ações↔BDR; RF separado)
- [ ] Come-cotas já pago explícito na posição RF ("perdeu R$ X com antecipação")
[x] `/tax/darf/{year}/{month}` — projeção mensal por classe com isenções — smoke E2E
- [ ] Disclaimer educacional + premissas + competência em toda resposta fiscal
- [ ] ⛔ BLOCKED na exposição pública: revisão editorial dos textos fiscais pendente (RISK-003) — superfície já exige login

## Fora deste marco

Apuração mensal completa/export DARF (PORT-009), day trade, compensação automática em sugestões.
