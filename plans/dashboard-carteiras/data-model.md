# Modelo de Dados — Módulo Portfolio

Extensão de `MODELS.md`. DDL versionado em `docker/init-db/02-portfolio.sql` (aplicação manual no
banco dev enquanto FND-013 não formaliza migrations). Tipos monetários `numeric(20,8)` para suportar
cripto fracionária; moeda da carteira é BRL fixo (decisão §1).

## Tabelas

### `portfolios`

```sql
create table if not exists portfolios (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references users(id) on delete cascade,
  title text not null,
  description text,
  risk_profile text not null default 'moderado'
    check (risk_profile in ('conservador','moderado','arrojado')),
  visibility text not null default 'private'
    check (visibility in ('private','public','link')),
  public_values_mode text not null default 'percent_only'
    check (public_values_mode in ('percent_only','full_values')),
  display_identity text,
  slug text unique,
  share_token_hash text,
  share_expires_at timestamptz,
  target_allocation jsonb,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
create index if not exists ix_portfolios_user on portfolios(user_id);
```

Limite de 3 carteiras por usuário: aplicado em serviço, não constraint.

### `portfolio_transactions`

```sql
create table if not exists portfolio_transactions (
  id uuid primary key default gen_random_uuid(),
  portfolio_id uuid not null references portfolios(id) on delete cascade,
  asset_id uuid references assets(id),
  synthetic_index_code text,
  type text not null check (type in
    ('BUY','SELL','INCOME','CORP_ACTION','TRANSFER_IN','TRANSFER_OUT')),
  broker text not null,
  quantity numeric(20,8),
  unit_price numeric(20,8),
  gross_amount numeric(20,8) not null,
  fees numeric(20,8) not null default 0,
  fx_rate numeric(20,10),
  currency text not null default 'BRL',
  trade_date date not null,
  settlement_date date,
  maturity_date date,
  corp_action jsonb,
  notes text,
  is_amendment boolean not null default false,
  amended_transaction_id uuid references portfolio_transactions(id),
  reversed_by_transaction_id uuid references portfolio_transactions(id),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint ck_pf_tx_target check (
    asset_id is not null or synthetic_index_code is not null
  )
);
create index if not exists ix_pf_tx_portfolio_date
  on portfolio_transactions(portfolio_id, trade_date);
create index if not exists ix_pf_tx_asset on portfolio_transactions(asset_id);
```

- `synthetic_index_code`: `'CDI'`/`'SELIC'` para caixa sintético.
- `fx_rate`: câmbio congelado na data da transação (fonte `fx_rates`) quando `currency <> 'BRL'`.
- Edição lógica: linha original mantém `is_amendment = false`; versão vigente aponta
  `amended_transaction_id` para a anterior. Projeções usam apenas a última versão vigente.

### `portfolio_positions_summary` (projetada, materializada)

```sql
create table if not exists portfolio_positions_summary (
  portfolio_id uuid not null references portfolios(id) on delete cascade,
  asset_id uuid not null references assets(id),
  broker text not null,
  quantity numeric(20,8) not null default 0,
  average_price numeric(20,8) not null default 0,
  invested_amount numeric(20,8) not null default 0,
  current_price numeric(20,8) not null default 0,
  current_value numeric(20,8) not null default 0,
  unrealized_pnl numeric(20,8) not null default 0,
  realized_pnl numeric(20,8) not null default 0,
  income_received numeric(20,8) not null default 0,
  contribution_pct numeric(10,6),
  last_accrual_at timestamptz,
  last_valued_at timestamptz,
  primary key (portfolio_id, asset_id, broker)
);
```

Agrupamento por custódia (`broker`) é decisão do grill §6.

### `portfolio_fixed_income_positions` (M-P3)

```sql
create table if not exists portfolio_fixed_income_positions (
  id uuid primary key default gen_random_uuid(),
  portfolio_id uuid not null references portfolios(id) on delete cascade,
  asset_id uuid references assets(id),
  indexer text not null
    check (indexer in ('CDI_PERCENT','CDI_PLUS','SELIC','IPCA_PLUS','PREFIXED')),
  indexer_rate numeric(12,6) not null,
  principal numeric(20,8) not null,
  start_date date not null,
  maturity_date date not null,
  liquidity text not null default 'maturity',
  accrued_value numeric(20,8) not null default 0,
  last_accrual_date date,
  tax_regime text not null default 'regressive',
  unique (portfolio_id, asset_id)
);
```

### `portfolio_daily_snapshots` (hypertable, M-P2)

```sql
create table if not exists portfolio_daily_snapshots (
  portfolio_id uuid not null references portfolios(id) on delete cascade,
  snapshot_date date not null,
  total_value numeric(20,8) not null,
  invested_amount numeric(20,8) not null,
  allocation jsonb,
  twr_since_inception numeric(14,8),
  created_at timestamptz not null default now(),
  primary key (portfolio_id, snapshot_date)
);
-- select create_hypertable('portfolio_daily_snapshots','snapshot_date',
--   if_not_exists => true, migrate_data => true);
```

### `portfolio_goals` e `portfolio_tax_ledger` (M-P4/M-P5)

Estruturas completas em [milestones/m-p4-tax.md](milestones/m-p4-tax.md) e
[milestones/m-p5-sharing-goals-export.md](milestones/m-p5-sharing-goals-export.md).

## Fontes já existentes (não duplicar)

- Preços: `assets`, `asset_quotes`
- Proventos: `asset_dividends`
- Indexadores: `macro_economic_series` (CDI=11/12, Selic, IPCA=433)
- Câmbio: `fx_rates` (USD/EUR/BTC-BRL)
