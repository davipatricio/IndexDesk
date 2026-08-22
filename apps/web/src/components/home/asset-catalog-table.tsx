'use client';

import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { fetchAssets } from '@/lib/api-client';
import { CatalogTable, getAssetCategory } from '@/components/catalog/catalog-table';

export function AssetCatalogTable() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['assets'],
    queryFn: () => fetchAssets(),
    staleTime: 60_000,
  });
  const assets = (data ?? []).filter((asset) => getAssetCategory(asset) === 'ETF').slice(0, 10);

  if (isError)
    return (
      <div
        role="alert"
        className="rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive"
      >
        Não foi possível carregar os ETFs agora.{' '}
        <button type="button" className="ml-2 underline" onClick={() => void refetch()}>
          Tentar novamente
        </button>
      </div>
    );
  return <CatalogTable category="ETF" data={assets} isLoading={isLoading} />;
}
