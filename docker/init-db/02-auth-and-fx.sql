-- Pré-requisitos do módulo Portfolio ausentes no banco dev (container recriado sem o DDL
-- manual de auth/FX — ver FND-013 e plans/dashboard-carteiras/README.md).
-- DDL fiel ao mapeamento EF de IndexDeskDbContext: tabela em snake_case, colunas PascalCase
-- (convenção do repo, ex.: tabela "assets" com coluna "Id").
-- Idempotente: seguro re-executar.

-- ---------- auth (módulo Auth) ----------

drop table if exists refresh_tokens cascade;
drop table if exists user_roles cascade;
drop table if exists role_permissions cascade;
drop table if exists permissions cascade;
drop table if exists roles cascade;
drop table if exists users cascade;

create table users (
  "Id" uuid primary key default gen_random_uuid(),
  "Email" varchar(255) not null,
  "PasswordHash" varchar(500) not null,
  "FullName" varchar(200) not null,
  "IsActive" boolean not null default true,
  "EmailVerified" boolean not null default false,
  "LastLoginAt" timestamptz,
  "CreatedAt" timestamptz not null default now(),
  "UpdatedAt" timestamptz not null default now()
);
create unique index "IX_users_Email" on users("Email");

create table roles (
  "Id" uuid primary key,
  "Name" varchar(50) not null,
  "Description" varchar(255) not null,
  "CreatedAt" timestamptz not null default now()
);
create unique index "IX_roles_Name" on roles("Name");

create table permissions (
  "Id" uuid primary key,
  "Slug" varchar(100) not null,
  "Description" varchar(255) not null,
  "Category" varchar(50) not null
);
create unique index "IX_permissions_Slug" on permissions("Slug");

create table user_roles (
  "UserId" uuid not null references users("Id") on delete cascade,
  "RoleId" uuid not null references roles("Id") on delete cascade,
  "AssignedAt" timestamptz not null default now(),
  primary key ("UserId", "RoleId")
);

create table role_permissions (
  "RoleId" uuid not null references roles("Id") on delete cascade,
  "PermissionId" uuid not null references permissions("Id") on delete cascade,
  "AssignedAt" timestamptz not null default now(),
  primary key ("RoleId", "PermissionId")
);

create table refresh_tokens (
  "Id" uuid primary key default gen_random_uuid(),
  "UserId" uuid not null references users("Id") on delete cascade,
  "TokenHash" varchar(128) not null,
  "ExpiresAt" timestamptz not null,
  "CreatedAt" timestamptz not null default now(),
  "RevokedAt" timestamptz,
  "ReplacedByTokenHash" varchar(128),
  "CreatedByIp" varchar(45),
  "UserAgent" varchar(255)
);
create unique index "IX_refresh_tokens_TokenHash" on refresh_tokens("TokenHash");
create index "IX_refresh_tokens_UserId" on refresh_tokens("UserId");

-- Seed RBAC (mesmos GUIDs fixos de SeedRbac no IndexDeskDbContext)
insert into roles ("Id", "Name", "Description", "CreatedAt")
values
  ('11111111-1111-1111-1111-111111111111', 'Admin', 'Administrator with full system access', '2025-01-01T00:00:00Z'),
  ('22222222-2222-2222-2222-222222222222', 'Pro', 'Pro tier subscriber with advanced analytics and unlimited backtests', '2025-01-01T00:00:00Z'),
  ('33333333-3333-3333-3333-333333333333', 'User', 'Standard registered user', '2025-01-01T00:00:00Z')
on conflict ("Id") do nothing;

insert into permissions ("Id", "Slug", "Description", "Category")
values
  ('a0000001-0000-0000-0000-000000000001', 'catalog:read', 'Read asset catalog and quotes', 'MarketData'),
  ('a0000001-0000-0000-0000-000000000002', 'analytics:read', 'Access basic analytics and calculators', 'Analytics'),
  ('a0000001-0000-0000-0000-000000000003', 'backtest:unlimited', 'Run unlimited portfolio backtests', 'Analytics'),
  ('a0000001-0000-0000-0000-000000000004', 'portfolio:read', 'Read user portfolio and positions', 'Portfolio'),
  ('a0000001-0000-0000-0000-000000000005', 'portfolio:write', 'Create and update user portfolio and transactions', 'Portfolio'),
  ('a0000001-0000-0000-0000-000000000006', 'admin:access', 'Access admin portal and backoffice', 'Admin'),
  ('a0000001-0000-0000-0000-000000000007', 'assets:write', 'Curate assets and override metadata', 'MarketData'),
  ('a0000001-0000-0000-0000-000000000008', 'reports:publish', 'Publish news and manager reports', 'Admin'),
  ('a0000001-0000-0000-0000-000000000009', 'users:manage', 'Manage users and role assignments', 'Admin')
on conflict ("Id") do nothing;

insert into role_permissions ("RoleId", "PermissionId", "AssignedAt")
select r."Id", p."Id", '2025-01-01T00:00:00Z'
from (values
  ('Admin', 'catalog:read'),
  ('Admin', 'analytics:read'),
  ('Admin', 'backtest:unlimited'),
  ('Admin', 'portfolio:read'),
  ('Admin', 'portfolio:write'),
  ('Admin', 'admin:access'),
  ('Admin', 'assets:write'),
  ('Admin', 'reports:publish'),
  ('Admin', 'users:manage'),
  ('Pro', 'catalog:read'),
  ('Pro', 'analytics:read'),
  ('Pro', 'backtest:unlimited'),
  ('Pro', 'portfolio:read'),
  ('Pro', 'portfolio:write'),
  ('User', 'catalog:read'),
  ('User', 'analytics:read'),
  ('User', 'portfolio:read'),
  ('User', 'portfolio:write')
) as rp(role_name, perm_slug)
join roles r on r."Name" = rp.role_name
join permissions p on p."Slug" = rp.perm_slug
on conflict do nothing;

-- ---------- fx_rates (provider-sync Fase 3; exigida pelo valuation do Portfolio) ----------

drop table if exists fx_rates cascade;
create table fx_rates (
  "Pair" varchar(10) not null,
  "Date" date not null,
  "Bid" numeric(18, 8) not null,
  "Ask" numeric(18, 8) not null,
  "SourceProvider" varchar(50) not null default 'AwesomeApi',
  "CreatedAt" timestamptz not null default now(),
  primary key ("Pair", "Date")
);
create index "IX_fx_rates_Date" on fx_rates("Date");
