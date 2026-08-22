# B3 — Mercado, Tickers, Índices e Operações

## O que é

A B3 é a bolsa brasileira (ações, ETFs, BDRs, FIIs, renda fixa, derivativos). Moeda de negociação: **BRL**.

## Formato de tickers (4 letras + 1–2 dígitos)

| Sufixo | Classe | Exemplos |
| :--- | :--- | :--- |
| **11** | ETFs e units | `BOVA11`, `IVVB11`, `SMAL11`, `WRLD11`, `IMAB11`, `GOLD11`, `B5P211` |
| **32/33/34/39** | BDRs (certificados de valores estrangeiros) | `BIJS39` (BDR de ETF iShares), `AAPL34` |
| 3 / 4 / 5/6 | Ações ON / PN / classes | `PETR4`, `VALE3` |

- Regra prática do produto: **ETF termina em 11**; BDR de ETF tipicamente em 30s/40s (ex.: `BIJS39`, `JSUS11` é ETF local — validar por `asset_type`, nunca só pelo sufixo).
- Símbolo TradingView: `BMFBOVESPA:{TICKER}` (persistido em `assets.tradingview_symbol`).

## Pregão e liquidação

- Janela útil considerada pelo produto: **10h–18h BRT** (leilão de abertura ~09:45–10:00; leilão de fechamento
  encerra o pregão contínuo ~17:00; after-market restrito). Cache intraday TTL 15min usa essa janela.
- Dias de pregão = dias úteis ANBIMA → tabela `market_holidays` (feriados nacionais até 2099).
- Liquidação financeira do mercado à vista: **D+2** (`portfolio_transactions.settlement_date`).

## Custos operacionais

Corretagem + emolumentos/taxas B3 → registrados em `portfolio_transactions.brokerage_fee` e entram no custo
de aquisição (preço médio). Ver [calculos-financeiros.md](calculos-financeiros.md) §PM.

## Corporate actions (obrigatório tratar em séries históricas)

- **Split** (desdobramento, ex. 1:10 → fator 10.0), **inplit** (agrupamento, fator <1), **bonificação**, **mudança de ticker**.
- Persistidos em `asset_corporate_actions` (`factor`, `effective_date`). Backtests/gráficos usam **`adj_close`**
  para evitar "degraus" falsos. Sem ajuste, PM e retornos ficam errados.
- ETFs raramente têm split; ações e BDRs com frequência maior.

## Índices de referência (benchmarks do comparador)

| Índice | O que é | Observações |
| :--- | :--- | :--- |
| **Ibovespa (IBOV)** | Principal índice da B3 (~80+ ativos, liquidez ponderada) | Carteira teórica rebalanceada quadrimestralmente (jan/mai/set); Yahoo `^BVSP` |
| **SMLL** | Small caps | Também quadrimestral |
| **IDIV** | Diversificação (governança/dividendos) | |
| **IFIX** | FIIs | Carteiras teóricas publicadas pela B3 (`dados.b3.com.br`) |
| **IMA-B / IMA-B 5+** | Tesouro IPCA+ (ANBIMA) | Séries ANBIMA ingeridas pelo Worker |

Rebalanceamentos de carteira teórica são eventos (`market_events_calendar.event_type = 'ETF_REBALANCE'`/índices).
Sempre versionar composição ingerida com data de referência.

## ETFs × BDRs de ETF (diferença-chave p/ fisco)

- **ETF listado na B3:** fundo de índice ICVM 555 cotado em bolsa; tributação conforme classe
  (RV: 15%/20% DARF próprio; RF: Lei 13.043/14, 15% na fonte).
- **BDR de ETF:** certificado depositário de um ETF estrangeiro (EUA/Irlanda); ganho de capital igual RV
  (15%/20%), mas dividendos/distribuições sofrem retenção no exterior (EUA 30%, Irlanda 15%) e tributação
  como renda do exterior. Ver [imposto-renda-variavel.md](imposto-renda-variavel.md).

## Regras de implementação

- Nunca inferir classe por sufixo de ticker — usar `assets.asset_type`.
- Feriados: sempre consultar `market_holidays` (não calcular Carnaval etc. na mão).
- Volume médio diário em R$ (não nº de negócios) nas métricas (`etf_analytics_summary.avg_daily_volume_30d`).

## Ficha técnica B3 do ETF (fonte: páginas oficiais de produto, 2026-08)

| Característica | Valor |
| :--- | :--- |
| Código de negociação | `XXXX11` (4 letras + sufixo de cota de fundo) |
| Cotação | Reais por cota, 2 casas decimais |
| Liquidação | Física e financeira, **D+2** |
| Mercado | À vista |
| Lote padrão | Secundário: **1 cota**; primário: definido pelo emissor |
| Emissão/resgate | **In-kind** (cesta de ativos ↔ lote mínimo de cotas) via agentes autorizados, ao PL/cota apurado no fechamento do dia da solicitação |

- Índice de referência deve ser reconhecido pela CVM (não precisa ser índice B3 — ex.: Teva, Morningstar).
- Famílias derivadas listadas pela B3 (conteúdo educacional/SEO futuro): opções semanais e flexíveis
  sobre ETFs, termo de ETF, BDR de ETF, ETF CONNECT.
- O portal B3 é JS-rendered (Lumis): **nunca raspar HTML** das páginas "ETFs Listados"; usar arquivos
  open-data (`dados.b3.com.br`) ou CVM.

## Regras de implementação (adendo rankings)

- Ranking por métrica ordena **em memória** após cálculo por ativo; ativos sem dado suficiente na
  métrica ficam sempre no fim (qualquer direção). Cache Redis 10 min.
- Volume médio = média de `close × volume` das ~30 sessões recentes (`avgVolume30D` nos DTOs),
  coerente com `etf_analytics_summary.avg_daily_volume_30d` quando a tabela for materializada.
