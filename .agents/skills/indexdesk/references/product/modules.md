# Módulos do Produto (MVP & Core — Fase 01)

Resumo operacional de cada módulo público/administrativo. Fonte integral: `PRODUCT.md`.

## 1. Catálogo rico de ETFs & BDRs (`/etf/[ticker]`, `/bdr/[ticker]`)

- **Metadados:** CNPJ, gestora, administrador, taxa de adm, taxa de performance (`assets` + `etf_metadata`).
- **Estatísticas:** PL atual e histórico 12M, nº de cotistas, volume médio diário (`fund_daily_reports`, `etf_analytics_summary`).
- **Métricas de fundo:** benchmark, tracking error/difference, volatilidade 12M, Sharpe, max drawdown (`etf_analytics_summary`).
- **Decomposição:** Top 10 holdings, exposição geográfica/setorial, domicílio fiscal EUA vs Irlanda vs Brasil (`etf_holdings`).
- **Painel fiscal completo (diferencial do produto):** status come-cotas, alíquotas swing/day trade,
  alerta de ausência da isenção de R$20k/mês para ETFs, modelo de recolhimento (DARF próprio × retenção na fonte),
  guia de compensação de prejuízos. Campos: `has_come_cotas`, `income_tax_rate`, `day_trade_tax_rate`,
  `has_monthly_sales_tax_exemption`, `is_tax_withheld_at_source`, `tax_classification`, `tax_notes`.
  → Regras por classe de ativo: [`../../market/imposto-renda-variavel.md`](../../market/imposto-renda-variavel.md)
- **Integração TradingView:** botão "Abrir Gráfico no TradingView" →
  `https://br.tradingview.com/chart/?symbol=BMFBOVESPA%3A{TICKER}` (símbolo persistido em `assets.tradingview_symbol`; usar o valor persistido, nunca montar por suposição).
- Exibir sempre disclaimer + data de atualização dos dados.

## 2. Comparador multi-ativos (até 6)

- Ativos: ETFs B3, BDRs de ETF, ações + índices de referência (Ibovespa, S&P 500, NASDAQ-100, CDI, Selic, IPCA, IGP-M, Teva).
- Rentabilidade normalizada (% base 0 na data inicial) + tabela lado a lado de risco/retorno/custos.
- Estado na URL (nuqs): ativos e período devem ser representados na URL; limite de 6 validado server-side.
- Séries longas agregam server-side.

## 3. Simulador de backtest (público, sem login)

- Inputs: pesos somando 100%, capital inicial, aportes mensais recorrentes, intervalo de datas (1M/6M/1A/5A/Max),
  rebalanceamento (nenhum/mensal/semestral/anual).
- Saídas: evolução do patrimônio (R$), drawdown histórico, CAGR, volatilidade, pior/melhor ano, rendimento real acima do IPCA.
- Motor em `Modules.Analytics` (`Calculators/` puros). Resultados repetidos podem cachear em Redis (input normalizado).
- Fórmulas: [`../../market/calculos-financeiros.md`](../../market/calculos-financeiros.md)

## 4. Ferramentas fiscais públicas (sem login)

Overlap matrix, tax drag (B3 × EUA × Irlanda), rendimento real (Fisher), equivalência CDI×IPCA+×pré,
DARF ETF, regra dos 4%, termômetro de fluxo CVM. URLs/palavras-chave:
[`seo-growth.md`](./seo-growth.md). Status no roadmap: MVP-012/013/014 **não iniciados**.

## 5. Admin & Backoffice (`/admin`) — RBAC `Role = ADMIN`

- `/admin/assets`: curadoria de ativos (descrições, gestoras, benchmark, símbolos TradingView, regras tributárias).
- **Override manual por campo:** conflitos entre provedores exibem origem/data; campos travados
  (`locked_fields` JSONB / `is_manually_overridden = TRUE`) são ignorados pelo sync do Worker.
  Decisão canônica pendente DEC-001.
- Upload manual de holdings CSV/Excel (fallback obrigatório quando não há feed de gestora).
- `/admin/news`, `/admin/reports`: criação/publicação editorial de notícias e relatórios (PDF/lâminas).
- `/admin/sync-jobs`: saúde dos jobs Quartz (sucesso/falha, duração, linhas), trigger manual confirmado,
  audit log de quem alterou o quê (`audit_logs`).
- Status: MVP-015/016 **não iniciados**.

## 6. Hub de Notícias & Relatórios de Gestoras (`/noticias`, `/relatorios`, `/etf/[ticker]#noticias`)

- Ingestão híbrida: automática (RSS gestoras, comunicados CVM/B3) + manual (curadoria admin).
- Associação N:N notícia↔ativo (`article_asset_tags`); relatórios mensais por gestora (`manager_reports`).
- Slug único sanitizado; conteúdo sanitizado antes de renderizar; origem e data sempre visíveis.
- Status: MVP-017/018 **não iniciados**.

## Fase 02/03 (futuro, não scaffoldar ainda)

Carteiras estilo Yahoo/Gorila (transações BUY/SELL/TRANSFER, preço médio Receita, TWR/MWR-TIR),
motor de accrual de renda fixa (CDI%/CDI+/Selic/IPCA+/pré), calendário de eventos (COPOM, proventos,
rebalanceamento de índices), rebalanceamento inteligente por aporte e apuração mensal de DARF (código 6015).
