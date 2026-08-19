import { DEFAULT_ASSETS, type AssetDto } from '@/lib/api-client';

export interface CatalogStats {
  assetCount: number;
  totalNetAssets: number;
  lowestManagementFee: number;
  lowestFeeTicker: string;
  averageManagementFee: number;
  managerCount: number;
  totalShareholders: number;
}

const EMPTY_STATS: CatalogStats = {
  assetCount: 0,
  totalNetAssets: 0,
  lowestManagementFee: 0,
  lowestFeeTicker: '',
  averageManagementFee: 0,
  managerCount: 0,
  totalShareholders: 0,
};

/**
 * Pure aggregates over the asset catalog.
 *
 * Derived from the same data the catalog table renders, so the home page
 * stat tiles never diverge from what the user sees in the list.
 */
export function computeCatalogStats(assets: AssetDto[] = DEFAULT_ASSETS): CatalogStats {
  if (assets.length === 0) return EMPTY_STATS;

  const lowestFee = assets.reduce((min, asset) =>
    asset.managementFee < min.managementFee ? asset : min,
  );
  const totalNetAssets = assets.reduce((sum, asset) => sum + asset.netAssets, 0);
  const averageManagementFee =
    assets.reduce((sum, asset) => sum + asset.managementFee, 0) / assets.length;
  const managerCount = new Set(assets.map((asset) => asset.manager)).size;
  const totalShareholders = assets.reduce((sum, asset) => sum + asset.shareholders, 0);

  return {
    assetCount: assets.length,
    totalNetAssets,
    lowestManagementFee: lowestFee.managementFee,
    lowestFeeTicker: lowestFee.ticker,
    averageManagementFee,
    managerCount,
    totalShareholders,
  };
}
