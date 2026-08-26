-- Portfolio module M-P5 — multi-metas por carteira (portfolio_goals).
--   docker exec -i indexdesk-timescaledb psql -U indexdesk -d indexdesk < docker/init-db/06-goals.sql
-- Idempotente: seguro re-executar.

create table if not exists portfolio_goals (
  "Id" uuid primary key default gen_random_uuid(),
  "PortfolioId" uuid not null references portfolios("Id") on delete cascade,
  "Kind" varchar(20) not null
    check ("Kind" in ('TARGET_AMOUNT', 'TARGET_RETURN_PCT', 'TARGET_DATE')),
  "TargetValue" numeric(20, 8),
  "TargetPct" numeric(10, 4),
  "TargetDate" date,
  "MonthlyContribution" numeric(20, 8),
  "AssumedAnnualRate" numeric(10, 4),
  "Status" varchar(20) not null default 'active'
    check ("Status" in ('active', 'achieved', 'cancelled')),
  "CreatedAt" timestamptz not null default now(),
  constraint ck_portfolio_goals_kind_target check (
    ("Kind" = 'TARGET_AMOUNT' and "TargetValue" is not null)
    or ("Kind" = 'TARGET_RETURN_PCT' and "TargetPct" is not null)
    or ("Kind" = 'TARGET_DATE' and "TargetDate" is not null)
  )
);
create index if not exists ix_pg_portfolio_status
  on portfolio_goals("PortfolioId", "Status");
