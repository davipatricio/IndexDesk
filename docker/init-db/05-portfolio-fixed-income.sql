-- Portfolio module v1.2 (M-P3 kickoff) — parâmetros de renda fixa por posição.
--   docker exec -i indexdesk-timescaledb psql -U indexdesk -d indexdesk < docker/init-db/05-portfolio-fixed-income.sql
-- Idempotente: seguro re-executar.

create table if not exists portfolio_fixed_income_positions (
  "Id" uuid primary key default gen_random_uuid(),
  "PortfolioId" uuid not null references portfolios("Id") on delete cascade,
  "AssetId" uuid references assets("Id"),
  "SyntheticIndexCode" varchar(10),
  "Indexer" varchar(20) not null
    check ("Indexer" in ('CDI_PERCENT', 'CDI_PLUS', 'SELIC', 'IPCA_PLUS', 'PREFIXED')),
  "IndexerRate" numeric(12, 6) not null,
  "Principal" numeric(20, 8) not null,
  "StartDate" date not null,
  "MaturityDate" date not null,
  "Liquidity" varchar(30) not null default 'maturity',
  "TaxRegime" varchar(20) not null default 'regressive',
  "AccruedValue" numeric(20, 8) not null default 0,
  "LastAccrualDate" date,
  "CreatedAt" timestamptz not null default now(),
  "UpdatedAt" timestamptz not null default now()
);
create index if not exists ix_pf_fi_portfolio_asset
  on portfolio_fixed_income_positions("PortfolioId", "AssetId");
