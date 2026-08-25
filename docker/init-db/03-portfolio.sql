-- Portfolio module v1 (M-P1) — carteiras e transações.
-- Aplicação manual no banco dev enquanto FND-013 não formaliza migrations:
--   docker exec -i indexdesk-timescaledb psql -U indexdesk -d indexdesk < docker/init-db/03-portfolio.sql
-- Convenção do repo: tabela snake_case, colunas PascalCase (fiel ao mapeamento EF).
-- Idempotente: seguro re-executar.

drop table if exists portfolio_transactions cascade;
drop table if exists portfolio_positions_summary cascade;
drop table if exists portfolios cascade;

create table portfolios (
  "Id" uuid primary key default gen_random_uuid(),
  "UserId" uuid not null references users("Id") on delete cascade,
  "Title" varchar(120) not null,
  "Description" varchar(1000),
  "RiskProfile" varchar(20) not null default 'moderado'
    check ("RiskProfile" in ('conservador', 'moderado', 'arrojado')),
  "Visibility" varchar(10) not null default 'private'
    check ("Visibility" in ('private', 'public', 'link')),
  "PublicValuesMode" varchar(20) not null default 'percent_only'
    check ("PublicValuesMode" in ('percent_only', 'full_values')),
  "DisplayIdentity" varchar(80),
  "Slug" varchar(80),
  "ShareTokenHash" varchar(128),
  "ShareExpiresAt" timestamptz,
  "TargetAllocationJson" varchar(2000),
  "CreatedAt" timestamptz not null default now(),
  "UpdatedAt" timestamptz not null default now()
);
create index "IX_portfolios_UserId" on portfolios("UserId");
create unique index "IX_portfolios_Slug" on portfolios("Slug") where "Slug" is not null;

create table portfolio_transactions (
  "Id" uuid primary key default gen_random_uuid(),
  "PortfolioId" uuid not null references portfolios("Id") on delete cascade,
  "AssetId" uuid references assets("Id"),
  "SyntheticIndexCode" varchar(10),
  "Type" varchar(20) not null
    check ("Type" in
      ('BUY', 'SELL', 'INCOME', 'CORP_ACTION', 'TRANSFER_IN', 'TRANSFER_OUT')),
  "Broker" varchar(80) not null,
  "Quantity" numeric(20, 8),
  "UnitPrice" numeric(20, 8),
  "GrossAmount" numeric(20, 8) not null,
  "Fees" numeric(20, 8) not null default 0,
  "FxRate" numeric(20, 10),
  "Currency" varchar(3) not null default 'BRL',
  "TradeDate" date not null,
  "MaturityDate" date,
  "CorpActionJson" text,
  "Notes" varchar(1000),
  "IsAmendment" boolean not null default false,
  "AmendedTransactionId" uuid references portfolio_transactions("Id"),
  "ReversedByTransactionId" uuid references portfolio_transactions("Id"),
  "CreatedAt" timestamptz not null default now(),
  "UpdatedAt" timestamptz not null default now(),
  constraint ck_pf_tx_target check ("AssetId" is not null or "SyntheticIndexCode" is not null)
);
create index "IX_portfolio_transactions_PortfolioId_TradeDate"
  on portfolio_transactions("PortfolioId", "TradeDate");
create index "IX_portfolio_transactions_AssetId" on portfolio_transactions("AssetId");
create index "IX_portfolio_transactions_AmendedTransactionId"
  on portfolio_transactions("AmendedTransactionId");

-- Projetada pelo PositionProjector; M-P1 calcula on-demand (tabela pronta p/ materializar).
create table portfolio_positions_summary (
  "PortfolioId" uuid not null references portfolios("Id") on delete cascade,
  "AssetId" uuid not null references assets("Id"),
  "Broker" varchar(80) not null,
  "Quantity" numeric(20, 8) not null default 0,
  "AveragePrice" numeric(20, 8) not null default 0,
  "InvestedAmount" numeric(20, 8) not null default 0,
  "CurrentPrice" numeric(20, 8) not null default 0,
  "CurrentValue" numeric(20, 8) not null default 0,
  "UnrealizedPnl" numeric(20, 8) not null default 0,
  "RealizedPnl" numeric(20, 8) not null default 0,
  "IncomeReceived" numeric(20, 8) not null default 0,
  "ContributionPct" numeric(10, 6),
  "LastAccrualAt" timestamptz,
  "LastValuedAt" timestamptz,
  primary key ("PortfolioId", "AssetId", "Broker")
);
