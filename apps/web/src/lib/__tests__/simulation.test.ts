import { describe, expect, it } from 'vitest';

import {
  accumulateRates,
  buildInvestmentSimulation,
  summarize,
  type BenchmarkSeriesInput,
  type SeriesPoint,
} from '@/lib/simulation';

const ASSET: SeriesPoint[] = [
  { date: '2025-01-01', value: 10 },
  { date: '2025-04-01', value: 11 },
  { date: '2025-07-01', value: 12 },
  { date: '2025-10-01', value: 15 },
];

describe('accumulateRates', () => {
  it('compounds percent rates into a growth factor', () => {
    const result = accumulateRates([
      { date: '2025-01-02', value: 100 }, // ×2
      { date: '2025-01-03', value: 50 }, // ×1.5
    ]);
    expect(result).toEqual([
      { date: '2025-01-02', value: 2 },
      { date: '2025-01-03', value: 3 },
    ]);
  });

  it('keeps factor 1 for zero rates', () => {
    const result = accumulateRates([{ date: '2025-01-02', value: 0 }]);
    expect(result[0]?.value).toBe(1);
  });
});

describe('summarize', () => {
  it('returns final value and percent return', () => {
    const { finalValue, returnPercent } = summarize([1000, 1100, 1200], 1000);
    expect(finalValue).toBe(1200);
    expect(returnPercent).toBeCloseTo(20, 6);
  });
});

describe('buildInvestmentSimulation', () => {
  const ibov: BenchmarkSeriesInput = {
    key: 'IBOV',
    label: 'Ibovespa',
    points: [
      { date: '2024-06-01', value: 100 },
      { date: '2025-02-01', value: 105 },
      { date: '2025-08-01', value: 120 },
      { date: '2025-12-01', value: 130 },
    ],
  };

  it('normalizes every series to the amount at the window start', () => {
    const result = buildInvestmentSimulation({
      amount: 1000,
      periodMonths: null,
      assetPoints: ASSET,
      benchmarks: [],
    });

    expect(result.dates).toEqual(ASSET.map((point) => point.date));
    expect(result.series[0]?.values[0]).toBeCloseTo(1000, 6);
    expect(result.series[0]?.values.at(-1)).toBeCloseTo(1500, 6);
    expect(result.unavailable).toEqual([]);
  });

  it('forward-fills benchmark closes onto the asset calendar', () => {
    const result = buildInvestmentSimulation({
      amount: 1000,
      periodMonths: null,
      assetPoints: ASSET,
      benchmarks: [ibov],
    });

    // IBOV has no point on 2025-04-01/07-01 → holds the 2025-02-01 close.
    expect(result.series[1]?.key).toBe('IBOV');
    expect(result.series[1]?.values.map((v) => Math.round(v))).toEqual([1000, 1050, 1050, 1200]);
  });

  it('marks benchmarks whose history starts after the window as unavailable', () => {
    const lateBenchmark: BenchmarkSeriesInput = {
      key: 'IFIX',
      label: 'IFIX',
      points: [{ date: '2025-09-01', value: 3682 }],
    };
    const result = buildInvestmentSimulation({
      amount: 1000,
      periodMonths: null,
      assetPoints: ASSET,
      benchmarks: [lateBenchmark],
    });

    expect(result.series).toHaveLength(1); // asset only
    expect(result.unavailable).toHaveLength(1);
    expect(result.unavailable[0]?.key).toBe('IFIX');
  });

  it('accumulates rate benchmarks (CDI/Selic) before aligning them', () => {
    const cdi: BenchmarkSeriesInput = {
      key: 'CDI',
      label: 'CDI',
      kind: 'rate',
      points: [
        { date: '2024-06-01', value: 50 }, // ×1.5
        { date: '2025-05-01', value: 50 }, // ×1.5 → factor 2.25
      ],
    };
    const result = buildInvestmentSimulation({
      amount: 1000,
      periodMonths: null,
      assetPoints: ASSET,
      benchmarks: [cdi],
    });

    // Base at 2025-01-01 uses factor 1.5; last date keeps 2.25.
    const cdiValues = result.series.find((s) => s.key === 'CDI')?.values ?? [];
    expect(Math.round(cdiValues[0] ?? 0)).toBe(1000);
    expect(Math.round(cdiValues.at(-1) ?? 0)).toBe(1500);
  });

  it('restricts the window to the requested months and anchors before it', () => {
    const result = buildInvestmentSimulation({
      amount: 500,
      periodMonths: 8, // 2025-02-01 .. end
      assetPoints: ASSET,
      benchmarks: [ibov],
    });

    // Anchor = last quote on/before 2025-02-01 → none exists inside, so the
    // first in-window point (2025-04-01) is the base.
    expect(result.startDate).toBe('2025-04-01');
    expect(result.dates).toEqual(['2025-04-01', '2025-07-01', '2025-10-01']);
    expect(result.series[0]?.values[0]).toBeCloseTo(500, 6);
    expect(result.series[0]?.values.at(-1)).toBeCloseTo((15 / 11) * 500, 4);
  });

  it('uses the asset inception when the period predates its first quote', () => {
    const youngAsset = ASSET.slice(-2); // only 2025-07-01 onward
    const result = buildInvestmentSimulation({
      amount: 1000,
      periodMonths: null,
      assetPoints: youngAsset,
      benchmarks: [ibov],
    });
    expect(result.inceptionDate).toBe('2025-07-01');
    expect(result.dates.length).toBe(2);
    expect(result.unavailable).toEqual([]);
  });

  it('returns an empty result for invalid input', () => {
    expect(
      buildInvestmentSimulation({
        amount: 0,
        periodMonths: null,
        assetPoints: ASSET,
        benchmarks: [],
      }).dates,
    ).toEqual([]);
    expect(
      buildInvestmentSimulation({
        amount: 1000,
        periodMonths: null,
        assetPoints: ASSET.slice(0, 1),
        benchmarks: [],
      }).series,
    ).toEqual([]);
  });
});
