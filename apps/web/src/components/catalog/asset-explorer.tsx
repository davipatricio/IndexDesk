'use client';

import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useQueryState, parseAsString, parseAsStringLiteral } from 'nuqs';
import { fetchAssets, type AssetDto } from '@/lib/api-client';
import { computeCatalogStats } from '@/lib/catalog-stats';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { CatalogKpiBar } from './catalog-kpi-bar';
import { CatalogFilters } from './catalog-filters';
import { CatalogTable, getAssetCategory, type CatalogCategory } from './catalog-table';
import { Layers, Landmark, Globe } from 'lucide-react';

const CATEGORIES = ['ETF', 'FII', 'BDR'] as const;
const EMPTY_ASSETS: AssetDto[] = [];

function text(asset: AssetDto, ...keys: string[]): string {
  for (const key of keys) {
    const value = (asset as unknown as Record<string, unknown>)[key];
    if (typeof value === 'string' && value.trim()) return value;
  }
  return '';
}

function categoryAssets(assets: AssetDto[], category: CatalogCategory): AssetDto[] {
  return assets.filter((asset) => getAssetCategory(asset) === category);
}

export function AssetExplorer() {
  const [tab, setTab] = useQueryState('tab', parseAsStringLiteral(CATEGORIES).withDefault('ETF'));
  const [search, setSearch] = useQueryState('q', parseAsString.withDefault(''));
  const [manager, setManager] = useQueryState('gestora', parseAsString.withDefault('all'));
  const [subCategory, setSubCategory] = useQueryState('segmento', parseAsString.withDefault('all'));
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['assets'],
    queryFn: () => fetchAssets(),
    staleTime: 60_000,
  });
  const assets = data ?? EMPTY_ASSETS;
  const allByCategory = React.useMemo(
    () =>
      Object.fromEntries(
        CATEGORIES.map((category) => [category, categoryAssets(assets, category)]),
      ) as Record<CatalogCategory, AssetDto[]>,
    [assets],
  );
  const available = allByCategory[tab];
  const managers = React.useMemo(
    () =>
      Array.from(
        new Set(available.map((asset) => text(asset, 'manager', 'issuer')).filter(Boolean)),
      ).sort(),
    [available],
  );
  const subCategories = React.useMemo(
    () =>
      Array.from(
        new Set(
          available
            .map((asset) => text(asset, 'subCategory', 'assetClass', 'currency'))
            .filter(Boolean),
        ),
      ).sort(),
    [available],
  );
  const filtered = React.useMemo(
    () =>
      available.filter((asset) => {
        const haystack = [
          asset.ticker,
          asset.name,
          text(asset, 'manager', 'issuer'),
          text(asset, 'benchmarkSymbol', 'benchmark'),
          text(asset, 'assetClass'),
        ]
          .join(' ')
          .toLowerCase();
        return (
          (!search.trim() || haystack.includes(search.trim().toLowerCase())) &&
          (manager === 'all' || text(asset, 'manager', 'issuer') === manager) &&
          (subCategory === 'all' ||
            text(asset, 'subCategory', 'assetClass', 'currency') === subCategory)
        );
      }),
    [available, search, manager, subCategory],
  );

  const handleTabChange = (value: string) => {
    if (CATEGORIES.includes(value as CatalogCategory)) {
      void setTab(value as CatalogCategory);
      void setManager('all');
      void setSubCategory('all');
    }
  };
  const reset = () => {
    void setSearch('');
    void setManager('all');
    void setSubCategory('all');
  };
  const stats = computeCatalogStats(filtered);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col justify-between gap-4 border-b pb-4 sm:flex-row sm:items-center">
        <Tabs value={tab} onValueChange={handleTabChange}>
          <TabsList className="h-10 p-1">
            <TabsTrigger value="ETF" className="gap-2 px-3 text-xs sm:text-sm">
              <Layers className="size-4 text-primary" />
              ETFs
              <Badge variant="secondary" className="px-1 py-0 text-[10px] font-mono">
                {allByCategory.ETF.length}
              </Badge>
            </TabsTrigger>
            <TabsTrigger value="FII" className="gap-2 px-3 text-xs sm:text-sm">
              <Landmark className="size-4 text-primary" />
              FIIs
              <Badge variant="secondary" className="px-1 py-0 text-[10px] font-mono">
                {allByCategory.FII.length}
              </Badge>
            </TabsTrigger>
            <TabsTrigger value="BDR" className="gap-2 px-3 text-xs sm:text-sm">
              <Globe className="size-4 text-primary" />
              BDRs
              <Badge variant="secondary" className="px-1 py-0 text-[10px] font-mono">
                {allByCategory.BDR.length}
              </Badge>
            </TabsTrigger>
          </TabsList>
        </Tabs>
        <span className="text-xs text-muted-foreground">Dados atualizados recentemente</span>
      </div>
      {isError ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive"
        >
          <span>Não foi possível carregar os ativos agora. Tente novamente em instantes.</span>
          <ButtonRetry onClick={() => void refetch()} />
        </div>
      ) : null}
      <CatalogKpiBar stats={stats} totalAssetsCount={available.length} />
      <CatalogFilters
        category={tab}
        search={search}
        onSearchChange={(value) => void setSearch(value || null)}
        manager={manager}
        onManagerChange={(value) => void setManager(value === 'all' ? null : value)}
        subCategory={subCategory}
        onSubCategoryChange={(value) => void setSubCategory(value === 'all' ? null : value)}
        managerOptions={managers}
        subCategoryOptions={subCategories}
        totalFiltered={filtered.length}
        totalAvailable={available.length}
        onResetFilters={reset}
      />
      <CatalogTable category={tab} data={filtered} isLoading={isLoading} />
    </div>
  );
}

function ButtonRetry({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="rounded-md border border-destructive/30 px-3 py-1.5 text-xs font-medium text-destructive hover:bg-destructive/10"
    >
      Tentar novamente
    </button>
  );
}
