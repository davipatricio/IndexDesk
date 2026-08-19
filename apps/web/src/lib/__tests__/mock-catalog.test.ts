import { describe, it, expect } from 'vitest';
import {
  MOCK_ETFS,
  MOCK_FIIS,
  MOCK_BDRS,
  ALL_CATALOG_ASSETS,
  getCatalogAssets,
  findCatalogAsset,
  getFilterOptions,
  computeCategoryStats,
  filterCatalogAssets,
} from '../mock-catalog';

describe('mock-catalog data layer', () => {
  it('loads non-empty fixture datasets for ETFs, FIIs, and BDRs', () => {
    expect(MOCK_ETFS.length).toBeGreaterThanOrEqual(10);
    expect(MOCK_FIIS.length).toBeGreaterThanOrEqual(10);
    expect(MOCK_BDRS.length).toBeGreaterThanOrEqual(10);
    expect(ALL_CATALOG_ASSETS.length).toBe(MOCK_ETFS.length + MOCK_FIIS.length + MOCK_BDRS.length);
  });

  it('retrieves category subsets correctly via getCatalogAssets', () => {
    const etfs = getCatalogAssets('ETF');
    expect(etfs.length).toBe(MOCK_ETFS.length);
    expect(etfs.every((a) => a.category === 'ETF')).toBe(true);

    const fiis = getCatalogAssets('FII');
    expect(fiis.length).toBe(MOCK_FIIS.length);
    expect(fiis.every((a) => a.category === 'FII')).toBe(true);

    const bdrs = getCatalogAssets('BDR');
    expect(bdrs.length).toBe(MOCK_BDRS.length);
    expect(bdrs.every((a) => a.category === 'BDR')).toBe(true);

    const all = getCatalogAssets();
    expect(all.length).toBe(ALL_CATALOG_ASSETS.length);
  });

  it('finds assets case-insensitively via findCatalogAsset', () => {
    expect(findCatalogAsset('ivvb11')?.ticker).toBe('IVVB11');
    expect(findCatalogAsset('HGLG11')?.category).toBe('FII');
    expect(findCatalogAsset('bivw39')?.category).toBe('BDR');
    expect(findCatalogAsset('UNKNOWN99')).toBeUndefined();
  });

  it('extracts unique and sorted filter options for each category', () => {
    const etfFilters = getFilterOptions('ETF');
    expect(etfFilters.managers.length).toBeGreaterThan(0);
    expect(etfFilters.subCategories.length).toBeGreaterThan(0);
    expect(etfFilters.managers).toContain('BlackRock');
    expect(etfFilters.subCategories).toContain('Ações Brasil');

    const fiiFilters = getFilterOptions('FII');
    expect(fiiFilters.managers).toContain('Kinea Investimentos');
    expect(fiiFilters.subCategories).toContain('Tijolo - Logística');

    const bdrFilters = getFilterOptions('BDR');
    expect(bdrFilters.managers).toContain('BlackRock');
  });

  it('computes category stats correctly for ETFs', () => {
    const stats = computeCategoryStats(MOCK_ETFS, 'ETF');
    expect(stats.assetCount).toBe(MOCK_ETFS.length);
    expect(stats.totalNetAssets).toBeGreaterThan(0);
    expect(stats.primaryMetric.label).toBe('Taxa Adm. Média');
    expect(stats.primaryMetric.value).toContain('% a.a.');
    expect(stats.secondaryMetric.label).toBe('Menor Taxa');
    expect(stats.secondaryMetric.value).toContain('BOVA11');
  });

  it('computes category stats correctly for FIIs', () => {
    const stats = computeCategoryStats(MOCK_FIIS, 'FII');
    expect(stats.assetCount).toBe(MOCK_FIIS.length);
    expect(stats.primaryMetric.label).toBe('DY Médio 12M');
    expect(stats.primaryMetric.value).toContain('% a.a.');
    expect(stats.secondaryMetric.label).toBe('P/VP Médio');
    expect(stats.secondaryMetric.value).toContain('x');
  });

  it('computes category stats correctly for BDRs', () => {
    const stats = computeCategoryStats(MOCK_BDRS, 'BDR');
    expect(stats.assetCount).toBe(MOCK_BDRS.length);
    expect(stats.primaryMetric.label).toBe('Ativos Subjacentes');
    expect(stats.primaryMetric.value).toContain('mapeados');
    expect(stats.secondaryMetric.label).toBe('Taxa ETF Base Média');
  });

  it('filters catalog assets by search term across fields', () => {
    // By Ticker
    const byTicker = filterCatalogAssets(MOCK_ETFS, { search: 'BOVA' });
    expect(byTicker.length).toBe(1);
    expect(byTicker[0]?.ticker).toBe('BOVA11');

    // By Manager
    const byManager = filterCatalogAssets(MOCK_ETFS, { manager: 'BlackRock' });
    expect(byManager.length).toBeGreaterThan(0);
    expect(byManager.every((a) => a.manager === 'BlackRock')).toBe(true);

    // By SubCategory
    const bySubCategory = filterCatalogAssets(MOCK_FIIS, {
      subCategory: 'Tijolo - Logística',
    });
    expect(bySubCategory.length).toBeGreaterThan(0);
    expect(bySubCategory.every((a) => a.subCategory === 'Tijolo - Logística')).toBe(true);

    // Combined search + manager
    const combined = filterCatalogAssets(MOCK_BDRS, {
      search: 'S&P 500',
      manager: 'BlackRock',
    });
    expect(combined.length).toBeGreaterThan(0);
    expect(combined.some((a) => a.ticker === 'BIVW39')).toBe(true);
  });
});
