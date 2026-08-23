/**
 * Shared period-slicing helpers for the price hero chart. Pure functions so
 * both the component and the unit tests exercise the exact same logic.
 */

export const HERO_TIMEFRAMES = [
  { value: '1M', label: '1M', months: 1 },
  { value: '3M', label: '3M', months: 3 },
  { value: '6M', label: '6M', months: 6 },
  { value: '1A', label: '1A', months: 12 },
  { value: 'TUDO', label: 'TUDO', months: null },
] as const;

export type HeroTimeframeValue = (typeof HERO_TIMEFRAMES)[number]['value'];

export interface PeriodQuotePoint {
  date: string;
}

/** Resolves the timeframe entry for a URL param, defaulting to 6M. */
export function resolveHeroTimeframe(value: string | null): {
  value: HeroTimeframeValue;
  label: string;
  months: number | null;
} {
  return HERO_TIMEFRAMES.find((timeframe) => timeframe.value === value) ?? HERO_TIMEFRAMES[2];
}

/**
 * Slices quotes to the requested window (months back from the last quote).
 * Keeps the final point when a sparse series has nothing inside the window,
 * so the chart never renders empty after a short offline sync.
 */
export function filterQuotesByPeriod<T extends PeriodQuotePoint>(
  quotes: T[],
  months: number | null,
): T[] {
  const sorted = [...quotes].sort((first, second) => first.date.localeCompare(second.date));
  if (months === null || sorted.length === 0) return sorted;

  const lastDate = sorted.at(-1)?.date;
  if (!lastDate) return sorted;

  const anchor = new Date(`${lastDate}T00:00:00Z`);
  if (Number.isNaN(anchor.getTime())) return sorted;
  anchor.setUTCMonth(anchor.getUTCMonth() - months);

  const filtered = sorted.filter((quote) => new Date(`${quote.date}T00:00:00Z`) >= anchor);
  return filtered.length > 0 ? filtered : sorted.slice(-1);
}
