'use client';

import * as React from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useSession } from '@/hooks/use-session';
import {
  clonePublicPortfolio,
  fetchPortfolios,
  fetchPortfolioSummary,
  type PortfolioSummaryDto,
} from '@/lib/api-client';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

const riskLabel: Record<string, string> = {
  conservador: 'Conservadora',
  moderado: 'Moderada',
  arrojado: 'Arrojada',
};

/**
 * Home do dashboard: visão consolidada de todas as carteiras (M-P1 mínimo —
 * patrimônio, investido e resultado agregados + cards por carteira).
 *
 * `useSearchParams` (clone ?clonar=) exige boundary de Suspense para o prerender
 * estático do shell não quebrar o build.
 */
export default function DashboardPage() {
  return (
    <React.Suspense fallback={<DashboardSkeleton />}>
      <DashboardHome />
    </React.Suspense>
  );
}

function DashboardHome() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const { isAuthenticated, isReady } = useSession();

  React.useEffect(() => {
    if (isReady && !isAuthenticated) router.replace('/entrar');
  }, [isReady, isAuthenticated, router]);

  // Clone de carteira pública vindo do CTA da página /c/[slug]
  const cloneSlug = searchParams.get('clonar');
  const cloneStarted = React.useRef(false);
  React.useEffect(() => {
    if (!isReady || !isAuthenticated || !cloneSlug || cloneStarted.current) return;
    cloneStarted.current = true;
    clonePublicPortfolio(cloneSlug)
      .then((newId) => {
        toast.success('Carteira importada!');
        queryClient.invalidateQueries({ queryKey: ['portfolios'] });
        router.replace(`/dashboard/c/${newId}`);
      })
      .catch((error: Error) => {
        toast.error(error.message);
        router.replace('/dashboard');
      });
  }, [isReady, isAuthenticated, cloneSlug, queryClient, router]);

  const summariesQuery = useQuery({
    queryKey: ['portfolios', 'consolidated'],
    queryFn: async (): Promise<PortfolioSummaryDto[]> => {
      const portfolios = await fetchPortfolios();
      return Promise.all(portfolios.map((p) => fetchPortfolioSummary(p.id)));
    },
    enabled: isReady && isAuthenticated,
  });

  if (!isReady || !isAuthenticated) return <DashboardSkeleton />;

  const summaries = summariesQuery.data;
  const totalValue = summaries?.reduce((acc, s) => acc + s.totalValue, 0) ?? 0;
  const totalInvested = summaries?.reduce((acc, s) => acc + s.totalInvested, 0) ?? 0;
  const unrealized = summaries?.reduce((acc, s) => acc + s.unrealizedPnl, 0) ?? 0;

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6 px-4 py-8">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Suas carteiras</h1>
          <p className="text-sm text-muted-foreground">
            Visão consolidada de todos os seus investimentos.
          </p>
        </div>
        <Button render={<Link href="/dashboard/carteiras/nova" />}>Nova carteira</Button>
      </header>

      {summariesQuery.isLoading ? (
        <DashboardSkeleton />
      ) : summariesQuery.isError ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-4 py-14 text-center">
            <p className="max-w-md text-sm text-muted-foreground">
              Não foi possível carregar suas carteiras agora. Tente novamente em instantes.
            </p>
            <Button variant="outline" onClick={() => void summariesQuery.refetch()}>
              Tentar novamente
            </Button>
          </CardContent>
        </Card>
      ) : !summaries || summaries.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center gap-4 py-14 text-center">
            <p className="max-w-md text-sm text-muted-foreground">
              Você ainda não tem carteiras. Crie a primeira para começar a acompanhar patrimônio,
              rentabilidade e impostos em um só lugar.
            </p>
            <Button render={<Link href="/dashboard/carteiras/nova" />}>
              Criar primeira carteira
            </Button>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Hero consolidado */}
          <section className="grid grid-cols-1 gap-4 sm:grid-cols-3">
            <Card>
              <CardHeader className="pb-1">
                <p className="text-xs text-muted-foreground">Patrimônio total</p>
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-semibold tabular-nums">{brl.format(totalValue)}</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  Investido: {brl.format(totalInvested)}
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-1">
                <p className="text-xs text-muted-foreground">Resultado não realizado</p>
              </CardHeader>
              <CardContent>
                <p
                  className={`text-2xl font-semibold tabular-nums ${
                    unrealized >= 0 ? 'text-emerald-600' : 'text-red-600'
                  }`}
                >
                  {brl.format(unrealized)}
                </p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {totalInvested > 0
                    ? `${((unrealized / totalInvested) * 100).toLocaleString('pt-BR', {
                        minimumFractionDigits: 2,
                        maximumFractionDigits: 2,
                      })}% sobre o investido`
                    : '—'}
                </p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-1">
                <p className="text-xs text-muted-foreground">Renda recebida</p>
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-semibold tabular-nums">
                  {brl.format(summaries.reduce((acc, s) => acc + s.incomeReceived, 0))}
                </p>
                <p className="mt-1 text-xs text-muted-foreground">Proventos e juros das posições</p>
              </CardContent>
            </Card>
          </section>

          {/* Cards por carteira */}
          <section className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
            {summaries.map((s) => (
              <Link key={s.portfolio.id} href={`/dashboard/c/${s.portfolio.id}`} className="group">
                <Card className="h-full transition-colors group-hover:border-primary/50">
                  <CardHeader className="pb-2">
                    <div className="flex items-center justify-between gap-2">
                      <CardTitle className="truncate text-base">{s.portfolio.title}</CardTitle>
                      <Badge variant="secondary">{riskLabel[s.portfolio.riskProfile] ?? '—'}</Badge>
                    </div>
                  </CardHeader>
                  <CardContent className="space-y-1 text-sm">
                    <p className="font-medium tabular-nums">{brl.format(s.totalValue)}</p>
                    <p
                      className={`tabular-nums ${
                        s.unrealizedPnl >= 0 ? 'text-emerald-600' : 'text-red-600'
                      }`}
                    >
                      {s.unrealizedPnl >= 0 ? '+' : ''}
                      {brl.format(s.unrealizedPnl)} não realizado
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {s.positions.length} posição(ões)
                    </p>
                  </CardContent>
                </Card>
              </Link>
            ))}
          </section>
        </>
      )}
    </div>
  );
}

function DashboardSkeleton() {
  return (
    <div className="mx-auto w-full max-w-6xl space-y-6 px-4 py-8" aria-busy>
      <Skeleton className="h-8 w-48" />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-28" />
        ))}
      </div>
    </div>
  );
}
