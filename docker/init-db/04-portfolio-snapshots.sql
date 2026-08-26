-- Portfolio module v1.1 (M-P2) — snapshots diários do patrimônio (hypertable TimescaleDB).
--   docker exec -i indexdesk-timescaledb psql -U indexdesk -d indexdesk < docker/init-db/04-portfolio-snapshots.sql
-- Idempotente: seguro re-executar.

create table if not exists portfolio_daily_snapshots (
  "PortfolioId" uuid not null references portfolios("Id") on delete cascade,
  "SnapshotDate" date not null,
  "TotalValue" numeric(20, 8) not null default 0,
  "InvestedAmount" numeric(20, 8) not null default 0,
  "AllocationJson" jsonb,
  "TwrSinceInception" numeric(14, 8),
  "CreatedAt" timestamptz not null default now(),
  primary key ("PortfolioId", "SnapshotDate")
);

-- Converte em hypertable quando a extensão timescaledb estiver disponível (dev usa imagem
-- timescale/timescaledb; bancos sem a extensão ficam com tabela comum).
do $$
begin
  if exists (select 1 from pg_extension where extname = 'timescaledb') then
    perform create_hypertable(
      'portfolio_daily_snapshots',
      'SnapshotDate',
      if_not_exists => true,
      migrate_data => true
    );
  end if;
end $$;
