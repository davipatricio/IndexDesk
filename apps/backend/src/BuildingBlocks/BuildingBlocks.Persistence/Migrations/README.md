# EF Core Migrations (FND-013)

Two migrations, both idempotent, both safe to replay:

| Migration | Purpose |
| :--- | :--- |
| `20260905125533_InitialCreate` | Creates every table, FK, unique index, and seeds RBAC roles/permissions. Emits `CREATE EXTENSION IF NOT EXISTS` for `pgcrypto` and `uuid-ossp`. |
| `20260905130000_AddTimescaleAndSeed` | Converts `asset_quotes`, `macro_economic_series`, `portfolio_daily_snapshots` into TimescaleDB hypertables (1 y / 5 y / 1 y chunks) when the extension is present, and seeds `market_holidays` for 2025–2026 with `ON CONFLICT DO NOTHING`. |

## Hosts that apply migrations

Both `IndexDesk.Api` and `IndexDesk.Worker` call `DatabaseInitializer.MigrateAsync(services)` at startup, before serving traffic / running Quartz jobs. The call is idempotent — once `__EFMigrationsHistory` is up to date, re-running is a no-op.

Per-job calls inside `Modules/IndexDesk.Modules.MarketData/Ingestion/*` exist as a defensive belt-and-suspenders so ingestion never crashes against a not-yet-bootstrapped DB; after the host-level call succeeds they are zero-cost.

## Adding a new migration

From `apps/backend/src/BuildingBlocks/BuildingBlocks.Persistence/`:

```bash
# Design-time factory builds the context from IDX_DESIGNTIME_CONNECTION
# (defaults to local dev). Override if needed:
export IDX_DESIGNTIME_CONNECTION="Host=localhost;Port=5432;Database=indexdesk;Username=indexdesk;Password=indexdesk_dev_secret;"
dotnet ef migrations add <Name> --context IndexDeskDbContext --output-dir Migrations
```

`dotnet ef` discovers the context through `IndexDeskDbContextDesignTimeFactory` in this project — no need to boot the host.

After generating, verify:
1. `Up` does not call `create_hypertable` directly — wrap any Timescale-only DDL in the existing `IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb')` guard.
2. `Down` mirrors `Up`; never assume Timescale is installed.
3. New table / column additions stay in sync with `IndexDeskDbContext.OnModelCreating` and `MODELS.md`.

## Bootstrap a brand-new database

Just start either host. `MigrateAsync` creates `__EFMigrationsHistory`, runs `InitialCreate`, then `AddTimescaleAndSeed`. No manual steps.

## Baseline an existing dev database

If the schema already exists from `EnsureCreatedAsync()` (older dev DBs) and `__EFMigrationsHistory` is empty, replaying `InitialCreate` will fail with "relation already exists". Run [`../../../sql/baseline-existing-db.sql`](../../../../sql/baseline-existing-db.sql) once to mark both migrations as applied, then re-start the host.

```bash
psql "$IDX_PG_URL" -f apps/backend/sql/baseline-existing-db.sql
```

The script is idempotent (`IF NOT EXISTS` + `ON CONFLICT DO NOTHING`).

## Plain PostgreSQL fallback (no TimescaleDB)

The `AddTimescaleAndSeed` migration guards every `create_hypertable(...)` call with `IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb')`. On a plain PostgreSQL install, the migration succeeds and the time-series tables remain regular heaps:

- Inserts work unchanged.
- Range queries over `asset_quotes` work but lose hypertable chunk-pruning — expect slower scans beyond ~1 M rows per ticker. Acceptable for local dev and small deployments; plan a partitioning migration before production scale.
- `pgcrypto` and `uuid-ossp` are still required and created by `InitialCreate`.

## TimescaleDB image used in dev

`timescale/timescaledb:latest-pg18` ([`docker-compose.yml`](../../../../../../docker-compose.yml)). For production, pin a specific tag and grant the `indexdesk` role `CREATE` on the target schema so the extension migrations can run.

## Verification checklist before merging a migration

- [ ] `dotnet ef migrations add` produces a single new `Up`/`Down` pair.
- [ ] No `EnsureCreatedAsync` calls anywhere in production paths.
- [ ] Local dev DB applies the new migration on host start (idempotent on second run).
- [ ] `psql` check: `SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";` lists the new row.
