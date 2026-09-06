-- Baseline marker for databases that pre-date EF Core migrations (FND-013).
-- The dev DB was originally created with EnsureCreatedAsync() and therefore has
-- the full schema but NO rows in __EFMigrationsHistory. Running DatabaseInitializer
-- against such a database will replay InitialCreate from scratch and fail on
-- "relation already exists". This script marks the migrations as already applied
-- WITHOUT re-executing them. Safe to re-run (ON CONFLICT DO NOTHING on the PK).
--
-- Usage (from repo root):
--   psql "$IDX_PG_URL" -f apps/backend/sql/baseline-existing-db.sql
-- Or against the local dev container:
--   docker exec -i indexdesk-postgres psql -U indexdesk -d indexdesk \
--       < apps/backend/sql/baseline-existing-db.sql
--
-- If the database is empty, do NOT run this file — let DatabaseInitializer.MigrateAsync
-- create everything from the migrations.

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId"    VARCHAR(150) NOT NULL,
    "ProductVersion" VARCHAR(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES
    ('20260905125533_InitialCreate',     '9.0.2'),
    ('20260905130000_AddTimescaleAndSeed','9.0.2')
ON CONFLICT ("MigrationId") DO NOTHING;
