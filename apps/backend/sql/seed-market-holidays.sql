-- ANBIMA B3 Market Holidays Seed (MODELS.md §market_holidays / FND-013)
-- Populates non-business days for 2025 and 2026.
-- Safe to re-run: idempotent ON CONFLICT (Date) DO NOTHING.
--
-- Consulted by BusinessDayCalculator / MarketHolidayQueries during the Worker
-- sync catch-up bootstrap and for downstream base-252 trading-day calculations.

CREATE TABLE IF NOT EXISTS market_holidays (
    "Date" DATE PRIMARY KEY,
    "Description" VARCHAR(100) NOT NULL,
    "Exchange" VARCHAR(10) NOT NULL DEFAULT 'B3',
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_market_holidays_exchange ON market_holidays("Exchange");

INSERT INTO market_holidays ("Date", "Description", "Exchange") VALUES
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
