# CVM — Regulação e Dados Abertos

## Papel

A CVM (Comissão de Valores Mobiliários) regula o mercado de valores mobiliários brasileiro (análogo à SEC).
ETFs são fundos de índice regulados pela **Instrução CVM 555** (fundos de investimento) com cotas listadas na B3.
Portal de dados abertos: `dados.cvm.gov.br` (HTTP estático, sem auth).

## Arquivos ingeridos pelo Worker

### 1. Informe Diário — `INFORME_DIARIO/DADOS/inf_diario_fi_YYYYMM.zip` (diário, D+1)

Campos: `CNPJ_Fundo`, `DT_COMPTC`, `VL_QUOTA`, `VL_PATRIM_LIQ`, `NR_COTST`.
→ tabela `fund_daily_reports` (`quota_value` alta precisão, `net_asset_value`, `shareholders_count`,
`net_issuance_redemption`). Ingestão ~04:00 com streaming COPY.

### 2. CDA mensal — `CDA/DADOS/cda_fi_YYYYMM.zip`

Composição e Diversificação das Aplicações: `CNPJ_Fundo`, `TP_APLIC` (tipo de ativo), `CD_ATIVO` (ticker/código),
`QT_TIT` (quantidade), `VL_MERC` (valor de mercado).
→ `etf_holdings`: alimenta **Top 10 holdings, exposição setorial/geográfica e matriz de overlap**.
Referência mensal — sempre exibir `as_of_date`.

### 3. Cadastro — `CAD/DADOS/cad_fi.csv`

`CNPJ_Fundo`, `DENOM_SOCIAL`, `G_GESTOR` (gestor), `ADMIN` (administrador), `TAXA_ADM`.
→ enriquece `etf_metadata` (manager/admin/fee) via match por CNPJ.

## CNPJ (14 dígitos) — formato e validação

- Formato numérico no banco: **14 dígitos sem máscara**, indexado (`assets.cnpj`); máscara `XX.XXX.XXX/YYYY-ZZ`.
- Dígito verificador módulo-11: pesos do primeiro DV sobre os 12 primeiros = `[5,4,3,2,9,8,7,6,5,4,3,2]`;
  resto <2 → DV=0, senão DV=11−resto. Segundo DV usa os 13 dígitos com pesos `[6,5,4,3,2,9,8,7,6,5,4,3,2]`.
- **Match assets↔CVM é por CNPJ.** CNPJ sem vínculo cadastral vira conflito para o admin resolver
  (override manual + `locked_fields`), não pode ser auto-resolvido silenciosamente.
- Fundos não mapeados são descartados no stream (nunca criar asset automático a partir de informe órfão).

## Conceitos de produto alimentados pelos dados CVM

| Métrica | Origem | Uso |
| :--- | :--- | :--- |
| PL / histórico 12M | `VL_PATRIM_LIQ` diário | Página do ativo, ranking |
| Nº cotistas / novos cotistas | `NR_COTST` (delta mensal) | Termômetro de fluxo `/ferramentas/fluxo-etfs-cvm` |
| Captação líquida | `net_issuance_redemption` | Ranking mensal de fluxo |
| Valor da cota | `VL_QUOTA` | Série histórica alternativa à cotação B3 |
| Holdings & overlap | CDA + feeds gestoras | Overlap matrix, Top 10 |

## Regras de implementação

- Dados CVM têm defasagem **D+1** (informe) / mensal (CDA): exibir sempre "dados referentes a DD/MM — fonte CVM".
- Idempotência obrigatória: reprocessar o mesmo mês atualiza, não duplica.
- Filtro por CNPJ **antes** do COPY; métricas de linhas processadas/inseridas/ignoradas em `sync_job_logs`.
- Conteúdo sanitizado quando notícias/comunicados CVM viram `news_articles`
  (categoria `CVM_RELEVANT_FACT`).
