'use client';

import { useQuery, type UseQueryOptions, type UseQueryResult } from '@tanstack/react-query';
import {
  fetchAssetDetail,
  fetchAssetPerformance,
  fetchAssetQuotes,
  type AssetDetailDto,
  type PerformanceQueryOptions,
  type PerformanceResponse,
  type QuoteItem,
  type QuoteQueryOptions,
} from '@/lib/api-client';

export const assetKeys = {
  all: ['assets'] as const,
  detail: (ticker: string) => [...assetKeys.all, ticker.toUpperCase(), 'detail'] as const,
  quotes: (ticker: string, options?: QuoteQueryOptions) =>
    [...assetKeys.all, ticker.toUpperCase(), 'quotes', options ?? {}] as const,
  performance: (ticker: string, options?: PerformanceQueryOptions) =>
    [...assetKeys.all, ticker.toUpperCase(), 'performance', options ?? {}] as const,
};

type AssetDetailQueryOptions = Omit<
  UseQueryOptions<AssetDetailDto, Error, AssetDetailDto, ReturnType<typeof assetKeys.detail>>,
  'queryKey' | 'queryFn'
>;

type AssetQuotesQueryOptions = Omit<
  UseQueryOptions<QuoteItem[], Error, QuoteItem[], ReturnType<typeof assetKeys.quotes>>,
  'queryKey' | 'queryFn'
>;

type AssetPerformanceQueryOptions = Omit<
  UseQueryOptions<
    PerformanceResponse,
    Error,
    PerformanceResponse,
    ReturnType<typeof assetKeys.performance>
  >,
  'queryKey' | 'queryFn'
>;

export function useAssetDetailQuery(
  ticker: string,
  options?: AssetDetailQueryOptions,
): UseQueryResult<AssetDetailDto, Error> {
  const normalizedTicker = ticker.trim().toUpperCase();

  return useQuery({
    queryKey: assetKeys.detail(normalizedTicker),
    queryFn: () => fetchAssetDetail(normalizedTicker),
    enabled: normalizedTicker.length > 0,
    staleTime: 5 * 60 * 1000,
    ...options,
  });
}

export function useAssetQuotesQuery(
  ticker: string,
  request?: QuoteQueryOptions,
  options?: AssetQuotesQueryOptions,
): UseQueryResult<QuoteItem[], Error> {
  const normalizedTicker = ticker.trim().toUpperCase();

  return useQuery({
    queryKey: assetKeys.quotes(normalizedTicker, request),
    queryFn: () => fetchAssetQuotes(normalizedTicker, request),
    enabled: normalizedTicker.length > 0,
    staleTime: 15 * 60 * 1000,
    ...options,
  });
}

export function useAssetPerformanceQuery(
  ticker: string,
  request?: PerformanceQueryOptions,
  options?: AssetPerformanceQueryOptions,
): UseQueryResult<PerformanceResponse, Error> {
  const normalizedTicker = ticker.trim().toUpperCase();

  return useQuery({
    queryKey: assetKeys.performance(normalizedTicker, request),
    queryFn: () => fetchAssetPerformance(normalizedTicker, request),
    enabled: normalizedTicker.length > 0,
    staleTime: 15 * 60 * 1000,
    ...options,
  });
}

// Short aliases are useful in detail pages while retaining explicit exports above.
export const useAssetQuery = useAssetDetailQuery;
export const useQuotesQuery = useAssetQuotesQuery;
export const usePerformanceQuery = useAssetPerformanceQuery;
