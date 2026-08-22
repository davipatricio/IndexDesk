import { describe, expect, it } from 'vitest';
import { computeCatalogStats } from '../catalog-stats';
import type { AssetDto } from '../api-client';

const apiAssets: AssetDto[] = [
  {
    ticker: 'REAL11',
    name: 'ETF real',
    assetType: 'ETF',
    currency: 'BRL',
    benchmarkSymbol: 'IBOV',
    lastPrice: 10,
    changeDayPercent: 1,
    return1mPercent: null,
    return12mPercent: 2,
    annualizedVolatilityPercent: null,
    sharpeRatio: null,
    maxDrawdownPercent: null,
    firstQuoteDate: null,
    lastQuoteDate: null,
  },
  {
    ticker: 'REAL12',
    name: 'ETF real 2',
    assetType: 'ETF',
    currency: 'BRL',
    benchmarkSymbol: 'CDI',
    lastPrice: 20,
    changeDayPercent: -1,
    return1mPercent: null,
    return12mPercent: 4,
    annualizedVolatilityPercent: null,
    sharpeRatio: null,
    maxDrawdownPercent: null,
    firstQuoteDate: null,
    lastQuoteDate: null,
  },
];

describe('computeCatalogStats', () => {
  it('computes API-backed quote aggregates without assuming fixture fields', () => {
    const stats = computeCatalogStats(apiAssets);

    expect(stats.assetCount).toBe(2);
    expect(stats.totalNetAssets).toBeNull();
    expect(stats.totalShareholders).toBeNull();
    expect(stats.primaryMetric).toEqual({ label: 'Retorno 12M médio', value: '3.00%' });
    expect(stats.secondaryMetric).toEqual({ label: 'Cotação média', value: 'R$ 15.00' });
  });

  it('handles an empty API response as an empty state', () => {
    const stats = computeCatalogStats([]);

    expect(stats.assetCount).toBe(0);
    expect(stats.totalNetAssets).toBeNull();
    expect(stats.totalShareholders).toBeNull();
    expect(stats.primaryMetric.value).toBe('—');
    expect(stats.secondaryMetric.value).toBe('—');
  });

  it('ignores missing or non-finite quote values', () => {
    const stats = computeCatalogStats([
      { ...apiAssets[0]!, lastPrice: null as unknown as number },
      { ...apiAssets[1]!, lastPrice: Number.NaN },
    ]);

    expect(stats.secondaryMetric.value).toBe('—');
  });
});
