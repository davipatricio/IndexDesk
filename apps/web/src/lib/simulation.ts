/**
 * Investment-simulation engine ("Simular investimento").
 *
 * Pure functions only: aligns the asset price calendar with benchmark series,
 * accumulates daily-rate benchmarks (CDI/Selic) into growth factors and
 * normalizes every series to the simulated amount at the window start.
 *
 * Alignment rules:
 * - The asset's own quote dates drive the x-axis (sparse FII calendars included).
 * - Price benchmarks are forward-filled from their last known close on or before
 *   each asset date.
 * - Rate benchmarks compound per observation (product of 1 + rate/100), mirroring
 *   the backend `AccumulateRateSeries`, before being forward-filled the same way.
 * - A benchmark that has no point on or before the window start cannot express a
 *   return for the whole period and is reported as unavailable instead of being
 *   silently truncated.
 */

export interface SeriesPoint {
  date: string;
  value: number;
}

export interface BenchmarkSeriesInput {
  key: string;
  label: string;
  /** Closing prices (price benchmarks) or raw percent rates (rate benchmarks). */
  points: SeriesPoint[];
  /** When true, each point is a percent rate compounded per observation. */
  kind?: 'price' | 'rate';
}

export type SimulationPeriodKey = '6M' | '1A' | '3A' | 'TUDO';

export const SIMULATION_PERIODS: ReadonlyArray<{
  key: SimulationPeriodKey;
  label: string;
  months: number | null;
}> = [
  { key: '6M', label: '6 meses', months: 6 },
  { key: '1A', label: '1 ano', months: 12 },
  { key: '3A', label: '3 anos', months: 36 },
  { key: 'TUDO', label: 'Desde o início', months: null },
];

export interface UnavailableBenchmark {
  key: string;
  label: string;
  reason: string;
}

export interface SimulationResult {
  /** Aligned x-axis: the asset's quote dates inside the window, ascending. */
  dates: string[];
  /** First quote date ever recorded for the asset (independent of the window). */
  inceptionDate: string | null;
  /** Effective first date of the simulated window. */
  startDate: string | null;
  /**
   * One entry per rendered series, values in BRL normalized to the amount at the
   * first date. The asset is always index 0 when present.
   */
  series: Array<{ key: string; label: string; values: number[] }>;
  unavailable: UnavailableBenchmark[];
}

function toTimestamp(date: string): number {
  const ts = Date.parse(`${date}T00:00:00Z`);
  return Number.isNaN(ts) ? 0 : ts;
}

type SortedPoint = SeriesPoint & { ts: number };

function sortByDate(points: SeriesPoint[]): SortedPoint[] {
  return [...points]
    .map((point) => ({ ...point, ts: toTimestamp(point.date) }))
    .filter((point) => point.ts > 0 && Number.isFinite(point.value))
    .sort((first, second) => first.ts - second.ts);
}

/** Compounds raw percent rates into a cumulative growth factor per date. */
export function accumulateRates(points: SeriesPoint[]): SeriesPoint[] {
  let factor = 1;
  return points.map((point) => {
    factor *= 1 + point.value / 100;
    return { date: point.date, value: factor };
  });
}

/** Sorts by date and compounds rate benchmarks into growth factors (keeps `ts`). */
function prepareBenchmarkPoints(
  points: SeriesPoint[],
  kind: BenchmarkSeriesInput['kind'],
): Array<SeriesPoint & { ts: number }> {
  const sorted = sortByDate(points);
  if (kind !== 'rate') return sorted;

  let factor = 1;
  return sorted.map((point) => {
    factor *= 1 + point.value / 100;
    return { date: point.date, value: factor, ts: point.ts };
  });
}

function monthsBefore(isoDate: string, months: number): number {
  const date = new Date(`${isoDate}T00:00:00Z`);
  date.setUTCMonth(date.getUTCMonth() - months);
  return date.getTime();
}

/**
 * Forward-fills a sorted series onto the requested calendar. Returns null when
 * the series has no point on or before the first requested date.
 */
function forwardFillOnto(sorted: SortedPoint[], calendar: SortedPoint[]): number[] | null {
  const first = calendar[0];
  const firstSorted = sorted[0];
  if (!first || !firstSorted || firstSorted.ts > first.ts) return null;

  const values: number[] = [];
  let cursor = 0;
  let last = firstSorted.value;
  for (const tick of calendar) {
    let next = sorted[cursor];
    while (next && next.ts <= tick.ts) {
      last = next.value;
      cursor += 1;
      next = sorted[cursor];
    }
    values.push(last);
  }
  return values;
}

export function buildInvestmentSimulation(options: {
  amount: number;
  periodMonths: number | null;
  assetPoints: SeriesPoint[];
  benchmarks: BenchmarkSeriesInput[];
}): SimulationResult {
  const { amount, periodMonths, benchmarks } = options;
  const empty: SimulationResult = {
    dates: [],
    inceptionDate: null,
    startDate: null,
    series: [],
    unavailable: [],
  };

  if (!(amount > 0)) return empty;

  const assetSorted = sortByDate(options.assetPoints);
  const firstAssetPoint = assetSorted[0];
  const lastTick = assetSorted.at(-1);
  if (assetSorted.length < 2 || !firstAssetPoint || !lastTick) return empty;

  const windowStartTs =
    periodMonths === null ? firstAssetPoint.ts : monthsBefore(lastTick.date, periodMonths);

  // Keep one anchor point at/before the window start so the first in-window
  // value already reflects movement relative to a real base close.
  const anchorIndex = assetSorted.findLastIndex((point) => point.ts <= windowStartTs);
  const startAnchor = anchorIndex >= 0 ? assetSorted[anchorIndex] : firstAssetPoint;
  if (!startAnchor) return empty;
  const effectiveStartTs = Math.max(windowStartTs, startAnchor.ts);

  const windowed = assetSorted.filter((point) => point.ts >= effectiveStartTs);
  if (windowed.length < 1 || !windowed[0]) return empty;

  const calendar: SortedPoint[] = [...windowed];
  const baseAssetValue = windowed[0].value;
  if (baseAssetValue <= 0) return empty;

  const series: SimulationResult['series'] = [];
  const unavailable: UnavailableBenchmark[] = [];

  series.push({
    key: 'asset',
    label: 'Ativo',
    values: windowed.map((point) => (amount * point.value) / baseAssetValue),
  });

  for (const benchmark of benchmarks) {
    const prepared = prepareBenchmarkPoints(benchmark.points, benchmark.kind);

    const filled = forwardFillOnto(prepared, calendar);
    if (filled === null) {
      unavailable.push({
        key: benchmark.key,
        label: benchmark.label,
        reason: 'histórico anterior ao início da janela indisponível',
      });
      continue;
    }

    const base = filled[0];
    if (base === undefined || base <= 0) {
      unavailable.push({
        key: benchmark.key,
        label: benchmark.label,
        reason: 'série com valores inválidos',
      });
      continue;
    }
    series.push({
      key: benchmark.key,
      label: benchmark.label,
      values: filled.map((value) => (amount * value) / base),
    });
  }

  return {
    dates: calendar.map((tick) => tick.date),
    inceptionDate: firstAssetPoint.date,
    startDate: windowed[0].date,
    series,
    unavailable,
  };
}

/** Final value and percent return of one simulated series. */
export function summarize(
  values: number[],
  amount: number,
): {
  finalValue: number;
  returnPercent: number;
} {
  const finalValue = values.at(-1) ?? amount;
  const base = values[0] ?? amount;
  return {
    finalValue,
    returnPercent: base > 0 ? (finalValue / base - 1) * 100 : 0,
  };
}
