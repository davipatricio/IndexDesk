import * as React from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { PortfoliosListTable } from '@/components/portfolio/portfolios-list-table';

export const metadata = {
  title: 'Dashboard | IndexDesk',
  description: 'Visão consolidada das suas carteiras e patrimônio na B3.',
};

/**
 * Listagem principal de carteiras do usuário.
 * RSC-first que delega a tabela rica e interativa para PortfoliosListTable (Client Component)
 * dentro de um Suspense boundary para suportar nuqs e query states.
 */
export default function DashboardPage() {
  return (
    <div className="mx-auto w-full max-w-6xl space-y-6 px-4 py-8">
      <React.Suspense fallback={<DashboardLoadingSkeleton />}>
        <PortfoliosListTable />
      </React.Suspense>
    </div>
  );
}

function DashboardLoadingSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true">
      <div className="flex items-center justify-between">
        <Skeleton className="h-7 w-36" />
        <Skeleton className="h-8 w-28" />
      </div>
      <div className="overflow-hidden rounded-xl border bg-card p-4 space-y-3">
        {[0, 1, 2].map((i) => (
          <div key={i} className="flex items-center justify-between gap-4 py-2">
            <div className="flex items-center gap-3">
              <Skeleton className="size-8 rounded-lg" />
              <div className="space-y-1.5">
                <Skeleton className="h-4 w-36" />
                <Skeleton className="h-3 w-20" />
              </div>
            </div>
            <Skeleton className="h-5 w-24" />
            <Skeleton className="h-5 w-16" />
            <Skeleton className="h-5 w-16" />
            <Skeleton className="h-7 w-24" />
          </div>
        ))}
      </div>
    </div>
  );
}
