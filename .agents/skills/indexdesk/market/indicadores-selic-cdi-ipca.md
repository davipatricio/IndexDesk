# Indicadores: Selic, CDI, IPCA, IGP-M

Fonte de verdade: séries do **BCB SGS** ingeridas diariamente (23:00 UTC) em `macro_economic_series`
(series_code + date + value). **Nunca hardcodar valores atuais no código.**

## Séries SGS usadas

| Indicador | Código SGS | Frequência | Significado |
| :--- | :---: | :--- | :--- |
| Selic diária | `11` | diária | Taxa efetiva diária anualizada (base 252) |
| CDI diário | `12` | diária | Taxa DI média anualizada (base 252) |
| IPCA | `433` | mensal | Variação % mensal (IBGE) — índice oficial de inflação |
| IGP-M | `189` | mensal | Variação % mensal (FGV) |

Endpoint padrão: `https://api.bcb.gov.br/dados/serie/bcdata.sgs.{code}/dados?formato=json`
(+ `&dataInicial=dd/mm/aaaa&dataFinal=dd/mm/aaaa` para janelas).

## Definições

- **Selic:** taxa básica de juros, definida pelo **COPOM** (8 reuniões/ano). Meta expressa % a.a. base 252.
  "Selic over" = efetiva; historicamente ≈ meta.
- **CDI:** taxa dos depósitos interbancários; acompanha a Selic (~0,10 p.p. abaixo quando a Selic é alta).
  É o benchmark padrão de renda fixa e o "risk-free" do Sharpe no produto.
- **IPCA:** inflação oficial (IBGE), coleta mensal, divulgação ~10 dias após o mês. Referência para
  rendimento real e títulos IPCA+.
- **IGP-M:** inflação FGV (mais sensível a câmbio/commodities).

## Como acumular (implementação)

- **Taxa diária (CDI/Selic):** fator por dia útil = `(1 + taxa_aa/100)^(1/252)`.
  Acumulação multiplicativa apenas em **dias úteis** (`market_holidays`).
  Valor futuro: `VF = VP × ∏(fator_d)` — ver [calculos-financeiros.md](calculos-financeiros.md) §1.
- **Mensal (IPCA/IGP-M):** acúmulo do período = `∏(1 + m_i) − 1` com m_i a variação mensal em decimal.
- **Anualização/desannualização** entre bases:
  - mensal→anual `(1+i_m)^12 − 1`; anual→mensal `(1+i_a)^(1/12) − 1`;
  - diária útil↔anual via expoente 252; diária corrida↔anual via 365 (só quando a fonte exigir).

## Regras de implementação

- Calculadoras/páginas leem **somente** `macro_economic_series` local (cache Redis 24h; ingest BCB 23h UTC).
- Toda saída exibe: série usada (ex.: "CDI — SGS 12"), intervalo de datas e data da última atualização.
- Reprocessar um período não duplica linhas (PK series_code+date).
- Não buscar BCB em runtime de requisição (DEC-006 aceito).
