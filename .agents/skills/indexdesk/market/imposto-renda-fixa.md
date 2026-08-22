# IR — Renda Fixa, Come-Cotas e Isenções

⚠️ Conteúdo educacional; validar regras vigentes antes de publicar (RISK-003).
Faixas de IR devem ser **versionadas** no código (requisito PORT-005).

## Come-Cotas (antecipação semestral de IR)

- Mecanismo: nos **últimos dias úteis de maio e novembro**, o fundo "come" uma fatia do rendimento do
  semestre e a usa para pagar o IR antecipado do cotista — reduz o nº de cotas/valor da cota.
- Alíquota: **20%** (fundos curto prazo) / **15%** (longo prazo), conforme classe do fundo.
- **Quem TEM:** fundos tradicionais (DI/renda fixa, multimercados). **Quem NÃO TEM:** ETFs listados na B3,
  fundos de ações, LCI/LCA/CRI/CRA, Tesouro Direto, ações/BDRs.
- É o principal argumento de eficiência dos ETFs RF vs fundos bancários (juros compostos sem mordida semestral).

## Tabela regressiva de IR (renda fixa em geral: CDB, Tesouro, debêntures comuns)

| Prazo da aplicação | Alíquota |
| :--- | :---: |
| até 180 dias | **22,5%** |
| 181–360 dias | **20%** |
| 361–720 dias | **17,5%** |
| acima de 720 dias | **15%** |

- Retido na fonte sobre o rendimento no resgate/vencimento. Prazo conta da aplicação ao resgate (dias corridos).
- Testes obrigatórios nas fronteiras 180/360/720 (PORT-005).

## ETFs de Renda Fixa na B3 — Lei 13.043/2014 (regime especial)

- **Alíquota única de 15%**, **retida na fonte pela corretora** na alienação (o investidor não emite DARF).
- **Sem come-cotas** e **sem tabela regressiva** — vantagem vs CDB longo prazo (que chega a 15% só após 2 anos)
  e vs fundos RF tradicionais (come-cotas).
- Exemplos de classe: ETFs de títulos públicos/credito (ex.: IMAB11, NFCR11) — classificação por ativo vive em
  `etf_metadata.tax_classification` / `is_tax_withheld_at_source = TRUE`.
- Comparador público "equivalência CDI × IPCA+ × LCI/LCA × ETF RF" deve usar este regime.

## Isentos de IR para pessoa física

| Instrumento | Observações |
| :--- | :--- |
| **LCI / LCA** | Letras hipotecárias/agrícolas; `portfolio_fixed_income_positions.is_tax_exempt = TRUE` |
| **CRI / CRA** | Securitização |
| **Debêntures incentivadas** (art. 2º Lei 12.431) | Destinadas a infraestrutura |
| **FI-INFRA** | Fundos de investimento em participações infra (condições legais específicas) |

Isento ≠ isento de IOF (resgate <30 dias ainda sofre IOF onde aplicável).

## IOF (< 30 dias)

Tabela regressiva linear: **96% no dia 1 → 0% no dia 30** sobre o rendimento (aplicável a renda fixa/Tesouro
resgatados com menos de 30 dias).

## Mapeamento para o produto

| Regra | Onde vive |
| :--- | :--- |
| Come-cotas por ativo | `etf_metadata.has_come_cotas` (ETFs bolsa = FALSE) |
| Regime especial RF | `is_tax_withheld_at_source`, `tax_classification` |
| Isenção por posição | `portfolio_fixed_income_positions.is_tax_exempt` |
| Indexadores p/ accrual | enum `CDI_PERCENT|CDI_PLUS|SELIC|IPCA_PLUS|PREFIXED` + `rate_percentage` + `spread` |
| Apuração regressiva projetada | PORT-005: faixas versionadas, fronteiras testadas, revisão fiscal obrigatória |
