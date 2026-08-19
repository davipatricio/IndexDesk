'use client';

import * as React from 'react';
import { useQueryState, parseAsString, parseAsStringLiteral } from 'nuqs';
import {
  getCatalogAssets,
  getFilterOptions,
  computeCategoryStats,
  filterCatalogAssets,
} from '@/lib/mock-catalog';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Badge } from '@/components/ui/badge';
import { CatalogKpiBar } from './catalog-kpi-bar';
import { CatalogFilters } from './catalog-filters';
import { CatalogTable } from './catalog-table';
import { Layers, Landmark, Globe } from 'lucide-react';

const CATEGORIES = ['ETF', 'FII', 'BDR'] as const;

export function AssetExplorer() {
  const [tab, setTab] = useQueryState('tab', parseAsStringLiteral(CATEGORIES).withDefault('ETF'));
  const [search, setSearch] = useQueryState('q', parseAsString.withDefault(''));
  const [manager, setManager] = useQueryState('gestora', parseAsString.withDefault('all'));
  const [subCategory, setSubCategory] = useQueryState('segmento', parseAsString.withDefault('all'));

  // Switch tab and clear category-specific dropdown filters
  const handleTabChange = (newCategory: string) => {
    if (newCategory === 'ETF' || newCategory === 'FII' || newCategory === 'BDR') {
      void setTab(newCategory);
      void setManager('all');
      void setSubCategory('all');
    }
  };

  const handleResetFilters = () => {
    void setSearch('');
    void setManager('all');
    void setSubCategory('all');
  };

  // 1. Data derivation for current tab
  const categoryAssets = React.useMemo(() => getCatalogAssets(tab), [tab]);
  const filterOptions = React.useMemo(() => getFilterOptions(tab), [tab]);

  // 2. Filter dataset
  const filteredAssets = React.useMemo(
    () =>
      filterCatalogAssets(categoryAssets, {
        search,
        manager,
        subCategory,
      }),
    [categoryAssets, search, manager, subCategory],
  );

  // 3. Compute KPI summary for filtered items
  const stats = React.useMemo(
    () => computeCategoryStats(filteredAssets, tab),
    [filteredAssets, tab],
  );

  return (
    <div className="flex flex-col gap-6">
      {/* Category Navigation Tabs */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b pb-4">
        <Tabs
          value={tab}
          onValueChange={(val) => {
            if (typeof val === 'string') handleTabChange(val);
          }}
        >
          <TabsList className="h-10 p-1">
            <TabsTrigger value="ETF" className="gap-2 px-3 text-xs sm:text-sm">
              <Layers className="size-4 text-primary" />
              <span>ETFs de Índice</span>
              <Badge variant="secondary" className="text-[10px] py-0 px-1 font-mono">
                {getCatalogAssets('ETF').length}
              </Badge>
            </TabsTrigger>
            <TabsTrigger value="FII" className="gap-2 px-3 text-xs sm:text-sm">
              <Landmark className="size-4 text-primary" />
              <span>Fundos Imobiliários (FIIs)</span>
              <Badge variant="secondary" className="text-[10px] py-0 px-1 font-mono">
                {getCatalogAssets('FII').length}
              </Badge>
            </TabsTrigger>
            <TabsTrigger value="BDR" className="gap-2 px-3 text-xs sm:text-sm">
              <Globe className="size-4 text-primary" />
              <span>BDRs &amp; ETFs Globais</span>
              <Badge variant="secondary" className="text-[10px] py-0 px-1 font-mono">
                {getCatalogAssets('BDR').length}
              </Badge>
            </TabsTrigger>
          </TabsList>
        </Tabs>

        <div className="text-xs text-muted-foreground">
          Dados mockados da B3 atualizados para visualização
        </div>
      </div>

      {/* KPI Stats Bar */}
      <CatalogKpiBar stats={stats} totalAssetsCount={categoryAssets.length} />

      {/* Multi-criteria Filter Bar */}
      <CatalogFilters
        category={tab}
        search={search}
        onSearchChange={(val) => void setSearch(val || null)}
        manager={manager}
        onManagerChange={(val) => void setManager(val === 'all' ? null : val)}
        subCategory={subCategory}
        onSubCategoryChange={(val) => void setSubCategory(val === 'all' ? null : val)}
        managerOptions={filterOptions.managers}
        subCategoryOptions={filterOptions.subCategories}
        totalFiltered={filteredAssets.length}
        totalAvailable={categoryAssets.length}
        onResetFilters={handleResetFilters}
      />

      {/* TanStack Table Grid */}
      <CatalogTable category={tab} data={filteredAssets} />
    </div>
  );
}
