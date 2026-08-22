import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  fetchAssetByTicker,
  fetchAssetPerformance,
  fetchAssetQuotes,
  fetchAssets,
  fetchRealYield,
} from '../api-client';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('API client market-data contract', () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  function mockFetch(fetchMock: typeof globalThis.fetch): void {
    globalThis.fetch = fetchMock;
  }

  it('parses the real-yield response returned by the API', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({ nominalRate: 12, inflationRate: 4, realYieldPercent: 7.6923 }),
    );
    mockFetch(fetchMock);

    await expect(fetchRealYield(12, 4)).resolves.toEqual({
      nominalRate: 12,
      inflationRate: 4,
      realYieldPercent: 7.6923,
    });
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toContain('/api/v1/analytics/real-yield');
  });

  it('accepts the paginated asset envelope without inventing rows', async () => {
    const items = [
      {
        ticker: 'REAL11',
        name: 'ETF real',
        manager: 'Gestora real',
        category: 'ETF',
        assetClass: 'Equity',
        benchmark: 'IBOV',
        managementFee: 0.2,
        netAssets: 100,
        shareholders: 2,
        lastPrice: 10,
        changeDayPercent: 1,
        changeYtdPercent: 2,
      },
    ];
    mockFetch(vi.fn().mockResolvedValue(jsonResponse({ items, totalCount: 1 })));

    await expect(fetchAssets()).resolves.toEqual(items);
  });

  it('preserves an empty API catalog as an empty result', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ items: [], totalCount: 0 }));
    mockFetch(fetchMock);

    await expect(fetchAssets({ category: 'ETF', search: 'missing' })).resolves.toEqual([]);
    expect(fetchMock.mock.calls[0]?.[0]).toContain('category=ETF');
    expect(fetchMock.mock.calls[0]?.[0]).toContain('search=missing');
  });

  it('surfaces an unavailable catalog response instead of returning fixture rows', async () => {
    mockFetch(vi.fn().mockResolvedValue(new Response('service unavailable', { status: 503 })));

    await expect(fetchAssets()).rejects.toThrow();
  });

  it('normalizes a ticker for detail requests and rejects an unknown ticker', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response('not found', { status: 404 }));
    mockFetch(fetchMock);

    await expect(fetchAssetByTicker('missing11')).rejects.toThrow();
    expect(fetchMock.mock.calls[0]?.[0]).toContain('/api/v1/assets/MISSING11');
  });

  it('returns quote rows from the API and forwards range options', async () => {
    const quotes = [
      {
        date: '2026-01-02',
        open: 10,
        high: 11,
        low: 9,
        close: 10.5,
        adjustedClose: 10.5,
        volume: 1000,
      },
    ];
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(quotes));
    mockFetch(fetchMock);

    await expect(
      fetchAssetQuotes('real11', { from: '2026-01-01', to: '2026-01-31', days: 30 }),
    ).resolves.toEqual(quotes);
    const url = String(fetchMock.mock.calls[0]?.[0]);
    expect(url).toContain('/api/v1/assets/REAL11/quotes');
    expect(url).toContain('from=2026-01-01');
    expect(url).toContain('to=2026-01-31');
    expect(url).toContain('days=30');
  });

  it('keeps an empty quote series empty when the API has no history', async () => {
    mockFetch(vi.fn().mockResolvedValue(jsonResponse([])));

    await expect(fetchAssetQuotes('REAL11')).resolves.toEqual([]);
  });

  it('parses performance metrics and query options from the API', async () => {
    const performance = {
      ticker: 'REAL11',
      from: '2025-01-01',
      to: '2026-01-01',
      startPrice: 10,
      endPrice: 12,
      priceReturnPercent: 20,
      totalReturnPercent: 21,
      annualizedReturnPercent: 20,
      dividendPayments: 1,
      dividendsTotal: 0.1,
      benchmarks: [],
    };
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(performance));
    mockFetch(fetchMock);

    await expect(
      fetchAssetPerformance('real11', {
        from: '2025-01-01',
        to: '2026-01-01',
        returnType: 'total',
        includeBenchmarks: false,
      }),
    ).resolves.toEqual(performance);
    const url = String(fetchMock.mock.calls[0]?.[0]);
    expect(url).toContain('returnType=total');
    expect(url).toContain('includeBenchmarks=false');
  });
});
