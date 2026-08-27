'use client';

import { useQuery } from '@tanstack/react-query';
import { fetchPortfolios, type PortfolioDto } from '@/lib/api-client';
import { useMounted } from '@/hooks/use-mounted';

/** Mesma família de keys usada pelas páginas do dashboard. */
export const portfolioKeys = {
  all: ['portfolios'] as const,
  list: () => [...portfolioKeys.all, 'list'] as const,
};

/**
 * Lista simples de carteiras para a sidebar. Compartilha a família de cache
 * `['portfolios']` invalidada pelas páginas do dashboard.
 */
export function usePortfoliosList() {
  const mounted = useMounted();
  const query = useQuery<PortfolioDto[]>({
    queryKey: portfolioKeys.list(),
    queryFn: () => fetchPortfolios(),
    enabled: mounted,
    staleTime: 60_000,
    retry: 1,
  });

  return {
    portfolios: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    refetch: query.refetch,
  };
}

const HUES = 24;

/**
 * Hue HSL determinístico a partir do id (hash simples, estável entre renders
 * e entre servidor/cliente — sem Math.random).
 */
export function portfolioHue(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i += 1) {
    hash = (hash * 31 + id.charCodeAt(i)) | 0;
  }
  return (Math.abs(hash) % HUES) * (360 / HUES);
}

export function portfolioAvatarStyle(id: string): React.CSSProperties {
  const hue = portfolioHue(id);
  return {
    backgroundColor: `hsl(${hue} 65% 45% / 0.15)`,
    color: `hsl(${hue} 65% 45%)`,
  };
}

/** Inicial para o avatar da carteira ("FII Ações" → "F"). */
export function portfolioInitial(title: string): string {
  const trimmed = title.trim();
  return (trimmed[0] ?? 'C').toUpperCase();
}
