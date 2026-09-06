'use client';

import * as React from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { fetchPortfolioSummary, deletePortfolio } from '@/lib/api-client';
import { useSession } from '@/hooks/use-session';
import { PerformancePanel } from '@/components/portfolio/performance-panel';
import { AnalysisPanel } from '@/components/portfolio/analysis-panel';
import { FiscalPanel } from '@/components/portfolio/fiscal-panel';
import { ShareControls } from '@/components/portfolio/share-controls';
import { GoalsPanel } from '@/components/portfolio/goals-panel';
import { PositionsTable } from '@/components/portfolio/positions-table';
import { MaskedValue } from '@/components/privacy/masked-value';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { fetchPortfolioTimeline, type TimelineItemDto } from '@/lib/api-client';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { toast } from 'sonner';
import { useQueryState, parseAsStringLiteral } from 'nuqs';
import { DeleteConfirmation } from '@/components/portfolio/delete-confirmation';

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

/**
 * Data de hoje (UTC) para filtrar vencimentos futuros. Avaliada 1× por carregamento
 * da página — chamar `Date.now()` durante o render é impuro (react/purity), e o card
 * só aparece depois da sessão pronta, então nunca participa do prerender estático.
 */
const TODAY_ISO = new Date().toISOString().slice(0, 10);

/** Detalhe da carteira (M-P1): patrimônio + tabela de posições por custódia. */
export default function CarteiraPage() {
  const router = useRouter();
  const [deleting, setDeleting] = React.useState(false);
  const [tab, setTab] = useQueryState(
    'aba',
    parseAsStringLiteral([
      'posicoes',
      'rentabilidade',
      'analise',
      'fiscal',
      'metas',
    ] as const).withDefault('posicoes'),
  );
  const params = useParams<{ id: string }>();
  const id = params.id;
  const queryClient = useQueryClient();
  const { isAuthenticated, isReady } = useSession();

  const summaryQuery = useQuery({
    queryKey: ['portfolio', id],
    queryFn: () => fetchPortfolioSummary(id),
    enabled: Boolean(isReady && isAuthenticated && id),
  });

  const timelineQuery = useQuery({
    queryKey: ['portfolio', id, 'timeline'],
    queryFn: () => fetchPortfolioTimeline(id),
    enabled: Boolean(isReady && isAuthenticated && id),
    staleTime: 10 * 60 * 1000,
  });

  React.useEffect(() => {
    if (isReady && !isAuthenticated) router.replace('/entrar');
  }, [isReady, isAuthenticated, router]);

  const handleDelete = async () => {
    try {
      await deletePortfolio(id);
      toast.success('Carteira excluída.');
      await queryClient.invalidateQueries({ queryKey: ['portfolios'] });
      router.push('/dashboard');
    } catch {
      toast.error('Não foi possível excluir a carteira. Tente novamente.');
    }
  };

  if (!isReady || !isAuthenticated || summaryQuery.isLoading)
    return <Skeleton className="mx-auto my-8 h-64 w-full max-w-6xl" />;

  if (summaryQuery.isError) {
    return (
      <div className="mx-auto max-w-6xl px-4 py-16 text-center">
        <p className="text-sm text-muted-foreground">
          Não foi possível carregar esta carteira.{' '}
          <Button variant="link" onClick={() => void summaryQuery.refetch()}>
            Tentar novamente
          </Button>
        </p>
        <Button variant="link" render={<Link href="/dashboard" />}>
          Voltar ao dashboard
        </Button>
      </div>
    );
  }

  const s = summaryQuery.data!;

  return (
    <div className="mx-auto w-full max-w-6xl space-y-6 px-4 py-8">
      {deleting && (
        <DeleteConfirmation
          title={s.portfolio.title}
          onConfirm={handleDelete}
          onClose={() => setDeleting(false)}
        />
      )}
      <Link href="/dashboard" className="text-sm text-muted-foreground hover:underline">
        Minhas carteiras
      </Link>
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{s.portfolio.title}</h1>
            <Badge variant="secondary">
              {s.portfolio.visibility === 'private' ? 'Privada' : 'Compartilhada'}
            </Badge>
          </div>
          {s.portfolio.description ? (
            <p className="text-sm text-muted-foreground">{s.portfolio.description}</p>
          ) : null}
        </div>
        <div className="flex flex-wrap gap-2">
          <ShareControls portfolioId={id} initial={{ visibility: s.portfolio.visibility }} />
          <Button variant="outline" onClick={() => setDeleting(true)}>
            Excluir
          </Button>
          <Button render={<Link href={`/dashboard/c/${id}/transacoes/nova`} />}>
            Nova transação
          </Button>
        </div>
      </header>

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Card>
          <CardHeader className="pb-1">
            <CardDescription>Patrimônio</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold tabular-nums">
              <MaskedValue>{brl.format(s.totalValue)}</MaskedValue>
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1">
            <CardDescription>Investido</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-2xl font-semibold tabular-nums">
              <MaskedValue>{brl.format(s.totalInvested)}</MaskedValue>
            </p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-1">
            <CardDescription>Resultado das posições abertas</CardDescription>
          </CardHeader>
          <CardContent>
            <p
              className={`text-2xl font-semibold tabular-nums ${
                s.unrealizedPnl >= 0 ? 'text-emerald-600' : 'text-red-600'
              }`}
            >
              <MaskedValue>{brl.format(s.unrealizedPnl)}</MaskedValue>
            </p>
          </CardContent>
        </Card>
      </section>

      <TimelineCard items={timelineQuery.data ?? []} />

      <Tabs
        value={tab}
        onValueChange={(value) => void setTab(value as typeof tab)}
        className="space-y-4"
      >
        <TabsList className="max-w-full overflow-x-auto">
          <TabsTrigger value="posicoes">Posições</TabsTrigger>
          <TabsTrigger value="rentabilidade">Rentabilidade</TabsTrigger>
          <TabsTrigger value="analise">Análise</TabsTrigger>
          <TabsTrigger value="fiscal">Fiscal</TabsTrigger>
          <TabsTrigger value="metas">Metas</TabsTrigger>
        </TabsList>

        <TabsContent value="posicoes" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Posições</CardTitle>
              <CardDescription>
                Agrupadas por corretora. Preços do último fechamento. Sem cotação, o valor é
                estimado pelo custo.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {s.positions.length === 0 ? (
                <p className="py-8 text-center text-sm text-muted-foreground">
                  Nenhuma posição ainda. Lance a primeira transação.
                </p>
              ) : (
                <PositionsTable positions={s.positions} />
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="rentabilidade">
          <PerformancePanel portfolioId={id} />
        </TabsContent>

        <TabsContent value="analise">
          <AnalysisPanel portfolioId={id} />
        </TabsContent>

        <TabsContent value="fiscal">
          <FiscalPanel portfolioId={id} positions={s.positions} />
        </TabsContent>

        <TabsContent value="metas">
          <GoalsPanel portfolioId={id} totalValue={s.totalValue} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function TimelineCard({ items }: { items: TimelineItemDto[] }) {
  const upcoming = items.filter((i) => i.date >= TODAY_ISO).slice(0, 5);
  if (upcoming.length === 0) return null;

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm">Próximos vencimentos</CardTitle>
        <CardDescription>Carências e vencimentos de renda fixa da carteira.</CardDescription>
      </CardHeader>
      <CardContent>
        <ul className="space-y-1.5">
          {upcoming.map((item) => {
            const days = Math.round(
              (new Date(`${item.date}T12:00:00`).getTime() -
                new Date(`${TODAY_ISO}T12:00:00`).getTime()) /
                86_400_000,
            );
            return (
              <li
                key={`${item.label}-${item.date}`}
                className="flex items-center justify-between text-xs"
              >
                <span className="flex items-center gap-2">
                  <Badge variant={days <= 30 ? 'default' : 'secondary'}>
                    {days <= 0 ? 'hoje' : `${days}d`}
                  </Badge>
                  {item.label}
                  {item.kind === 'liquidity' ? (
                    <span className="text-muted-foreground">(carência)</span>
                  ) : null}
                </span>
                <span className="tabular-nums text-muted-foreground">
                  <MaskedValue>{brl.format(item.amount)}</MaskedValue>
                </span>
              </li>
            );
          })}
        </ul>
      </CardContent>
    </Card>
  );
}
