import type { AssetDto } from '@/lib/api-client';

export interface CatalogStatsSummary {
  assetCount: number;
  totalNetAssets: number | null;
  primaryMetric: { label: string; value: string };
  secondaryMetric: { label: string; value: string };
  totalShareholders: number | null;
}

function numeric(asset: AssetDto, key: string): number | null {
  const value = (asset as unknown as Record<string, unknown>)[key];
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

export function computeCatalogStats(assets: AssetDto[] = []): CatalogStatsSummary {
  const prices = assets.map((asset) => numeric(asset, 'lastPrice')).filter((value): value is number => value != null);
  const returns = assets.map((asset) => numeric(asset, 'return12mPercent')).filter((value): value is number => value != null);

  return {
    assetCount: assets.length,
    totalNetAssets: null,
    primaryMetric: {
      label: 'Retorno 12M médio',
      value: returns.length ? `${(returns.reduce((sum, value) => sum + value, 0) / returns.length).toFixed(2)}%` : '—',
    },
    secondaryMetric: {
      label: 'Cotação média',
      value: prices.length ? `R$ ${(prices.reduce((sum, value) => sum + value, 0) / prices.length).toFixed(2)}` : '—',
    },
    totalShareholders: null,
  };
}
