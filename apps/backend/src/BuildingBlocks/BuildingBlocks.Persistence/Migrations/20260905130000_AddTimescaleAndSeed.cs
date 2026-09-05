using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndexDesk.BuildingBlocks.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimescaleAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TimescaleDB hypertables (DEC-003 / MODELS.md §2.2). The DO-block guard keeps the
            // migration replayable on vanilla PostgreSQL (fallback: tables stay as plain heaps
            // until native partitioning is adopted), so CI/test databases without the extension
            // apply the same migration unchanged.
            // Chunks follow MODELS.md: quotes & CVM daily reports 1 year, macro series 5 years.
            // portfolio_daily_snapshots converts too (it is a time series by SnapshotDate).
            // fx_rates and etf_holdings stay regular tables.
            // migrate_data => TRUE converts any pre-existing rows (dev DB was created via
            // EnsureCreated) instead of failing on non-empty tables.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb') THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM timescaledb_information.hypertables
                            WHERE hypertable_name = 'asset_quotes'
                        ) THEN
                            PERFORM create_hypertable('asset_quotes', 'Date', chunk_time_interval => INTERVAL '1 year', migrate_data => TRUE);
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM timescaledb_information.hypertables
                            WHERE hypertable_name = 'macro_economic_series'
                        ) THEN
                            PERFORM create_hypertable('macro_economic_series', 'Date', chunk_time_interval => INTERVAL '5 years', migrate_data => TRUE);
                        END IF;

                        IF NOT EXISTS (
                            SELECT 1 FROM timescaledb_information.hypertables
                            WHERE hypertable_name = 'portfolio_daily_snapshots'
                        ) THEN
                            PERFORM create_hypertable('portfolio_daily_snapshots', 'SnapshotDate', chunk_time_interval => INTERVAL '1 year', migrate_data => TRUE);
                        END IF;
                    END IF;
                END $$;
                """
            );

            // ANBIMA/B3 holidays 2025–2026 (idempotent: ON CONFLICT DO NOTHING).
            // Matches sql/seed-market-holidays.sql; source of truth moves to migrations.
            migrationBuilder.Sql(
                """
                INSERT INTO market_holidays ("Date", "Description", "Exchange")
                VALUES
                    ('2025-01-01', 'Confraternização Universal', 'B3'),
                    ('2025-03-03', 'Carnaval', 'B3'),
                    ('2025-03-04', 'Carnaval', 'B3'),
                    ('2025-04-18', 'Sexta-feira Santa', 'B3'),
                    ('2025-04-21', 'Tiradentes', 'B3'),
                    ('2025-05-01', 'Dia do Trabalho', 'B3'),
                    ('2025-06-19', 'Corpus Christi', 'B3'),
                    ('2025-09-07', 'Dia da Independência', 'B3'),
                    ('2025-10-12', 'Nossa Senhora Aparecida', 'B3'),
                    ('2025-11-02', 'Finados', 'B3'),
                    ('2025-11-15', 'Proclamação da República', 'B3'),
                    ('2025-12-25', 'Natal', 'B3'),
                    ('2026-01-01', 'Confraternização Universal', 'B3'),
                    ('2026-02-16', 'Carnaval', 'B3'),
                    ('2026-02-17', 'Carnaval', 'B3'),
                    ('2026-04-03', 'Sexta-feira Santa', 'B3'),
                    ('2026-04-21', 'Tiradentes', 'B3'),
                    ('2026-05-01', 'Dia do Trabalho', 'B3'),
                    ('2026-06-04', 'Corpus Christi', 'B3'),
                    ('2026-09-07', 'Dia da Independência', 'B3'),
                    ('2026-10-12', 'Nossa Senhora Aparecida', 'B3'),
                    ('2026-11-02', 'Finados', 'B3'),
                    ('2026-11-15', 'Proclamação da República', 'B3'),
                    ('2026-12-25', 'Natal', 'B3')
                ON CONFLICT ("Date") DO NOTHING;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverts only the Timescale metadata; tables keep existing data as plain heaps.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb') THEN
                        PERFORM drop_hypertable('asset_quotes', TRUE);
                        PERFORM drop_hypertable('macro_economic_series', TRUE);
                        PERFORM drop_hypertable('portfolio_daily_snapshots', TRUE);
                    END IF;
                END $$;
                """
            );
        }
    }
}
