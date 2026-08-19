import { describe, it, expect } from 'vitest';
import { computeCatalogStats } from '../catalog-stats';
import { DEFAULT_ASSETS } from '../api-client';

describe('computeCatalogStats', () => {
  it('computes aggregates correctly from DEFAULT_ASSETS', () => {
    const stats = computeCatalogStats(DEFAULT_ASSETS);

    expect(stats.assetCount).toBe(DEFAULT_ASSETS.length);
    expect(stats.totalNetAssets).toBeGreaterThan(20000000000);
    expect(stats.lowestFeeTicker).toBe('BOVA11');
    expect(stats.lowestManagementFee).toBe(0.1);
    expect(stats.managerCount).toBeGreaterThanOrEqual(3);
    expect(stats.totalShareholders).toBeGreaterThan(500000);
  });

  it('handles empty input gracefully', () => {
    const stats = computeCatalogStats([]);

    expect(stats.assetCount).toBe(0);
    expect(stats.totalNetAssets).toBe(0);
    expect(stats.lowestManagementFee).toBe(0);
    expect(stats.lowestFeeTicker).toBe('');
    expect(stats.averageManagementFee).toBe(0);
    expect(stats.managerCount).toBe(0);
    expect(stats.totalShareholders).toBe(0);
  });
});
