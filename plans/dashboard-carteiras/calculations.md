# Motor de Cálculo

Calculadores puros e testáveis em `IndexDesk.Modules.Portfolio/Calculators` (espelha
`Modules.Analytics/Calculators`). Fórmulas canônicas: `market/calculos-financeiros.md` da skill.

## 1. Preço médio (`AveragePriceCalculator`)

- BUY: `novo_pm = (qtd_ant × pm_ant + qtd × unit + fees) / (qtd_ant + qtd)` — despesas entram no custo.
- SELL: reduz quantidade, PM intacto; realização = `(unit − pm) × qtd − fees_venda`.
- Quantidade decimal livre; arredondamento só na exibição (pt-BR).

## 2. Corp actions

| Evento | Efeito |
| :--- | :--- |
| Split `f:n` (1:2 dobra) | qtd × (n/f); PM ÷ (n/f) |
| Grupamento (inpc) `f:n` | qtd ÷ (f/n); PM × (f/n) |
| Bonificação `pct` | qtd × (1+pct); PM dilui proporcional |
| Subscrição | trata como BUY com preço de subscrição (PM mistura) |

## 3. Rentabilidade

- **Simples:** Δ patrimônio / base ajustada, aportes fora do retorno.
- **TWR:** encadeamento sub-períodos entre fluxos externos; convenção Dietz modificado quando não
  há valuation intraday; timezone America/Sao_Paulo, dias úteis B3.
- **MWR/TIR:** Newton/bisseção sobre fluxos datados; provento vira caixa (fluxo negativo).
- Presets: 1M default, 3M/6M/YTD/1A/Max + datas livres.

## 4. Accrual RF (`FixedIncomeAccrualCalculator`, M-P3)

- Base 252, dias úteis (calendário B3 local via série de cotações/macro).
- `CDI_PERCENT`: fator diário `(1+cdi_anual)^(1/252)` × % do CDI.
- `CDI_PLUS`: spread somado à taxa antes do fator.
- `IPCA_PLUS`: IPCA acumulado no período × (1+spread) pró-rata dias úteis.
- `PREFIXED`: exponencial base 252.
- Idempotente: reexecução no mesmo dia não duplica (guarda `last_accrual_date`).

## 5. Fiscal (`TaxCalculators`, M-P4)

| Regra | Valor |
| :--- | :--- |
| IR regressiva RF/previdência | ≤180d 22,5% · ≤360d 20% · ≤720d 17,5% · >720d 15% |
| IR swing variável | 15% (day trade 20%, fora do MVP de projeção) |
| IOF | tabela decrescente 0–30 dias |
| Come-cotas | semestral (último dia útil mai/nov), alíquota da faixa +15pp no complemento |
| Isenções PF | vendas ações ≤ R$20k/mês (**não vale ETF**); FII R$35k/mês; LCI/LCA/CRI/CRA isentas |

Ledger de prejuízos por classe (ações↔BDR compensam; RF separado). Toda saída fiscal carrega
premissas + competência + disclaimer educacional.

## 6. FX

- Custo histórico congela câmbio do `trade_date` em `fx_rate`.
- Valuation corrente: último close de `fx_rates`.
- Cripto BRL: par direto `-BRL` quando existir; senão USD × USDBRL ⛔ provider pendente.

## 7. Projeção de metas

- Run-rate: extrapola retorno recente até `target_value`/`target_pct` → data estimada.
- Juros compostos: `VF = VP(1+i)^n + PMT[((1+i)^n −1)/i]` com i e PMT escolhidos pelo usuário.
