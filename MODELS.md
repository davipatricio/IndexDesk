# 🗄️ MODELS.md — Modelo de Dados Relacional & Séries Temporais (PostgreSQL 18 + TimescaleDB)

Este documento define o esquema de banco de dados do **IndexDesk**, estruturado para suportar o monólito modular .NET (`IndexDesk.Api` + `IndexDesk.Worker`), queries de backtest em < 10ms e ingestão massiva de dados da CVM, B3 e BCB.

---

## 1. Diagrama de Relacionamento de Entidades (ERD Conceitual)

```text
┌─────────────────────────┐       1:1       ┌─────────────────────────┐
│         assets          ├─────────────────┤      etf_metadata       │
│  (Tickers, CNPJ, Tipo)  │                 │  (Taxas, Domicílio, PL) │
└────────────┬────────────┘                 └─────────────────────────┘
             │
             ├────── 1:N ────► ┌───────────────────────────────────────┐
             │                 │        etf_holdings (Top 10)          │
             │                 │   (Composição de ações/pesos)         │
             │                 └───────────────────────────────────────┘
             │
             ├────── 1:N ────► ┌───────────────────────────────────────┐
             │                 │   asset_quotes (Timescale Hypertable) │
             │                 │  (Cotações Diárias Ajustadas - B3)    │
             │                 └───────────────────────────────────────┘
             │
             ├────── 1:N ────► ┌───────────────────────────────────────┐
             │                 │ fund_daily_reports (Hypertable CVM)   │
             │                 │  (Cota, PL Histórico, Cotistas)       │
             │                 └───────────────────────────────────────┘
             │
             ├────── 1:N ────► ┌───────────────────────────────────────┐
             │                 │           asset_dividends             │
             │                 │  (Data COM, Data Pagamento, Valor)    │
             │                 └───────────────────────────────────────┘
             │
             └────── 1:1 ────► ┌───────────────────────────────────────┐
                               │        etf_analytics_summary          │
                               │  (Sharpe, Drawdown, Volatilidade 12M) │
                               └───────────────────────────────────────┘

┌────────────────────────────────────────┐  ┌──────────────────────────┐
│  macro_economic_series (Hypertable BCB)│  │     users & auth_tokens  │
│    (CDI, Selic, IPCA, IGP-M Diários)   │  │    (JWT & Refresh Cookie)│
└────────────────────────────────────────┘  └──────────────────────────┘
```

---

## 2. Esquemas e Tabelas Detalhadas

### 2.1. Módulo Cadastral e Metadados (`Modules.MarketData`)

#### `assets` (Catálogo Principal de Ativos)

Tabela central de identificação de ETFs, BDRs de ETFs, Ações e Índices de Mercado.

```sql
CREATE TYPE asset_type_enum AS ENUM ('ETF', 'BDR_ETF', 'STOCK', 'INDEX');

CREATE TABLE assets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticker VARCHAR(20) NOT NULL UNIQUE,          -- Ex: 'VWRA11', 'BOVA11', 'BIJS39', '^BVSP'
    name VARCHAR(255) NOT NULL,                  -- Ex: 'Ishares Ibovespa Fundo De Indice'
    asset_type asset_type_enum NOT NULL,         -- ETF, BDR_ETF, STOCK, INDEX
    cnpj VARCHAR(14) NULL,                       -- Apenas números (14 dígitos), indexado para match CVM
    isin VARCHAR(12) NULL UNIQUE,                -- Padrão ISIN B3
    currency VARCHAR(3) NOT NULL DEFAULT 'BRL',  -- BRL, USD
    tradingview_symbol VARCHAR(50) NULL,         -- Ex: 'BMFBOVESPA:BOVA11', 'BMFBOVESPA:VWRA11'
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_manually_overridden BOOLEAN NOT NULL DEFAULT FALSE, -- Trava editorial: worker não sobrescreve
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_assets_cnpj ON assets(cnpj) WHERE cnpj IS NOT NULL;
CREATE INDEX idx_assets_type ON assets(asset_type);
```

#### `etf_metadata` (Dados Específicos e Fiscais de ETFs/BDRs)

Informações tributárias, institucionais e regulatórias.

```sql
CREATE TYPE tax_domicile_enum AS ENUM ('BRAZIL', 'USA', 'IRELAND_UCITS', 'OTHER');

CREATE TABLE etf_metadata (
    asset_id UUID PRIMARY KEY REFERENCES assets(id) ON DELETE CASCADE,
    benchmark_id UUID NULL REFERENCES assets(id),  -- Benchmark atrelado (ex: Ibovespa, S&P 500)
    manager_name VARCHAR(150) NOT NULL,            -- Gestora: 'BlackRock', 'Investo', 'Itaú'
    admin_name VARCHAR(150) NULL,                  -- Administrador: 'BNY Mellon', 'BTG Pactual'
    management_fee NUMERIC(5, 4) NOT NULL,         -- Taxa adm: 0.0030 = 0.30% a.a.
    performance_fee NUMERIC(5, 4) NULL,            -- Taxa performance
    tax_domicile tax_domicile_enum NOT NULL DEFAULT 'BRAZIL',

    -- Configurações Fiscais e Tributárias (IR / Come-Cotas / Retenção)
    has_come_cotas BOOLEAN NOT NULL DEFAULT FALSE,               -- Maioria dos ETFs de bolsa NÃO tem come-cotas
    income_tax_rate NUMERIC(5, 4) NOT NULL DEFAULT 0.1500,       -- Alíquota Swing Trade padrão (ex: 0.1500 = 15%)
    day_trade_tax_rate NUMERIC(5, 4) NOT NULL DEFAULT 0.2000,    -- Alíquota Day Trade (20%)
    is_tax_withheld_at_source BOOLEAN NOT NULL DEFAULT FALSE,   -- TRUE p/ ETFs Renda Fixa (retido na fonte), FALSE p/ DARF
    has_monthly_sales_tax_exemption BOOLEAN NOT NULL DEFAULT FALSE, -- FALSE para ETFs (não há isenção de 20k/mês)
    dividend_withholding_tax NUMERIC(5, 4) NOT NULL DEFAULT 0.0, -- Retenção na fonte no exterior (0.30 EUA, 0.15 Irlanda)
    reinvests_dividends BOOLEAN NOT NULL DEFAULT TRUE,           -- ETFs BR acumulam proventos
    tax_classification VARCHAR(120) NULL,                        -- Ex: 'Renda Variável (DARF 15%)', 'Renda Fixa PMR > 720d (Fonte 15%)'
    tax_notes TEXT NULL,                                         -- Instruções detalhadas de apuração e compensação

    -- Trava Editorial de Campos Individuais (Admin Backoffice)
    is_manually_overridden BOOLEAN NOT NULL DEFAULT FALSE,
    locked_fields JSONB NOT NULL DEFAULT '[]'::jsonb,            -- Ex: ["management_fee", "manager_name", "tax_classification"]

    inception_date DATE NULL,
    description TEXT NULL,
    website_url VARCHAR(500) NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

#### `etf_holdings` (Composição e Sobreposição / Overlap)

Histórico e composição de ativos detidos pelo fundo (alimenta a Matriz de Overlap).

```sql
CREATE TABLE etf_holdings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    etf_asset_id UUID NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
    holding_ticker VARCHAR(20) NULL,             -- Ticker do ativo subjacente (ex: 'AAPL', 'VALE3')
    holding_name VARCHAR(255) NOT NULL,          -- Nome da empresa/ativo
    weight_percentage NUMERIC(6, 4) NOT NULL,    -- Peso: 0.0850 = 8.50%
    sector VARCHAR(100) NULL,                    -- Ex: 'Information Technology', 'Financials'
    country VARCHAR(3) NULL,                     -- ISO Alpha-3: 'USA', 'BRA', 'JPN'
    as_of_date DATE NOT NULL,                    -- Data da posição
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_etf_holdings_etf_date ON etf_holdings(etf_asset_id, as_of_date DESC);
CREATE INDEX idx_etf_holdings_ticker ON etf_holdings(holding_ticker) WHERE holding_ticker IS NOT NULL;
```

---

### 2.2. Séries Temporais de Alta Performance (TimescaleDB)

#### `asset_quotes` (Cotações Diárias B3 / Yahoo Finance)

Tabela convertida em **Hypertable** para consultas em < 5ms e compressão de séries longas.

```sql
CREATE TABLE asset_quotes (
    asset_id UUID NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
    date DATE NOT NULL,
    open NUMERIC(14, 4) NOT NULL,
    high NUMERIC(14, 4) NOT NULL,
    low NUMERIC(14, 4) NOT NULL,
    close NUMERIC(14, 4) NOT NULL,
    adj_close NUMERIC(14, 4) NOT NULL,           -- Fechamento ajustado para backtests
    volume NUMERIC(18, 2) NOT NULL DEFAULT 0,
    trades_count INTEGER NULL,
    PRIMARY KEY (asset_id, date)
);

-- Ativação do particionamento no TimescaleDB
SELECT create_hypertable('asset_quotes', 'date', chunk_time_interval => INTERVAL '1 year');

-- Compressão colunar para dados com mais de 30 dias
ALTER TABLE asset_quotes SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'asset_id',
    timescaledb.compress_orderby = 'date DESC'
);
SELECT add_compression_policy('asset_quotes', INTERVAL '30 days');
```

#### `fund_daily_reports` (Informe Diário CVM)

Alimentada pelo streaming da CVM via `NpgsqlBinaryImporter` (`COPY`).

```sql
CREATE TABLE fund_daily_reports (
    asset_id UUID NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
    date DATE NOT NULL,
    quota_value NUMERIC(18, 8) NOT NULL,         -- Valor da cota CVM (alta precisão)
    net_asset_value NUMERIC(18, 2) NOT NULL,     -- Patrimônio Líquido (PL)
    shareholders_count INTEGER NOT NULL,         -- Número de cotistas
    net_issuance_redemption NUMERIC(18, 2) NULL, -- Captação líquida do dia
    PRIMARY KEY (asset_id, date)
);

-- Hypertable TimescaleDB
SELECT create_hypertable('fund_daily_reports', 'date', chunk_time_interval => INTERVAL '1 year');

-- Compressão colunar
ALTER TABLE fund_daily_reports SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'asset_id',
    timescaledb.compress_orderby = 'date DESC'
);
SELECT add_compression_policy('fund_daily_reports', INTERVAL '30 days');
```

#### `macro_economic_series` (Séries Econômicas BCB - SGS)

Guarda séries como CDI diário, Selic, IPCA e IGP-M.

```sql
CREATE TABLE macro_economic_series (
    series_code INTEGER NOT NULL,                -- 12 (CDI), 11 (Selic), 433 (IPCA), 189 (IGP-M)
    date DATE NOT NULL,
    value NUMERIC(12, 6) NOT NULL,               -- Taxa/valor no período
    PRIMARY KEY (series_code, date)
);

SELECT create_hypertable('macro_economic_series', 'date', chunk_time_interval => INTERVAL '5 years');
```

#### `market_holidays` (Feriados Nacionais e Dias Não Úteis B3 / ANBIMA)

Essencial para cálculo exato de retorno acumulado (base 252 dias úteis) e CDI acumulado.

```sql
CREATE TABLE market_holidays (
    date DATE PRIMARY KEY,
    description VARCHAR(100) NOT NULL,           -- Ex: 'Carnaval', 'Tiradentes', 'Confraternização Universal'
    exchange VARCHAR(10) NOT NULL DEFAULT 'B3'   -- 'B3', 'US'
);
```

#### `asset_corporate_actions` (Splits, Inplits e Desdobramentos)

Permite ajuste retroativo de séries de preços e evita "degraus" falsos nos gráficos.

```sql
CREATE TYPE corporate_action_enum AS ENUM ('SPLIT', 'INPLIT', 'TICKER_CHANGE', 'BONUS');

CREATE TABLE asset_corporate_actions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id UUID NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
    action_type corporate_action_enum NOT NULL,
    factor NUMERIC(10, 6) NOT NULL,              -- Ex: Split 1:10 = 10.0, Inplit 10:1 = 0.1
    effective_date DATE NOT NULL,                -- Data a partir da qual o preço ajustado se aplica
    notes VARCHAR(255) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_asset_actions ON asset_corporate_actions(asset_id, effective_date DESC);
```

---

### 2.3. Analytics e Métricas Pré-Calculadas (`Modules.Analytics`)

#### `etf_analytics_summary` (Snapshot de Risco e Retorno)

Atualizada após a ingestão para alimentar o catálogo e a página do ETF (`/etf/[ticker]`).

```sql
CREATE TABLE etf_analytics_summary (
    asset_id UUID PRIMARY KEY REFERENCES assets(id) ON DELETE CASCADE,
    as_of_date DATE NOT NULL,
    current_nav NUMERIC(18, 2) NOT NULL,         -- PL atual
    avg_daily_volume_30d NUMERIC(18, 2) NULL,    -- Volume médio diário (R$)
    shareholders_count INTEGER NULL,
    cagr_1y NUMERIC(8, 4) NULL,                  -- Retorno 1 Ano
    cagr_3y NUMERIC(8, 4) NULL,                  -- Retorno 3 Anos
    cagr_5y NUMERIC(8, 4) NULL,                  -- Retorno 5 Anos
    volatility_12m NUMERIC(8, 4) NULL,           -- Volatilidade Anualizada
    sharpe_ratio_12m NUMERIC(8, 4) NULL,         -- Índice Sharpe (vs. CDI)
    max_drawdown_12m NUMERIC(8, 4) NULL,         -- Pior queda 12M
    tracking_error_12m NUMERIC(8, 4) NULL,       -- Desvio padrão vs. Benchmark
    tracking_difference_12m NUMERIC(8, 4) NULL,  -- Diferença de retorno vs. Benchmark
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

#### `asset_dividends` (Proventos e Dividendos)

```sql
CREATE TYPE dividend_type_enum AS ENUM ('DIVIDEND', 'JCP', 'AMORTIZATION', 'OTHER');

CREATE TABLE asset_dividends (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id UUID NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
    declared_date DATE NULL,
    ex_date DATE NOT NULL,                       -- Data COM / EX
    payment_date DATE NULL,                      -- Data de Pagamento
    rate NUMERIC(12, 6) NOT NULL,                -- Valor por cota (R$)
    dividend_type dividend_type_enum NOT NULL DEFAULT 'DIVIDEND',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (asset_id, ex_date, dividend_type, rate)
);

CREATE INDEX idx_asset_dividends_asset_ex ON asset_dividends(asset_id, ex_date DESC);
```

---

### 2.4. Módulo de Autenticação e Usuários (`Modules.Auth`)

```sql
CREATE TYPE user_role_enum AS ENUM ('USER', 'ADMIN', 'SUPERADMIN');

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,         -- Hash Argon2id ou BCrypt
    full_name VARCHAR(150) NOT NULL,
    role user_role_enum NOT NULL DEFAULT 'USER', -- Permite acesso ao painel /admin
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE user_refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(255) NOT NULL UNIQUE,     -- SHA256 do token em cookie HttpOnly
    expires_at TIMESTAMPTZ NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_refresh_tokens_user ON user_refresh_tokens(user_id) WHERE NOT is_revoked;

-- Logs de Auditoria do Painel Admin
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    entity_name VARCHAR(100) NOT NULL,           -- 'assets', 'etf_metadata', 'etf_holdings'
    entity_id VARCHAR(100) NOT NULL,             -- ID do registro alterado
    action VARCHAR(50) NOT NULL,                 -- 'UPDATE', 'CREATE', 'MANUAL_OVERRIDE', 'HOLDINGS_UPLOAD'
    old_data JSONB NULL,
    new_data JSONB NULL,
    ip_address VARCHAR(45) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_entity ON audit_logs(entity_name, entity_id);

-- Logs de Execução e Conflitos de Sincronização (Worker)
CREATE TABLE sync_job_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_name VARCHAR(100) NOT NULL,              -- 'CvmDailySync', 'BcbMacroSync', 'BrapiQuotesSync'
    provider_name VARCHAR(50) NOT NULL,          -- 'CVM', 'BCB', 'BRAPI', 'ISHARES_FEED'
    status VARCHAR(20) NOT NULL,                 -- 'SUCCESS', 'FAILED', 'PARTIAL_WARNING'
    records_processed INTEGER NOT NULL DEFAULT 0,
    records_updated INTEGER NOT NULL DEFAULT 0,
    records_skipped INTEGER NOT NULL DEFAULT 0,  -- Registros ignorados devido a metadata_lock
    error_details TEXT NULL,
    execution_time_ms INTEGER NOT NULL,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX idx_sync_logs_date ON sync_job_logs(started_at DESC);
```

````

---

### 2.5. Simulações de Backtest Salvas (Fase 2 / SEO)

Permite que simulações populares gerem URLs estáticas ou sejam salvas por usuários autenticados.

```sql
CREATE TABLE saved_backtests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL, -- Permite backtests anônimos/públicos
    title VARCHAR(150) NOT NULL,
    initial_amount NUMERIC(14, 2) NOT NULL,
    monthly_contribution NUMERIC(14, 2) NOT NULL DEFAULT 0.0,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    rebalance_frequency VARCHAR(20) NOT NULL,    -- 'NONE', 'MONTHLY', 'SEMI_ANNUAL', 'ANNUAL'
    allocations JSONB NOT NULL,                  -- [{"ticker": "BOVA11", "weight": 0.5}, {"ticker": "IVVB11", "weight": 0.5}]
    metrics_summary JSONB NOT NULL,              -- {"cagr": 0.142, "sharpe": 0.85, "max_drawdown": -0.22, "final_amount": 250000}
    is_public BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_saved_backtests_user ON saved_backtests(user_id) WHERE user_id IS NOT NULL;
````

---

### 2.6. Módulo de Gestão de Carteiras e Transações (Fase 2 & 3 — `Modules.Portfolio`)

Suporta o sistema completo estilo Yahoo Finance / Gorila / Google Finance.

```sql
CREATE TYPE transaction_type_enum AS ENUM (
    'BUY',
    'SELL',
    'TRANSFER_IN',
    'TRANSFER_OUT',
    'DIVIDEND_REINVEST',
    'SPLIT_ADJUSTMENT'
);

CREATE TABLE portfolios (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,                  -- Ex: 'Carteira Principal', 'Aposentadoria FIRE'
    description TEXT NULL,
    base_currency VARCHAR(3) NOT NULL DEFAULT 'BRL',
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_portfolios_user ON portfolios(user_id);

-- Transações de Ativos Cotados (ETFs, BDRs, Ações)
CREATE TABLE portfolio_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    portfolio_id UUID NOT NULL REFERENCES portfolios(id) ON DELETE CASCADE,
    asset_id UUID NOT NULL REFERENCES assets(id),
    transaction_type transaction_type_enum NOT NULL,
    trade_date DATE NOT NULL,
    settlement_date DATE NULL,                   -- Data de liquidação (D+2)
    quantity NUMERIC(14, 6) NOT NULL,            -- Quantidade de cotas negociadas
    unit_price NUMERIC(14, 4) NOT NULL,          -- Preço unitário em BRL
    total_amount NUMERIC(16, 2) NOT NULL,        -- quantity * unit_price
    brokerage_fee NUMERIC(10, 2) NOT NULL DEFAULT 0.0, -- Taxas e emolumentos B3
    broker_name VARCHAR(100) NULL,               -- Ex: 'NuInvest', 'XP', 'Inter', 'BTG'
    notes TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_transactions_portfolio_date ON portfolio_transactions(portfolio_id, trade_date DESC);
CREATE INDEX idx_transactions_asset ON portfolio_transactions(asset_id);

-- Posições de Renda Fixa com Auto-Cálculo de CDI / Selic / IPCA+
CREATE TYPE fixed_income_indexer_enum AS ENUM ('CDI_PERCENT', 'CDI_PLUS', 'SELIC', 'IPCA_PLUS', 'PREFIXED');

CREATE TABLE portfolio_fixed_income_positions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    portfolio_id UUID NOT NULL REFERENCES portfolios(id) ON DELETE CASCADE,
    title VARCHAR(150) NOT NULL,                 -- Ex: 'CDB Banco Inter 110% CDI', 'LCI Itaú 90% CDI'
    issuer_name VARCHAR(150) NULL,               -- Emissor (Banco, Financeira)
    indexer fixed_income_indexer_enum NOT NULL,
    rate_percentage NUMERIC(6, 4) NOT NULL,      -- Ex: 1.1000 = 110% do CDI, 0.0620 = 6.2% a.a.
    spread NUMERIC(6, 4) NULL DEFAULT 0.0,       -- Spread adicional (ex: CDI + 1.5%)
    initial_amount NUMERIC(14, 2) NOT NULL,      -- Valor original investido
    start_date DATE NOT NULL,                    -- Data da aplicação
    maturity_date DATE NULL,                     -- Data de vencimento
    is_tax_exempt BOOLEAN NOT NULL DEFAULT FALSE,-- TRUE para LCI/LCA/CRI/CRA (isento de IR)
    current_value NUMERIC(14, 2) NULL,           -- Valor recalculado diariamente pelo worker
    last_accrual_date DATE NULL,                 -- Última data de capitalização diária
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_fixed_income_portfolio ON portfolio_fixed_income_positions(portfolio_id);

-- Snapshot Consolidado de Posição (Preço Médio, Lucro Não Realizado e TWR)
CREATE TABLE portfolio_positions_summary (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    portfolio_id UUID NOT NULL REFERENCES portfolios(id) ON DELETE CASCADE,
    asset_id UUID NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
    quantity_held NUMERIC(14, 6) NOT NULL DEFAULT 0.0,
    average_price NUMERIC(14, 4) NOT NULL DEFAULT 0.0,  -- Preço Médio ponderado oficial Receita
    total_invested NUMERIC(16, 2) NOT NULL DEFAULT 0.0, -- quantity_held * average_price
    current_value NUMERIC(16, 2) NOT NULL DEFAULT 0.0,  -- quantity_held * current_quote
    unrealized_gain_amount NUMERIC(16, 2) NOT NULL DEFAULT 0.0,
    unrealized_gain_percent NUMERIC(8, 4) NOT NULL DEFAULT 0.0,
    realized_gain_total NUMERIC(16, 2) NOT NULL DEFAULT 0.0, -- Lucro de vendas passadas (para IR)
    last_updated TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (portfolio_id, asset_id)
);
```

---

### 2.7. Calendário Unificado de Eventos do Mercado & Proventos

```sql
CREATE TYPE market_event_type_enum AS ENUM (
    'DIVIDEND_EX',
    'DIVIDEND_PAY',
    'ETF_REBALANCE',
    'COPOM_MEETING',
    'IPCA_RELEASE',
    'CVM_REPORT',
    'CORPORATE_SPLIT'
);

CREATE TABLE market_events_calendar (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    asset_id UUID NULL REFERENCES assets(id) ON DELETE CASCADE, -- NULL para eventos macro (COPOM/IPCA)
    event_type market_event_type_enum NOT NULL,
    event_date DATE NOT NULL,
    title VARCHAR(200) NOT NULL,                 -- Ex: 'Data COM Dividendos DIVO11', 'Decisão Taxa Selic COPOM'
    description TEXT NULL,
    details JSONB NULL,                          -- {"rate": 1.25, "currency": "BRL", "expected_selic": 0.105}
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_market_events_date ON market_events_calendar(event_date ASC);
CREATE INDEX idx_market_events_asset ON market_events_calendar(asset_id) WHERE asset_id IS NOT NULL;
```

---

### 2.8. Hub de Notícias, Fatos Relevantes e Relatórios de Gestoras

Suporta tanto ingestão automática por RSS/APIs quanto publicação manual pelo time de administradores no painel `/admin`.

```sql
CREATE TYPE news_category_enum AS ENUM (
    'MARKET_NEWS',
    'CVM_RELEVANT_FACT',
    'MANAGER_REPORT',
    'DIVIDEND_ANNOUNCEMENT',
    'INDEX_REBALANCE',
    'EDUCATIONAL_ANALYSIS'
);

CREATE TABLE news_articles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    author_id UUID NULL REFERENCES users(id) ON DELETE SET NULL, -- NULL se for ingestão via RSS automático
    slug VARCHAR(255) NOT NULL UNIQUE,           -- Para URL amigável de SEO (/noticias/[slug])
    title VARCHAR(300) NOT NULL,
    summary TEXT NOT NULL,
    content TEXT NOT NULL,                       -- Markdown ou HTML sanitizado
    category news_category_enum NOT NULL DEFAULT 'MARKET_NEWS',
    cover_image_url VARCHAR(500) NULL,
    source_name VARCHAR(100) NULL,               -- 'Investo', 'BlackRock', 'CVM', 'B3', 'Redação IndexDesk'
    source_url VARCHAR(500) NULL,
    is_published BOOLEAN NOT NULL DEFAULT TRUE,
    is_featured BOOLEAN NOT NULL DEFAULT FALSE,  -- Destaque na Home
    published_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_news_published ON news_articles(published_at DESC) WHERE is_published;
CREATE INDEX idx_news_slug ON news_articles(slug);

-- Associação N:N entre Notícias e Ativos (Ex: Uma notícia impacta BOVA11 e SMAL11)
CREATE TABLE article_asset_tags (
    article_id UUID NOT NULL REFERENCES news_articles(id) ON DELETE CASCADE,
    asset_id UUID NOT NULL REFERENCES assets(id) ON DELETE CASCADE,
    PRIMARY KEY (article_id, asset_id)
);

CREATE INDEX idx_article_tags_asset ON article_asset_tags(asset_id);

-- Relatórios Mensais e Lâminas de Gestoras (PDFs e Documentos de Pesquisa)
CREATE TABLE manager_reports (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    uploader_id UUID NULL REFERENCES users(id) ON DELETE SET NULL,
    asset_id UUID NULL REFERENCES assets(id) ON DELETE SET NULL, -- Pode ser específico de um ETF ou geral da gestora
    manager_name VARCHAR(150) NOT NULL,          -- 'BlackRock', 'Investo', 'Itaú'
    report_title VARCHAR(255) NOT NULL,          -- Ex: 'Carta Mensal Investo - Janeiro 2026'
    reference_month DATE NOT NULL,               -- 2026-01-01
    file_url VARCHAR(500) NOT NULL,              -- Link para o PDF (S3 / R2 storage)
    file_size_bytes BIGINT NULL,
    summary TEXT NULL,
    is_published BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_manager_reports_manager ON manager_reports(manager_name, reference_month DESC);
CREATE INDEX idx_manager_reports_asset ON manager_reports(asset_id) WHERE asset_id IS NOT NULL;
```

---

## 3. Diretrizes de Performance para o .NET EF Core / Dapper

1. **Leituras de Backtest & Comparadores:** Utilizar consultas compiladas ou Dapper/Raw SQL com projeção direta em `structs` / `readonly record struct` para zero alocação de memória no cálculo de retorno acumulado.
2. **Ingestão CVM em Massa:** Utilizar a API de baixo nível `NpgsqlBinaryImporter`:
   ```csharp
   await using var writer = await connection.BeginBinaryImportAsync(
       "COPY fund_daily_reports (asset_id, date, quota_value, net_asset_value, shareholders_count, net_issuance_redemption) FROM STDIN (FORMAT BINARY)");
   // Streaming direto linha a linha
   ```
3. **Chaves Primárias & TimescaleDB:** Tabelas particionadas no TimescaleDB **sempre** incluem a coluna de particionamento (`date`) na Chave Primária composta `(asset_id, date)`.
