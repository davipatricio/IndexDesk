import { describe, expect, it } from 'vitest';

import { filterQuotesByPeriod, resolveHeroTimeframe } from '@/lib/chart-period';

const QUOTES = Array.from({ length: 24 }, (_, index) => {
  const date = new Date('2026-08-01T00:00:00Z');
  date.setUTCMonth(date.getUTCMonth() - index);
  return { date: date.toISOString().slice(0, 10), close: 100 + index, volume: 1000 };
});

describe('filterQuotesByPeriod', () => {
  it('returns everything for null months', () => {
    expect(filterQuotesByPeriod(QUOTES, null)).toHaveLength(24);
  });

  it('slices roughly the requested months back from the last quote', () => {
    const sixMonths = filterQuotesByPeriod(QUOTES, 6);
    // Window is [last - 6m .. last]: months 0..6 of a monthly series → 7 points
    // (start boundary included).
    expect(sixMonths.length).toBeGreaterThanOrEqual(6);
    expect(sixMonths.length).toBeLessThanOrEqual(8);
    expect(sixMonths.at(-1)?.date).toBe(QUOTES[0]?.date); // sorted ascending: newest last
  });

  it('keeps the single newest point when the window has no data (sparse sync)', () => {
    const sparse = [
      { date: '2024-01-15', close: 5, volume: 0 },
      { date: '2026-08-20', close: 9, volume: 1 },
    ];
    expect(filterQuotesByPeriod(sparse, 3)).toEqual([sparse[1]]);
  });

  it('sorts unsorted input', () => {
    const picked = [QUOTES[5], QUOTES[0], QUOTES[2]].filter((quote) => quote != null);
    const shuffled = picked;
    const result = filterQuotesByPeriod(shuffled, null);
    expect(result.map((quote) => quote.date)).toEqual(
      [...result.map((quote) => quote.date)].sort(),
    );
  });
});

describe('resolveHeroTimeframe', () => {
  it('maps known values and defaults unknown ones to 6M', () => {
    expect(resolveHeroTimeframe('1A').months).toBe(12);
    expect(resolveHeroTimeframe('TUDO').months).toBeNull();
    expect(resolveHeroTimeframe(null).value).toBe('6M');
    expect(resolveHeroTimeframe('hacker').value).toBe('6M');
  });
});
