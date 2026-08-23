# `PROVIDERS.md` — Guia de Provedores de Dados e Integrações

Este documento especifica todas as fontes de dados e APIs externas utilizadas pelo **IndexDesk**, incluindo custos, limites de taxa (_rate limits_), métodos de acesso e a estratégia de resiliência do sistema.

---

## 1. Resumo Consolidado dos Provedores

| Provedor                                                   | Categoria               |           Custo            |           Rate Limit            | Função Principal no IndexDesk                                       |
| :--------------------------------------------------------- | :---------------------- | :------------------------: | :-----------------------------: | :------------------------------------------------------------------ |
| **BCB (Banco Central - SGS)**                              | Macroeconomia           |        **Gratuito**        |          ~100 req/min           | Histórico de CDI, Selic, IPCA e IGP-M.                              |
| **CVM (Informe Diário)**                                   | Regulatório / PL        |        **Gratuito**        |  Sem limite rígido (HTTP/FTP)   | Cota, PL histórico (12M), Cotistas diários e Gestoras.              |
| **CVM (CDA - Carteira Mensal)**                            | Holdings Nacionais      |        **Gratuito**        | Sem limite rígido (HTTP mensal) | **Composição e ativos dos fundos/ETFs da B3** (Holdings & Overlap). |
| **B3 (Portal de Dados Abertos)**                           | Mercado Oficial         |        **Gratuito**        |  Download programático diário   | Carteiras teóricas de índices (IBOV, SMLL, IDIV) e códigos ISIN.    |
| **ANBIMA (Dados Abertos)**                                 | Feriados & Renda Fixa   |        **Gratuito**        |        Sem limite rígido        | Feriados bancários (regra 252 dias úteis) e índices IMA-B / IDA.    |
| **Feeds Diretos de Gestoras (iShares, Vanguard, Investo)** | Holdings Globais/Locais |        **Gratuito**        |    Download de CSVs públicos    | Holdings diários oficiais dos ETFs (Top 10, pesos, setores).        |
| **Brapi.dev**                                              | B3 Market Data          |  **Free cycle 15k req**    |       15.000 req/ciclo · dados +30 min · 1 ativo/req       | Cotações diárias ajustadas e dividendos de ETFs e BDRs na B3.       |
| **Yahoo Finance API**                                      | Benchmarks Globais      | **Gratuito (Não oficial)** |       ~2.000 req/IP/hora        | Índices globais (`^BVSP`, `^GSPC`, `^IXIC`) e Câmbio (`USDBRL=X`).  |
| **Financial Modeling Prep (FMP) / EODHD**                  | Dados Globais (Backup)  | **Freemium / $19-$29/mês** |      250 a 10.000 req/dia       | Holdings e setores de ETFs UCITS (Irlanda) e ETFs dos EUA.          |
| **HG Brasil Finanças**                                     | B3 Backup Data          |  **Pago (não contratado)** |       —                            | Provedor nacional de contingência para cotações e moedas. Client mantido no código; **sem chave por ora** — ativar só se Brapi+Yahoo provarem insuficiente. |

---

## 2. Detalhamento Técnico por Provedor

### 2.1. Banco Central do Brasil (BCB - SGS)

- **Tipo:** API REST (JSON)
- **Autenticação:** Nenhuma (Pública)
- **Documentação:** [BCB SGS API](https://dadosabertos.bcb.gov.br/)

#### Principais Séries Cadastradas:

| Indicador        | Código da Série | Frequência | Endpoint BCB                                                           |
| :--------------- | :-------------: | :--------: | :--------------------------------------------------------------------- |
| **CDI Diário**   |      `12`       |   Diária   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.12/dados?formato=json`  |
| **Selic Diária** |      `11`       |   Diária   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.11/dados?formato=json`  |
| **IPCA Mensal**  |      `433`      |   Mensal   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.433/dados?formato=json` |
| **IGP-M Mensal** |      `189`      |   Mensal   | `https://api.bcb.gov.br/dados/serie/bcdata.sgs.189/dados?formato=json` |

#### Estratégia de Ingestão:

- **Cron Job:** Ingestão diária às **23:00 UTC** no `IndexDesk.Worker` via Quartz.NET.
- **Filtro de Data:** Query string `?dataInicial=dd/mm/aaaa&dataFinal=dd/mm/aaaa` para evitar carregar o histórico completo.

---

### 2.2. CVM — Portal de Dados Abertos (Informes + Composição CDA)

- **Tipo:** Arquivos CSV/Zip (HTTP estático)
- **Autenticação:** Nenhuma (Pública)
- **Frequência de Atualização:** Diária (D+1) e Mensal

#### Arquivos Utilizados:

1. **Informe Diário (`/INFORME_DIARIO/DADOS/inf_diario_fi_YYYYMM.zip`):**
   - **Campos:** `CNPJ_Fundo`, `DT_COMPTC`, `VL_QUOTA`, `VL_PATRIM_LIQ`, `NR_COTST`.
   - **Ingestão:** Worker diário às **04:00 AM** com streaming via `CsvHelper` + `NpgsqlBinaryImporter` (`COPY`).
2. **Composição e Diversificação das Aplicações (`/CDA/DADOS/cda_fi_YYYYMM.zip`):**
   - **Campos:** `CNPJ_Fundo`, `TP_APLIC` (Tipo de Ativo), `CD_ATIVO` (Ticker/Código), `QT_TIT` (Quantidade), `VL_MERC` (Valor de Mercado).
   - **Função:** Alimenta a tabela `etf_holdings` para a **Matriz de Overlap** e Top 10 Holdings dos ETFs nacionais.
3. **Cadastro Geral (`/CAD/DADOS/cad_fi.csv`):**
   - **Campos:** `CNPJ_Fundo`, `DENOM_SOCIAL`, `G_GESTOR`, `ADMIN`, `TAXA_ADM`.

---

### 2.3. Feeds Oficiais de Gestoras (Holdings Diários)

Muitos emissores disponibilizam arquivos diários de composição de carteira:

- **BlackRock (iShares Brasil):** Exportação CSV pública por produto (ex: `BOVA11`, `IVVB11`, `SMAL11`, `ACWI`).
  - _Padrão de URL:_ `https://www.ishares.com/br/produtos/[ID]/[ticker]/1495093766861.ajax?fileType=csv&fileName=[ticker]_holdings`
- **Investo ETF:** Dados e composição de ETFs globais listados na B3 (`WRLD11`, `BDEF11`, `ALUG11`).
- **Vanguard (ETFs US/UCITS):** Composição detalhada de `VOO`, `VTI`, `VWRA`.
- **Ingestão:** O `IndexDesk.Worker` roda um job semanal de harmonização de holdings para manter a tabela `etf_holdings` sempre atualizada.

---

### 2.4. ANBIMA & B3 (Feriados e Índices)

1. **Feriados Bancários ANBIMA:**
   - Lista oficial de feriados nacionais até 2099 utilizada para preencher a tabela `market_holidays` e calcular o CDI acumulado em dias úteis (base 252).
2. **Índices Teva e ANBIMA:**
   - Séries históricas de índices de Renda Fixa (`IMA-B`, `IMA-B 5+`, `IRF-M`, `IDA`) e índices temáticos Teva.
3. **B3 Market Data Aberto (`dados.b3.com.br`):**
   - Composição das carteiras teóricas do Ibovespa, IFIX, SMLL e IDIV para benchmarks no comparador.

---

### 2.5. Brapi.dev (B3 Market Data & Proventos)

- **Tipo:** API REST (JSON)
- **Autenticação:** Bearer Token
- **Documentação:** [Brapi Docs](https://brapi.dev/docs)

#### Limites e Endpoints:

- **Cotação Histórica do Ativo:**  
  `GET https://brapi.dev/api/quote/{ticker}?range=5y&interval=1d&token={TOKEN}`
- **Cotação em Lote (Batch):**  
  `GET https://brapi.dev/api/quote/list?tickers=VWRA11,BIJS39,GOLD11,LFTS11&token={TOKEN}`
- **Dividendos e Proventos:**  
  `GET https://brapi.dev/api/quote/{ticker}?dividends=true&token={TOKEN}`

#### Plano atual (contratado 2026-08) e política de consumo

| Parâmetro | Valor |
| :--- | :--- |
| Cota do ciclo | **15.000 requisições** (ciclo mensal da conta) |
| Custo por 1.000 req | Grátis (plano atual) |
| Atualização dos dados | **A cada 30 minutos** (delay upstream) |
| Granularidade | **1 ativo por requisição** — cada ticker consumido conta como 1 req |

**Regras para não estourar a cota:**

1. **Cadência mínima de polling = 30 min.** O upstream não tem dado mais fresco que isso;
   pollar intraday abaixo disso queima cota sem ganho. Padrão do Worker continua **EOD
   pós-fechamento**; coleta intraday (se um dia existir) nunca abaixo de 30 min.
2. **Contador de consumo no Redis:** chave mensal `providers:brapi:ciclo:{YYYYMM}`
   (`INCR` por requisição feita, incluindo cada ticker do batch). Sem leitura de saldo
   exposta pela API, rastramos o que gastamos.
3. **Guardrails por consumo do ciclo:**
   - `< 80%` — operação normal.
   - `≥ 80% (12k)` — pausa backfills e jobs não-críticos; mantém só EOD diário.
   - `≥ 90% (13,5k)` — modo sobrevivência: nenhuma chamada Brapi fora do EOD essencial;
     fallback assume o que der.
4. **Orçamento de referência:** catálogo de 200 ativos × 21 dias úteis ≈ 4.200 req/mês
   + proventos (~1 req/ativo/semana ≈ 800) → ~5k/mês em regime, sobrando ~10k do ciclo
   para novos ativos e backfill histórico (backfill de 1 ticker com `range=5y` = 1 req,
   é barato; re-poll desnecessário é caro).
5. **Idempotência vale cota:** antes de chamar, checar se o dia-alvo já está persistido
   (`quotes` upsert por `(ticker, date)`); nunca repetir ingestão concluída.

**Balanceamento entre providers (presente e futuro):**

- **Brapi** = exclusivo para cotações/proventos de **B3** (ETFs, BDRs, FIIs). Não usar para
  benchmarks globais.
- **Yahoo Finance** (free, ~2k req/h/IP) = benchmarks globais (`^GSPC`, `^IXIC`, `^BVSP`),
  câmbio (`USDBRL=X`). Overflow natural se Brapi entrar em guardrail e o dado existir lá
  (tickers `.SA`).
- **HG Brasil** = fallback de câmbio/cotações B3 quando Brapi sofre instabilidade ou ciclo
  estourado — **porém é plano pago e não está contratado**: permanece desativado (client no
  código, sem key). Enquanto isso, o papel de fallback recai sobre Yahoo (`.SA`) e dado local.
- **FMP** = contingência para holdings/alocação de ETFs globais UCITS (client ainda não escrito).
- Regra geral: **dado local-first primeiro** — provider só roda via Worker agendado; usuário
  nunca dispara chamada externa.

---

### 2.6. Yahoo Finance API (Ativos Globais & Câmbio)

- **Tipo:** API REST não-oficial / Pacote NuGet `YahooFinanceApi`
- **Autenticação:** Nenhuma
- **Tickers Principais:**
  - **S&P 500:** `^GSPC`
  - **NASDAQ-100:** `^IXIC`
  - **Ibovespa:** `^BVSP`
  - **Dólar Comercial:** `USDBRL=X`
  - **Ouro:** `GC=F`

---

### 2.7. Provedores de Contingência / Backup (FMP & HG Brasil)

- **Financial Modeling Prep (FMP):** Usado como plano de contingência caso os feeds diretos de gestoras mudem de formato para ETFs internacionais (Holdings, country allocation e sector allocation via `/api/v3/etf-holder/{ticker}`).
- **HG Brasil Finanças:** fallback para cotações da B3 e câmbio caso Brapi sofra instabilidade.
  **Plano pago — não contratado por ora.** O `HgBrasilClient` permanece no código (mantido e
  compilando), mas sem `ApiKey` configurada ele não deve ser acionado pelo Worker: tratar como
  contingência de último recurso, ativação futura mediante decisão explícita de custo.

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
# HG Brasil: plano pago — SEM chave por ora. Client mantido no código, desativado.
Providers__FMP__ApiKey=""
# Providers__HGBrasil__ApiKey=""
```
