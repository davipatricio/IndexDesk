# Market — Regras do Mercado Brasileiro (índice)

Regras de mercado, regulação e fisco usadas pelas calculadoras, painéis fiscais e conteúdo editorial
do IndexDesk. Escritas para uso por agentes — cada arquivo é autocontido.

## Arquivos

| Arquivo | Conteúdo |
| :--- | :--- |
| [b3-operacoes.md](b3-operacoes.md) | B3: tickers/sufixos, pregão, liquidação D+2, índices e carteiras teóricas, corporate actions, feriados |
| [cvm-regulacao-dados.md](cvm-regulacao-dados.md) | CVM: ICVM 555, informe diário, CDA, cad_fi, CNPJ (validação), fluxo/captação |
| [imposto-renda-variavel.md](imposto-renda-variavel.md) | IR sobre ETFs/ações/BDRs: swing 15% × day trade 20%, **sem isenção R$20k p/ ETF**, DARF 6015, compensação, dividendos de BDRs |
| [imposto-renda-fixa.md](imposto-renda-fixa.md) | Come-cotas, tabela regressiva (180/360/720), ETFs RF Lei 13.043/14, LCI/LCA isentas, IOF |
| [tesouro-direto.md](tesouro-direto.md) | Tesouro Selic/IPCA+/Prefixado, custódia B3, marcação a mercado, tributação |
| [indicadores-selic-cdi-ipca.md](indicadores-selic-cdi-ipca.md) | Definições Selic/CDI/IPCA/IGP-M, séries SGS, acumulação base 252, conversões |
| [calculos-financeiros.md](calculos-financeiros.md) | Todas as fórmulas implementáveis: accrual, CAGR, vol, Sharpe, drawdown, Fisher, overlap, PM, TWR/MWR, rebalance |

## Regras-fonte (valem para tudo abaixo)

1. **Dados > constantes.** Indicadores macro vêm da tabela `macro_economic_series` (SGS 11/12/433/189);
   regras fiscais por ativo vêm de `etf_metadata.*`. Nunca hardcodar alíquota/índice no código.
2. **Valores que mudam no tempo** (Selic meta, custódia B3, tabelas) devem ser lidos da fonte ingerida ou
   versionados em regras (`PORT-005` exige faixas de IR versionadas). Ao escrever texto educacional,
   indicar "conforme regras vigentes" quando aplicável.
3. **Base 252 dias úteis** é a convenção brasileira para anualização de CDI/Selic/volatilidade;
   calendário de dias úteis = tabela `market_holidays` (ANBIMA).
4. **Fisco é educacional:** toda saída fiscal exibe premissas, data-fonte e disclaimer; não é orientação
   individual (RISK-003). Regulamentações mudam — validar antes de publicar conteúdo novo.
5. **Provenance:** todo número exibido carrega origem/data (CVM D+1, BCB D+1, holdings com `as_of_date`).
