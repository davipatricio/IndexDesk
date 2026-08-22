# Modelo de Dados — PostgreSQL 18 + TimescaleDB

Fonte integral com DDL completo: `MODELS.md`. Resumo para navegação rápida.

## Tabelas por domínio

### MarketData / cadastro
- **`assets`** — catálogo central: `ticker` UNIQUE, `asset_type` enum (`ETF|BDR_ETF|STOCK|INDEX`), `cnpj` (14 dígitos,
  indexado p/ match CVM), `isin`, `currency`, `tradingview_symbol`, `is_manually_overridden`.
- **`etf_metadata`** (1:1 assets) — gestora/admin, `management_fee` NUMERIC(5,4) (0.0030=0.30% aa),
  `tax_domicile` enum (`BRAZIL|USA|IRELAND_UCITS|OTHER`), campos fiscais: `has_come_cotas`,
  `income_tax_rate` (default 0.15), `day_trade_tax_rate` (0.20), `is_tax_withheld_at_source`,
  `has_monthly_sales_tax_exemption` (**FALSE p/ ETFs**), `dividend_withholding_tax` (0.30 EUA / 0.15 Irlanda),
  `reinvests_dividends`, `tax_classification`, `tax_notes`; trava editorial por campo: `locked_fields` JSONB
  (ex.: `["management_fee","manager_name"]`) + `is_manually_overridden`.
- **`etf_holdings`** — composição 1:N: `holding_ticker/name`, `weight_percentage` (0.0850=8.50%), sector,
  country ISO3, `as_of_date` (overlap usa a data mais recente).
- **`asset_dividends`** — `ex_date`, `payment_date`, `rate`, tipo enum (`DIVIDEND|JCP|AMORTIZATION|OTHER`).

### Séries temporais (hypertables TimescaleDB)
- **`asset_quotes`** PK `(asset_id,date)` — OHLCV + `adj_close` (**sempre usar adj_close em backtests**).
  Hypertable chunk 1 ano; compressão >30 dias (segmentby asset_id, orderby date DESC).
- **`fund_daily_reports`** PK `(asset_id,date)` — CVM informe: `quota_value` (18,8), `net_asset_value`,
  `shareholders_count`, `net_issuance_redemption` (captação líquida). Mesma política de compressão.
- **`macro_economic_series`** PK `(series_code,date)` — códigos SGS: **12 CDI, 11 Selic, 433 IPCA, 189 IGP-M**;
  chunk 5 anos.
- **`market_holidays`** — feriados ANBIMA/B3 até 2099 → base para dias úteis (252) e CDI acumulado.
- **`asset_corporate_actions`** — enum `SPLIT|INPLIT|TICKER_CHANGE|BONUS` + `factor` (split 1:10 = 10.0)
  + `effective_date`; evita "degraus" falsos nas séries.

### Analytics
- **`etf_analytics_summary`** (1:1) — snapshot pós-ingestão: PL, volume médio 30d, cotistas, CAGR 1/3/5y,
  vol 12M, Sharpe 12M (vs CDI), max drawdown 12M, tracking error/difference 12M.

### Auth & operação
- **`users`** (`role`: USER|ADMIN|SUPERADMIN, hash Argon2id/BCrypt) · **`user_refresh_tokens`** (hash SHA256,
  revogável) · **`audit_logs`** (entity, action, old/new JSONB, ip) · **`sync_job_logs`** (job/provider/status
  SUCCESS|FAILED|PARTIAL_WARNING, records processed/updated/**skipped** [metadata_lock], duração).

### Fase 02/03 (reservado, não implementar antes da fase)
- **`saved_backtests`** — allocations/metrics JSONB, público/anônimo permitido.
- **`portfolios`**, **`portfolio_transactions`** (BUY|SELL|TRANSFER_IN|TRANSFER_OUT|DIVIDEND_REINVEST|SPLIT_ADJUSTMENT,
  custos em `brokerage_fee`), **`portfolio_fixed_income_positions`** (indexer enum `CDI_PERCENT|CDI_PLUS|SELIC|IPCA_PLUS|PREFIXED`,
  `rate_percentage` 1.10=110% CDI, `is_tax_exempt` LCI/LCA, accrual diário via worker),
  **`portfolio_positions_summary`** (PM ponderado Receita, lucro realizado/não realizado).
- **`market_events_calendar`** (COPOM, IPCA release, proventos, rebalance índices) ·
  **`news_articles`** + **`article_asset_tags`** N:N + **`manager_reports`** (PDFs/lâminas).

## Regras de performance (EF Core/Dapper)

1. Leitura de backtest/comparador: queries compiladas ou Dapper/raw SQL projetando em structs sem alocação.
2. Ingestão em massa: `NpgsqlBinaryImporter` COPY binário streaming linha a linha (nunca carregar tudo em RAM).
3. Hypertable: coluna `date` **sempre** entra na PK composta `(asset_id, date)`; explorar chunk exclusion nos filtros.
4. Escritas idempotentes: re-execução atualiza (upsert/COPY), nunca duplica.
