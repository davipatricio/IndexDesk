# 🚀 SEO_TOOLS.md — Estratégia de Ferramentas Públicas, SEO Programático & Conversão

Este documento detalha o ecossistema de **ferramentas públicas (sem necessidade de login)**, **páginas programáticas de SEO** e **gatilhos de conversão** do **IndexDesk** para capturar tráfego orgânico de alta intenção no Google e transformar visitantes em usuários cadastrados.

---

## 1. Filosofia de SEO & Conversão da Plataforma

```text
  [ Busca Orgânica no Google ]
  (Ex: "WRLD11 vs VWRA11", "Calculadora Overlap ETF", "ETF Renda Fixa sem Come-Cotas")
                │
                ▼
  ┌────────────────────────────────────────────────────────────────────────┐
  │         Páginas Públicas Indexadas (SSG / ISR / Server-First)          │
  │   - Carregamento Instantâneo (< 100ms)                                 │
  │   - Uso 100% Gratuito e Interativo no Navegador (sem paywall/login)   │
  │   - JSON-LD Structured Data (Rich Snippets no Google)                  │
  └───────────────────────────────────┬────────────────────────────────────┘
                                      │
                                      ▼
  ┌────────────────────────────────────────────────────────────────────────┐
  │                   Gatilhos de Conversão Não-Intrusivos                 │
  │   - "Salvar esta simulação na minha conta"                             │
  │   - "Exportar Relatório Completo em PDF com Gráficos"                  │
  │   - "Criar Alerta de Desvio de Rebalanceamento"                        │
  │   - "Receber o Fluxo Mensal de Captação CVM por E-mail"                │
  └───────────────────────────────────┬────────────────────────────────────┘
                                      │ (Cadastro com 1 Clique - Google / E-mail)
                                      ▼
  ┌────────────────────────────────────────────────────────────────────────┐
  │                 Usuário Autenticado (Retenção & LTV)                   │
  │   - Gestão de Carteira Real, Histórico de Proventos, Alertas           │
  └────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Catálogo de Ferramentas Públicas e Mini-Ferramentas (Sem Login)

### 🛠️ 1. Comparador 1 vs 1 Programático (`/comparador/[ticker1]-vs-[ticker2]`)

- **Palavras-chave Alvo:** _"WRLD11 vs VWRA11"_, _"IVVB11 ou SPXI11"_, _"BOVA11 vs SMAL11"_, _"B5P211 vs IMAB11"_.
- **Como Funciona:** O Next.js gera páginas estáticas para todas as combinações relevantes de ETFs da B3.
- **Saídas da Ferramenta:**
  - Gráfico comparativo de rentabilidade histórica normalizada.
  - Tabela lado a lado: Taxa de Administração, Domicílio Fiscal, Retenção de Dividendos, Volume Médio, Patrimônio Líquido e Sharpe.
  - Veredito resumido da sobreposição de carteira (_Overlap_).
- **Gatilho de Conversão:** _"Adicionar um terceiro ativo ou comparar com minha carteira atual"_.

---

### 🛠️ 2. Calculadora de Tax Drag & Domicílio Fiscal (B3 vs. EUA vs. Irlanda UCITS)

- **URL:** `/ferramentas/calculadora-tax-drag-irlanda`
- **Palavras-chave Alvo:** _"vale a pena ETF Irlanda"_, _"calculadora imposto ETF exterior"_, _"tax drag calculadora"_, _"BDR de ETF x ETF direto"_.
- **O que Resolve:** Demonstra matematicamente o impacto da retenção de 30% nos EUA vs. 15% na Irlanda (tratado bitributação) vs. 15% de ganho de capital na B3 no longo prazo (10, 20 e 30 anos).
- **Inputs do Usuário:** Aporte inicial, aporte mensal, prazo em anos, dividend yield médio estimado (ex: 2%) e taxa de administração.
- **Saída:** Gráfico visual comparando o patrimônio líquido final nas três jurisdições, descontando taxas e impostos.
- **Gatilho de Conversão:** _"Baixar relatório detalhado em PDF com a memória de cálculo fiscal"_.

---

### 🛠️ 3. Matriz de Sobreposição & Overlap de ETFs (`/ferramentas/overlap`)

- **URL:** `/ferramentas/overlap` (e programática `/overlap/[ticker1]-[ticker2]`)
- **Palavras-chave Alvo:** _"ETF overlap calculator"_, _"sobreposição de ações ETFs B3"_, _"IVVB11 e WRLD11 repetem ações"_.
- **O que Resolve:** Identifica se o investidor está comprando as mesmas empresas duas vezes ao combinar ETFs (ex: quem tem `IVVB11` e `WRLD11` tem ~60% de exposição repetida às mesmas big techs).
- **Saída:**
  - Percentual exato de sobreposição de peso.
  - Tabela com as Top Ações repetidas e a exposição ponderada agregada.
  - Diagrama visual de intersecção (Venn / Bar chart).
- **Gatilho de Conversão:** _"Analisar o overlap de toda a minha carteira (mais de 2 ativos)"_.

---

### 🛠️ 4. Calculadora de Rendimento Real (Equação de Fisher + IPCA Histórico)

- **URL:** `/ferramentas/calculadora-rendimento-real-ipca`
- **Palavras-chave Alvo:** _"como calcular ganho real acima da inflação"_, _"calculadora equação de fisher ipca"_, _"rendimento real cdi"_.
- **O que Resolve:** Calcula o retorno real exato desinflacionado:
  $$\text{Taxa Real} = \frac{1 + \text{Taxa Nominal}}{1 + \text{IPCA}} - 1$$
- **Diferencial:** Permite selecionar períodos históricos reais com 1 clique (ex: _"Governo X"_, _"Últimos 10 anos"_, _"Ano de 2023"_) puxando os dados reais do SGS do Banco Central.
- **Gatilho de Conversão:** _"Simular o rendimento real da minha carteira completa"_.

---

### 🛠️ 5. Calculadora de Equivalência de Renda Fixa (CDI x IPCA+ x LCI/LCA x ETF RF)

- **URL:** `/ferramentas/equivalencia-renda-fixa`
- **Palavras-chave Alvo:** _"quanto rende 120% do cdi em ipca+"_, _"comparador cdb vs lci vs etf renda fixa"_, _"tabela equivalencia renda fixa"_.
- **O que Resolve:** Compara investimentos tributados com alíquota regressiva tradicional (CDBs), isentos (LCI/LCA) e **ETFs de Renda Fixa da B3 (Lei 13.043/14 com 15% fixo e sem come-cotas)**.
- **Gatilho de Conversão:** _"Salvar parâmetros de taxa para alertas de oportunidade"_.

---

### 🛠️ 6. Simulador de Aposentadoria & Renda Passiva (Regra dos 4% com ETFs Globais)

- **URL:** `/ferramentas/simulador-regra-4-por-cento`
- **Palavras-chave Alvo:** _"quanto preciso acumular para viver de renda"_, _"calculadora regra dos 4%"_, _"simulador fire brasil"_, _"aposentadoria com etfs"_.
- **O que Resolve:** Calcula o montante necessário para atingir a independência financeira (_FIRE_), simulando retiradas anuais seguras com base na volatilidade e histórico de ETFs globais e Tesouro IPCA.
- **Gatilho de Conversão:** _"Salvar meu plano de aposentadoria e acompanhar aportes mensais"_.

---

### 🛠️ 7. Guia & Calculadora de DARF para ETFs (`/ferramentas/calculadora-darf-etf`)

- **URL:** `/ferramentas/calculadora-darf-etf`
- **Palavras-chave Alvo:** _"como calcular darf de etf"_, _"etf tem isenção de 20 mil"_, _"aliquota ir venda etf b3"_, _"codigo darf etf 6015"_.
- **O que Resolve:** Ferramenta passo a passo onde o usuário digita o preço médio de compra, preço de venda e quantidade de cotas. A calculadora indica:
  - Lucro líquido auferido.
  - Valor do IR devido (15% ou 20% Day Trade).
  - Código da Receita para emissão do DARF (`6015`).
  - Data limite de pagamento (último dia útil do mês subsequente).
  - Alerta educacional de que **não se aplica a isenção dos R$ 20.000**.
- **Gatilho de Conversão:** _"Calcular DARF com histórico automático importando notas de corretagem"_.

---

### 🛠️ 8. Termômetro de Fluxo e Captação da CVM (`/ferramentas/fluxo-etfs-cvm`)

- **URL:** `/ferramentas/fluxo-etfs-cvm`
- **Palavras-chave Alvo:** _"etfs mais negociados b3"_, _"maiores etfs em patrimonio brasil"_, _"etfs que mais ganharam cotistas"_.
- **O que Resolve:** Dashboard aberto mostrando em tempo quase-real (D+1 CVM):
  - Ranking dos 10 ETFs com maior captação líquida no mês.
  - Ranking dos ETFs que mais ganharam novos cotistas.
  - ETFs com maior volume de negociação diária.
- **Gatilho de Conversão:** _"Receber relatório semanal do fluxo de ETFs no WhatsApp/E-mail"_.

---

## 3. Páginas de Listas Programáticas (Hubs de Conteúdo SEO)

| Rota Programática           | Título SEO (H1)                                                            | Intenção de Busca                                             |
| :-------------------------- | :------------------------------------------------------------------------- | :------------------------------------------------------------ |
| `/etfs/renda-fixa`          | **Todos os ETFs de Renda Fixa da B3 (Sem Come-Cotas e com Taxas)**         | Investidores fugindo do come-cotas de fundos bancários.       |
| `/etfs/dividendos`          | **Melhores ETFs de Dividendos da B3 (DIVO11, NSDV11 e mais)**              | Buscas por renda passiva e dividendos mensais.                |
| `/etfs/sp500`               | **Comparativo de Todos os ETFs de S&P 500 na B3 (IVVB11, SPXI11, SPXB11)** | Investidores querendo a menor taxa adm para dolarizar.        |
| `/bdrs/etfs-internacionais` | **Guia Completo de BDRs de ETFs Listados na B3**                           | Investidores buscando ETFs globais sem conta no exterior.     |
| `/gestoras/[gestora]`       | **ETFs e Fundos de Índice da [BlackRock / Investo / Itaú]**                | Buscas por produtos de gestoras específicas.                  |
| `/relatorios`               | **Cartas Mensais e Relatórios de Gestoras de ETFs**                        | Pesquisa aprofundada de teses de investimento institucionais. |
| `/noticias`                 | **Notícias e Fatos Relevantes de ETFs e BDRs na B3**                       | Acompanhamento de eventos e frescor de conteúdo SEO.          |

---

## 4. Infraestrutura Técnica para Dominar o SEO

### 4.1. Structured Data (Schema.org / JSON-LD)

- **Páginas de Ativos (`/etf/[ticker]`):**
  - `FinancialProduct`: Nome, ticker, moeda, taxa de administração, gestora.
  - `BreadcrumbList`: `Início > ETFs de Ações > VWRA11`.
- **Páginas de Ferramentas e Calculadoras (`/ferramentas/*`):**
  - `WebApplication` / `SoftwareApplication`: Classifica a página como ferramenta interativa no Google.
  - `FAQPage`: Perguntas e respostas frequentes sanando dúvidas imediatas sobre o cálculo abaixo da ferramenta.

### 4.2. Geração Dinâmica de OpenGraph Images (`@vercel/og`)

Toda página de ativo e comparador gera dinamicamente uma imagem de preview de 1200x630px para compartilhamento em redes sociais:

- **Exemplo para `/etf/vwra11`:** Card escuro moderno com o logo do ativo, cotação atual em R$, rentabilidade dos últimos 12 meses e miniatura do gráfico de linha.
- **Exemplo para `/comparador/bova11-vs-ivvb11`:** Card comparativo exibindo as duas linhas de rentabilidade sobrepostas.

### 4.3. Dynamic Sitemap (`sitemap.xml`) e Robots.txt

- Geração do sitemap no Next.js dividida em índices:
  - `sitemap-assets.xml`: Todos os tickers `/etf/*` e `/bdr/*`.
  - `sitemap-comparisons.xml`: Top 500 pares comparativos mais buscados (`/comparador/*-vs-*`).
  - `sitemap-tools.xml`: Todas as calculadoras públicas.
- `lastmod` atualizado com o timestamp da última cotação ingerida pelo `IndexDesk.Worker`.

---

## 5. Estratégia de Lead Magnet & Conversão Ética

| Ponto de Contato na Ferramenta | Mecanismo de Conversão                         | Valor Entregue ao Usuário                                             |
| :----------------------------- | :--------------------------------------------- | :-------------------------------------------------------------------- |
| **Simulador de Backtest**      | Botão _"Salvar Simulação"_                     | Guarda o link permanente na conta para reavaliar no futuro.           |
| **Comparador de Ativos**       | Botão _"Exportar Relatório PDF"_               | Gera um PDF diagramado profissional com tabelas e gráficos.           |
| **Calculadoras Fiscais**       | Campo _"Enviar memória de cálculo por E-mail"_ | O usuário recebe o passo a passo com o código do DARF por e-mail.     |
| **Catálogo de ETFs**           | Botão _"Criar Alerta de Desvio"_               | Notifica quando o ETF atingir novo topo histórico ou desviar da meta. |
| **Páginas de Artigos / Hubs**  | Caixa _"Radar Semanal dos ETFs"_               | Newsletter enxuta com o resumo de fluxo da CVM e novidades da B3.     |
