# 📈 IndexDesk — Plataforma de Inteligência, Backtest e Analytics para ETFs e BDRs

## 1. Visão Geral do Produto

O **IndexDesk** é uma plataforma web para investidores do mercado brasileiro com foco central em **ETFs (nacionais e internacionais) e BDRs de ETFs listados na B3**. A plataforma reúne dados profundos de gestoras, composição de fundos, eficiência fiscal, ferramentas de comparativo multi-ativos, simuladores de backtest e calculadoras financeiras.

---

## 2. Proposta de Valor e Diferenciais

- **Centralização de Dados de ETFs/BDRs:** Elimina a dispersão de informações entre sites de gestoras (Investo, BlackRock, Itaú, Nu Asset) e CVM.
- **Transparência Fatorial e Fiscais:** Exibe claramente sobreposição de ativos (_Overlap_), taxas ocultas, domicílio fiscal (EUA vs. Irlanda) e retenção de dividendos na fonte.
- **Motor de Backtest Rápido:** Permite simular alocações de R$ 1.000 a R$ 1.000.000+ com rebalanceamento automático, considerando câmbio, inflação (IPCA) e CDI.
- **Foco Absoluto em SEO:** Páginas estáticas/ISR altamente otimizadas para cada ticker (`/etf/vwra11`, `/bdr/bijs39`), atraindo tráfego orgânico constante via buscas do Google.

---

## 3. Módulos do Produto (Fase 1 — MVP & Core)

### 3.1. Catálogo Ricos de ETFs & BDRs

- **Página de Ativo (`/etf/[ticker]` e `/bdr/[ticker]`):**
  - **Metadados:** CNPJ, Gestora, Administrador, Taxa de Administração, Taxa de Performance.
  - **Estatísticas de Mercado:** Patrimônio Líquido (PL / AUM), Histórico do PL (12M), Número de Cotistas, Volume Médio Diário.
  - **Métricas de Fundo:** Benchmark subjacente, Tracking Error, Tracking Difference, Volatilidade 12M, Sharpe Ratio, Max Drawdown.
  - **Decomposição:** Top 10 Holdings, Exposição Geográfica, Exposição Setorial, Domicílio Fiscal (EUA vs. Irlanda vs. Brasil) e tributação sobre dividendos.
  - **Painel Fiscal e Tributário Completo:**
    - **Come-Cotas:** Status claro se o ativo possui ou não incidência semestral de come-cotas (maio/novembro) vs. tributação exclusiva na alienação/resgate.
    - **Alíquota de Imposto de Renda (IR):** Alíquotas de Swing Trade (ex: 15% para ETFs de ações e ETFs de RF > 720 dias) e Day Trade (20%).
    - **Regras de Isenção e Recolhimento:** Alerta visual destacando a **não aplicação da isenção de R$ 20k/mês** para ETFs de ações na B3 e modelo de recolhimento (DARF próprio pelo investidor vs. Retenção na fonte pela corretora para ETFs de Renda Fixa).
    - **Compensação de Prejuízos:** Guia explicativo de compensação de perdas acumuladas da mesma classe.
  - **Integração Direta com TradingView:**
    - Botão/Link com badge oficial: _"Abrir Gráfico no TradingView"_ direcionando para `https://br.tradingview.com/chart/?symbol=BMFBOVESPA%3A{TICKER}` para análise técnica com indicadores customizados.

### 3.2. Motor de Comparação Multi-Ativos e Índices

- Comparador lado a lado de até 6 ativos ou índices.
- **Ativos Suportados:** ETFs B3, BDRs de ETFs, Ações individuais.
- **Índices de Referência Integrados:** Ibovespa, S&P 500, NASDAQ-100, CDI, Selic, IPCA, IGPM, Índices Teva.
- **Visualização:** Gráfico de rentabilidade normalizada (% base 0 na data inicial) e tabela comparativa de indicadores de risco/retorno.

### 3.3. Simulador de Backtest de Carteiras

- Montagem de carteiras customizadas com pesos percentuais (soma = 100%).
- Configurações de Simulação:
  - Capital Inicial e Aportes Mensais Recorrentes.
  - Intervalo de datas livre ou predefinido (1M, 6M, 1A, 5A, Max).
  - Regra de Rebalanceamento: Sem rebalanceamento, Mensal, Semestral, Anual.
- Saídas da Simulação:
  - Gráfico de Evolução do Patrimônio (R$).
  - Gráfico de Drawdown histórico (maiores quedas no período).
  - Tabela com CAGR (Retorno Anualizado), Volatilidade, Pior Ano, Melhor Ano e Rendimento Real acima do IPCA.

### 3.4. Ferramentas Fiscais e Calculadoras de Apoio

- **Matriz de Sobreposição (_ETF Overlap_):** Identifica a % de ações repetidas ao combinar 2 ou mais ETFs.
- **Calculadora de Tax Drag / Eficiência Fiscal:** Compara o retorno acumulado entre ETFs listados na B3, ETFs dos EUA e ETFs UCITS da Irlanda.
- **Calculadora de Rendimento Real (Equação de Fisher):** Desconto exato da inflação acumulada (IPCA).
- **Calculadora de Equivalência CDI x IPCA+ x Pré-Fixado.**

### 3.5. Painel Administrativo & Backoffice (`/admin`)

Área restrita e segura (RBAC - `Role = ADMIN`) para a equipe de produto e dados gerenciar a integridade da plataforma:

- **Gestão Cadastral & Curadoria de Ativos (`/admin/assets`):**
  - Cadastro de novos ETFs/BDRs, edição de descrições ricas, gestoras, benchmark atrelado, links de lâminas e símbolos do TradingView.
  - Ajuste fino de regras tributárias (alíquotas de IR, come-cotas, retenção na fonte).
- **Resolução de Divergências & Override Manual (`metadata_lock`):**
  - Visualizador de conflitos entre provedores (ex: cotação divergente entre Brapi e Yahoo, ou CNPJ da CVM sem vínculo).
  - Capacidade de "travar" campos cadastrais (`is_manually_overridden = TRUE`) para impedir que a ingestão automática do `IndexDesk.Worker` sobrescreva correções editoriais manuais.
- **Importação Manual de Holdings & Relatórios de Gestoras:**
  - Upload de arquivos CSV / Excel com composição de fundos para novos ETFs ou gestoras locais que não possuem feeds diários públicos.
  - Visualização e edição rápida da tabela de Top 10 Holdings e pesos percentuais.
- **Editor e Publicação de Notícias & Relatórios (`/admin/news`, `/admin/reports`):**
  - Criação manual de artigos, comunicados de mercado e upload de relatórios/lâminas em PDF de gestoras, associando a um ou mais tickers e gestoras.
- **Monitoramento de Sincronizações & Logs de Auditoria (`/admin/sync-jobs`):**
  - Dashboard em tempo real da saúde dos jobs do Quartz.NET (sucesso/falha, tempo de execução, linhas inseridas).
  - Botão de disparo manual (_Trigger Sync_) para forçar reprocessamento imediato de uma data ou ativo específico.
  - Histórico completo de auditoria (_Audit Log_) registrando quem alterou cada dado.

### 3.6. Hub de Notícias, Relatórios de Gestoras & Fatos Relevantes

- **Notícias e Comunicados Contextuais por Ativo (`/etf/[ticker]#noticias`):**
  - Exibição de notícias, fatos relevantes CVM e comunicados da B3 diretamente na página do ativo.
- **Hub Central de Pesquisa e Relatórios (`/relatorios` e `/noticias`):**
  - Cartas mensais de gestoras (BlackRock, Investo, Itaú, Nubank Asset), relatórios de teses de ETFs e lâminas em PDF para download com visualizador integrado.
  - Filtros por gestora (`/gestoras/[gestora]`), classe de ativo (Ações BR, Global, Renda Fixa, Cripto) e data.
- **Ingestão Híbrida:**
  - **Automática:** Ingestão de feeds RSS de gestoras, comunicados abertos CVM/B3 e APIs de notícias financeiras.
  - **Manual:** Publicação e curadoria editorial pela equipe do IndexDesk via painel Admin.

---

## 4. Estratégia de SEO & Growth (Inbound Marketing)

Para dominar os motores de busca e capturar tráfego orgânico no nicho financeiro:

- **Páginas dinâmicas indexáveis para cada ticker:** `/etf/[ticker]` e `/bdr/[ticker]` geradas via SSG/ISR.
- **Structured Data (Schema.org / JSON-LD):** Marcações `FinancialProduct` e `BreadcrumbList` em todas as páginas de ativos para gerar Rich Snippets no Google.
- **Geração de Dynamic OpenGraph Images (@vercel/og):** Cards visuais gerados automaticamente para compartilhamento no WhatsApp/Twitter com gráfico e retorno atual do ETF.
- **Sitemap Dinâmico (`sitemap.xml`) e Robots.txt:** Atualizados via job do Next.js a cada inclusão/atualização de ativo.
- **Calculadoras embeddáveis:** Ferramentas públicas indexadas no Google para palavras-chave como _"Calculadora Rendimento Real IPCA"_ ou _"Comparador WRLD11 x VWRA11"_.

---

## 5. Roadmap e Funcionalidades Futuras (Fase 2 & 3)

### 5.1. Sistema Completo de Gestão de Carteiras (Style Yahoo Finance / Gorila / Google Finance)

- **Múltiplas Carteiras por Usuário:** Carteira Principal, Reserva de Oportunidade, Aposentadoria Internacional, etc.
- **Registro Detalhado de Transações:**
  - **Tipos Suportados:** Compra (`BUY`), Venda (`SELL`), Transferência de Custódia (`TRANSFER_IN`, `TRANSFER_OUT` entre corretoras), Bonificação e Reinvestimento de Proventos.
  - **Custos Operacionais:** Registro de corretagem, taxas B3 e emolumentos para dedução automática do custo de aquisição.
- **Cálculo Fiscal de Preço Médio (PM):**
  - Média ponderada estrita no padrão da Receita Federal.
  - Ajustes automáticos de PM e quantidade após desdobramentos (_splits_) e agrupamentos (_inplits_).
- **Métricas Avançadas de Rentabilidade:**
  - **TWR (Time-Weighted Return):** Rentabilidade real da estratégia, eliminando a distorção de aportes e saques ao longo do tempo.
  - **MWR / TIR (Taxa Interna de Retorno):** Rentabilidade financeira real do capital do investidor.
  - **Comparativo Multi-Benchmark:** Rentabilidade da carteira vs. CDI, Ibovespa, IPCA e S&P 500 (em R$ e em USD).

### 5.2. Motor de Auto-Cálculo de Renda Fixa (CDI / Selic / IPCA+)

- Permite cadastrar títulos pós-fixados (CDB, LCI/LCA, LFTS11, Tesouro Selic, Debêntures) com taxas parametrizadas (ex: 110% do CDI, CDI + 1.5% a.a., IPCA + 6.2%).
- **Acúmulo Automático em Segundo Plano:** O sistema utiliza a série histórica diária de CDI/Selic do Banco Central (SGS) para capitalizar diariamente os títulos, mantendo o saldo atualizado a cada fechamento de mercado sem intervenção manual.
- **Tributação Regressiva Projetada:** Cálculo automático do imposto de renda devido de acordo com a tabela regressiva (22.5% até 180 dias ➔ 15% acima de 720 dias) e isenções legais (LCI/LCA).

### 5.3. Calendário de Eventos do Mercado & Proventos

- **Eventos Corporativos de Ativos:** Datas COM, Ex e Pagamento de dividendos/JCP, com alertas configuráveis.
- **Eventos Macroeconômicos:** Datas das reuniões do COPOM (decisão da taxa Selic), divulgação do IPCA/IBGE e vencimento de contratos futuros.
- **Rebalanceamento de Índices:** Datas de rebalanceamento das carteiras teóricas do Ibovespa, IFIX e índices internacionais.

### 5.4. Rebalanceamento Inteligente & Assistente de DARF

- **Aportes Inteligentes:** Sugestão exata de quantas cotas de cada ETF comprar com o novo aporte para rebalancear a carteira sem precisar vender ativos nem gerar fatos geradores de IR.
- **Painel de Apuração Mensal de DARF:**
  - Segregação de lucros/prejuízos em ETFs de Ações vs. BDRs vs. Ações.
  - Controle de prejuízos acumulados a compensar.
  - Geração das informações de pagamento (Código DARF `6015`, valor devido, vencimento).
