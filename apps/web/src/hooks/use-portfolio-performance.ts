'use client';

import { useQuery } from '@tanstack/react-query';
import { parseAsIsoDate, parseAsStringLiteral, useQueryStates } from 'nuqs';
import { fetchPortfolioPerformance } from '@/lib/api-client';

export const PORTFOLIO_PERIODS = ['1M', '3M', '6M', '1A', 'TUDO'] as const;
export function usePortfolioPerformance(portfolioId: string) {
  const [periodParams, setPeriodParams] = useQueryStates({
    p: parseAsStringLiteral(PORTFOLIO_PERIODS).withDefault('TUDO'),
    de: parseAsIsoDate,
    ate: parseAsIsoDate,
  });
  let from = periodParams.de?.toISOString().slice(0, 10);
  if (!from && periodParams.p !== 'TUDO') {
    const date = new Date();
    date.setDate(date.getDate() - { '1M': 30, '3M': 91, '6M': 182, '1A': 365 }[periodParams.p]);
    from = date.toISOString().slice(0, 10);
  }
  const to = periodParams.ate?.toISOString().slice(0, 10);
  const invalidRange = !!from && !!to && from > to;
  const query = useQuery({
    queryKey: ['portfolio', portfolioId, 'performance', from, to],
    queryFn: () => fetchPortfolioPerformance(portfolioId, { from, to, benchmarks: 'CDI,IBOV' }),
    enabled: !invalidRange,
    staleTime: 5 * 60 * 1000,
  });
  return { query, periodParams, setPeriodParams, invalidRange };
}
