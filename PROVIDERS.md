# `PROVIDERS.md` — Guia de Provedores de Dados e Integrações

Este documento especifica todas as fontes de dados e APIs externas utilizadas pelo **IndexDesk**, incluindo custos, limites de taxa (*rate limits*), métodos de acesso e a estratégia de resiliência do sistema.

---

## 1. Resumo Consolidado dos Provedores

| Provedor | Categoria | Custo | Rate Limit | Função Principal no IndexDesk |
| :--- | :--- | :---: | :---: | :--- |
| **BCB (Banco Central - SGS)** | Macroeconomia | **Gratuito** | ~100 req/min | Histórico de CDI, Selic, IPCA e IGP-M. |
| **CVM (Informe Diário)** | Regulatório / PL | **Gratuito** | Sem limite rígido (HTTP/FTP) | Cota, PL histórico (12M), Cotistas diários e Gestoras. |
| **CVM (CDA - Carteira Mensal)** | Holdings Nacionais | **Gratuito** | Sem limite rígido (HTTP mensal) | **Composição e ativos dos fundos/ETFs da B3** (Holdings & Overlap). |
| **B3 (Portal de Dados Abertos)** | Mercado Oficial | **Gratuito** | Download programático diário | Carteiras teóricas de índices (IBOV, SMLL, IDIV) e códigos ISIN. |
| **ANBIMA (Dados Abertos)** | Feriados & Renda Fixa | **Gratuito** | Sem limite rígido | Feriados bancários (regra 252 dias úteis) e índices IMA-B / IDA. |
| **Feeds Diretos de Gestoras (iShares, Vanguard, Investo)** | Holdings Globais/Locais | **Gratuito** | Download de CSVs públicos | Holdings diários oficiais dos ETFs (Top 10, pesos, setores). |
| **Brapi.dev** | B3 Market Data | **Freemium / R$ 29-99** | 10 a 1.000 req/min | Cotações diárias ajustadas e dividendos de ETFs e BDRs na B3. |
| **Yahoo Finance API** | Benchmarks Globais | **Gratuito (Não oficial)** | ~2.000 req/IP/hora | Índices globais (`^BVSP`, `^GSPC`, `^IXIC`) e Câmbio (`USDBRL=X`). |
| **Financial Modeling Prep (FMP) / EODHD** | Dados Globais (Backup) | **Freemium / $19-$29/mês** | 250 a 10.000 req/dia | Holdings e setores de ETFs UCITS (Irlanda) e ETFs dos EUA. |
| **HG Brasil Finanças** | B3 Backup Data | **Freemium / R$ 39/mês** | 500 req/dia (Free) | Provedor nacional de contingência para cotações e moedas. |

---

## 2. Detalhamento Técnico por Provedor

### 2.1. Banco Central do Brasil (BCB - SGS)
* **Tipo:** API REST (JSON)
* **Autenticação:** Nenhuma (Pública)
* **Documentação:** [BCB SGS API](https://dadosabertos.bcb.gov.br/)

#### Principais Séries Cadastradas:
| Indicador | Código da Série | Frequência | Endpoint BCB |
| :--- | :---: | :---: | :--- |
| **CDI Diário** | `12` | Diária | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.12/dados?formato=json` |
| **Selic Diária** | `11` | Diária | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.11/dados?formato=json` |
| **IPCA Mensal** | `433` | Mensal | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.433/dados?formato=json` |
| **IGP-M Mensal** | `189` | Mensal | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.189/dados?formato=json` |

#### Estratégia de Ingestão:
* **Cron Job:** Ingestão diária às **23:00 UTC** no `IndexDesk.Worker` via Quartz.NET.
* **Filtro de Data:** Query string `?dataInicial=dd/mm/aaaa&dataFinal=dd/mm/aaaa` para evitar carregar o histórico completo.

---

### 2.2. CVM — Portal de Dados Abertos (Informes + Composição CDA)
* **Tipo:** Arquivos CSV/Zip (HTTP estático)
* **Autenticação:** Nenhuma (Pública)
* **Frequência de Atualização:** Diária (D+1) e Mensal

#### Arquivos Utilizados:
1. **Informe Diário (`/INFORME_DIARIO/DADOS/inf_diario_fi_YYYYMM.zip`):**
   * **Campos:** `CNPJ_Fundo`, `DT_COMPTC`, `VL_QUOTA`, `VL_PATRIM_LIQ`, `NR_COTST`.
   * **Ingestão:** Worker diário às **04:00 AM** com streaming via `CsvHelper` + `NpgsqlBinaryImporter` (`COPY`).
2. **Composição e Diversificação das Aplicações (`/CDA/DADOS/cda_fi_YYYYMM.zip`):**
   * **Campos:** `CNPJ_Fundo`, `TP_APLIC` (Tipo de Ativo), `CD_ATIVO` (Ticker/Código), `QT_TIT` (Quantidade), `VL_MERC` (Valor de Mercado).
   * **Função:** Alimenta a tabela `etf_holdings` para a **Matriz de Overlap** e Top 10 Holdings dos ETFs nacionais.
3. **Cadastro Geral (`/CAD/DADOS/cad_fi.csv`):**
   * **Campos:** `CNPJ_Fundo`, `DENOM_SOCIAL`, `G_GESTOR`, `ADMIN`, `TAXA_ADM`.

---

### 2.3. Feeds Oficiais de Gestoras (Holdings Diários)
Muitos emissores disponibilizam arquivos diários de composição de carteira:
* **BlackRock (iShares Brasil):** Exportação CSV pública por produto (ex: `BOVA11`, `IVVB11`, `SMAL11`, `ACWI`).
  * *Padrão de URL:* `https://www.ishares.com/br/produtos/[ID]/[ticker]/1495093766861.ajax?fileType=csv&fileName=[ticker]_holdings`
* **Investo ETF:** Dados e composição de ETFs globais listados na B3 (`WRLD11`, `BDEF11`, `ALUG11`).
* **Vanguard (ETFs US/UCITS):** Composição detalhada de `VOO`, `VTI`, `VWRA`.
* **Ingestão:** O `IndexDesk.Worker` roda um job semanal de harmonização de holdings para manter a tabela `etf_holdings` sempre atualizada.

---

### 2.4. ANBIMA & B3 (Feriados e Índices)
1. **Feriados Bancários ANBIMA:**
   * Lista oficial de feriados nacionais até 2099 utilizada para preencher a tabela `market_holidays` e calcular o CDI acumulado em dias úteis (base 252).
2. **Índices Teva e ANBIMA:**
   * Séries históricas de índices de Renda Fixa (`IMA-B`, `IMA-B 5+`, `IRF-M`, `IDA`) e índices temáticos Teva.
3. **B3 Market Data Aberto (`dados.b3.com.br`):**
   * Composição das carteiras teóricas do Ibovespa, IFIX, SMLL e IDIV para benchmarks no comparador.

---

### 2.5. Brapi.dev (B3 Market Data & Proventos)
* **Tipo:** API REST (JSON)
* **Autenticação:** Bearer Token
* **Documentação:** [Brapi Docs](https://brapi.dev/docs)

#### Limites e Endpoints:
* **Cotação Histórica do Ativo:**  
  `GET https://brapi.dev/api/quote/{ticker}?range=5y&interval=1d&token={TOKEN}`
* **Cotação em Lote (Batch):**  
  `GET https://brapi.dev/api/quote/list?tickers=VWRA11,BIJS39,GOLD11,LFTS11&token={TOKEN}`
* **Dividendos e Proventos:**  
  `GET https://brapi.dev/api/quote/{ticker}?dividends=true&token={TOKEN}`

---

### 2.6. Yahoo Finance API (Ativos Globais & Câmbio)
* **Tipo:** API REST não-oficial / Pacote NuGet `YahooFinanceApi`
* **Autenticação:** Nenhuma
* **Tickers Principais:**
  * **S&P 500:** `^GSPC`
  * **NASDAQ-100:** `^IXIC`
  * **Ibovespa:** `^BVSP`
  * **Dólar Comercial:** `USDBRL=X`
  * **Ouro:** `GC=F`

---

### 2.7. Provedores de Contingência / Backup (FMP & HG Brasil)
* **Financial Modeling Prep (FMP):** Usado como plano de contingência caso os feeds diretos de gestoras mudem de formato para ETFs internacionais (Holdings, country allocation e sector allocation via `/api/v3/etf-holder/{ticker}`).
* **HG Brasil Finanças:** Utilizado como fallback para cotações da B3 e câmbio em tempo real caso a Brapi sofra instabilidade.

---

## 3. Resiliência, Rate Limit e Cache Strategy

```text
[Requisição de Backtest / Consulta do Usuário]
               │
               ▼
   ┌──────────────────────┐
   │  Aprovação em Redis? │ ─── (SIM) ───► Retorna em < 5ms (Cache Hit)
   └───────────┬──────────┘
               │ (NÃO)
               ▼
   ┌──────────────────────┐
   │  Existe no Postgres? │ ─── (SIM) ───► Salva no Redis e Retorna
   └───────────┬──────────┘
               │ (NÃO - Dado inexistente ou reprocessamento do Worker)
               ▼
   ┌──────────────────────┐
   │ Ingestão em Worker   │ ─── Utiliza Polly (Retry Exponencial + Circuit Breaker)
   └───────────┬──────────┘
               │
               ▼
   ┌──────────────────────┐
   │ Salva no DB + Redis  │ ─── Disponível instantaneamente para os usuários
   └──────────────────────┘
```

### Regras de Cache no Redis:
1. **Cotações Históricas Fechadas (Dias Anteriores):** Cache com TTL **infinito/30 dias**, pois dados passados não mudam.
2. **Dados do Dia Vigente:** Cache com TTL de **15 minutos** durante o pregão da B3 (10h às 18h).
3. **Indicadores BCB / ANBIMA (CDI/Selic/IPCA/Feriados):** Cache com TTL de **24 horas**.
4. **Holdings & Composição (CVM CDA / Gestoras):** Cache com TTL de **7 dias**.

---

## 4. Variáveis de Ambiente (.env)

Configurações para o **`IndexDesk.Worker`** e **`IndexDesk.Api`**:

```env
# Database & Cache
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=indexdesk_db;Username=indexdesk_user;Password=indexdesk_password;"
ConnectionStrings__Redis="localhost:6379"

# RabbitMQ
ConnectionStrings__RabbitMQ="amqp://guest:guest@localhost:5672/"

# Providers API Keys & Base URLs
Providers__Brapi__ApiKey="SUA_CHAVE_BRAPI_AQUI"
Providers__Brapi__BaseUrl="https://brapi.dev/api/"

Providers__BCB__BaseUrl="https://api.bcb.gov.br/dados/serie/bcdata.sgs."
Providers__CVM__BaseUrl="https://dados.cvm.gov.br/dados/FI/DOC/"
Providers__ANBIMA__BaseUrl="https://www.anbima.com.br/"

# Contingency / Global Providers (Opcionais para Fase 1)
Providers__FMP__ApiKey=""
Providers__HGBrasil__ApiKey=""
```
