# IR — Renda Variável: ETFs, Ações e BDRs na B3

⚠️ Conteúdo educacional; regras vigentes podem mudar — validar antes de publicar (RISK-003).
Metadados por ativo: `etf_metadata` (`income_tax_rate`, `day_trade_tax_rate`,
`has_monthly_sales_tax_exemption`, `is_tax_withheld_at_source`, `dividend_withholding_tax`, `tax_notes`).

## Alíquotas de ganho de capital

| Operação | Alíquota | Recolhimento |
| :--- | :---: | :--- |
| Swing trade / comum (venda comum) | **15%** sobre o lucro | DARF próprio do investidor |
| Day trade (compra e venda no mesmo dia) | **20%** | DARF próprio |

## ⚠️ Isenção de R$ 20 mil/mês NÃO vale para ETFs (nem BDRs)

- A isenção para vendas até R$20.000/mês aplica-se **somente a ações** no mercado à vista
  (Lei 8.981/95, art. 72, §4 — "ações").
- Cotas de **ETF** e **BDRs** não são ações → **qualquer lucro gera IR e DARF**, mesmo em vendas pequenas.
- O produto trata isso como alerta visual obrigatório no painel fiscal e na calculadora DARF
  (`has_monthly_sales_tax_exemption = FALSE` para ETFs).

## DARF

- Código **6015** ("Ganhos líquidos em operações de renda variável") — usado p/ operações comuns e day trade.
- Vencimento: **último dia útil do mês subsequente** ao da apuração.
- Apuração mensal por classe de operação; sem lucro apurado ⇒ sem DARF.

## Compensação de prejuízos

- Prejuízo em **operações comuns** compensa lucro em operações comuns da mesma espécie renda variável
  (ações ↔ ETFs de ações; cf. Solução de Consulta COSIT nº 42/2019). Saldo pode ser carregado.
- Prejuízo em **day trade só compensa day trade** (e vice-versa) — regimes separados.
- Ganhos em fundos de ações são tributação exclusiva (15%, sem come-cotas) e não se misturam nessa apuração.
- Ferramentas devem segregar as classes e manter saldo de prejuízos a compensar (base p/ PORT-009).

## BDRs (inclusive BDRs de ETF)

1. **Ganho de capital na venda:** igual RV — 15% swing / 20% DT, DARF próprio, sem isenção de R$20k.
2. **Dividendos/distribuições recebidas via BDR:** duas camadas:
   - **Retenção na fonte no exterior:** EUA **30%** (não há tratado amplo Brasil–EUA), Irlanda **15%**
     (tratado Brasil–Irlanda) → campo `etf_metadata.dividend_withholding_tax`.
   - **Brasil:** tratados como rendimentos de aplicação financeira no exterior (Lei 14.754/2023):
     apuração anual, alíquota de 15%. Crédito do imposto estrangeiro segue tratado — na prática a diferença
     30% × 15% é o motor da calculadora de **tax drag** (B3 × EUA × Irlanda).
   - ⚠️ Detalhes de crédito/apuração exigem revisão fiscal antes de texto definitivo.

## ETFs de Renda Fixa (exceção importante)

Tributação própria pela **Lei 13.043/2014**: **15% fixo retido na fonte** pela corretora na alienação,
**sem come-cotas e sem tabela regressiva**. Não usar as regras desta página para eles — ver
[imposto-renda-fixa.md](imposto-renda-fixa.md). Campo: `is_tax_withheld_at_source = TRUE`.

## Mapeamento para o produto

| Regra | Onde vive |
| :--- | :--- |
| Alíquota swing/DT por ativo | `income_tax_rate` / `day_trade_tax_rate` (defaults 0.15/0.20) |
| Ausência de isenção 20k | `has_monthly_sales_tax_exemption = FALSE` + alerta na UI |
| Quem recolhe | `is_tax_withheld_at_source` (FALSE → DARF 6015 pelo investidor) |
| Come-cotas | `has_come_cotas` (ETFs de bolsa: FALSE) |
| Retenção dividendos exterior | `dividend_withholding_tax` (0.30 / 0.15) |
| Texto explicativo por ativo | `tax_classification`, `tax_notes` (curadoria admin) |
| Calculadora DARF | MVP-014: lucro, IR 15%/20% conforme classe/input, código 6015, vencimento, disclaimer |
