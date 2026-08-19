import { describe, it, expect } from 'vitest';
import { fetchRealYield, fetchAssets, fetchAssetByTicker } from '../api-client';

describe('API Client & Fallbacks', () => {
  it('fetchRealYield calculates exact Fisher yield correctly in fallback mode', async () => {
    // nominal = 12%, inflation = 4% => real = (1.12 / 1.04 - 1) * 100 = 7.6923%
    const result = await fetchRealYield(12, 4);
    expect(result.realYieldPercent).toBeCloseTo(7.6923, 3);
  });

  it('fetchAssets returns default ETF catalog when API is offline', async () => {
    const assets = await fetchAssets();
    expect(assets.length).toBeGreaterThan(0);
    expect(assets.some((a) => a.ticker === 'IVVB11')).toBe(true);
    expect(assets.some((a) => a.ticker === 'BOVA11')).toBe(true);
  });

  it('fetchAssetByTicker returns ticker data correctly', async () => {
    const asset = await fetchAssetByTicker('B5P211');
    expect(asset.ticker).toBe('B5P211');
    expect(asset.category).toBe('ETF');
  });
});
