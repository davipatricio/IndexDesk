'use client';

import * as React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useQueryState, parseAsStringLiteral } from 'nuqs';
import {
  ArrowUpDown,
  ArrowUp,
  ArrowDown,
  MoreHorizontal,
  Plus,
  TrendingUp,
  FolderPlus,
  RefreshCw,
  ExternalLink,
  Trash2,
  Lock,
  Globe,
  Share2,
} from 'lucide-react';
import { toast } from 'sonner';

import {
  usePortfoliosList,
  portfolioInitial,
  portfolioAvatarStyle,
} from '@/hooks/use-portfolios-list';
import { dashboardQParser } from '@/components/layout/dashboard-header';
import { MaskedValue, BlurChart } from '@/components/privacy/masked-value';
import { Sparkline } from '@/components/charts/sparkline';
import { DeleteConfirmation } from '@/components/portfolio/delete-confirmation';
import { deletePortfolio, type PortfolioDto } from '@/lib/api-client';
import { useSession } from '@/hooks/use-session';
import { useQueryClient } from '@tanstack/react-query';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

const SORT_COLUMNS = ['value', 'month', 'year'] as const;
type SortColumn = (typeof SORT_COLUMNS)[number];

const SORT_DIRECTIONS = ['asc', 'desc'] as const;

const sortParser = parseAsStringLiteral(SORT_COLUMNS).withDefault('value');
const dirParser = parseAsStringLiteral(SORT_DIRECTIONS).withDefault('desc');

const brlFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const percentFormatter = new Intl.NumberFormat('pt-BR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  signDisplay: 'always',
});

function formatPercent(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—';
  }
  return `${percentFormatter.format(value)}%`;
}

function getLatestValue(portfolio: PortfolioDto): number {
  if (portfolio.series30d && portfolio.series30d.length > 0) {
    const last = portfolio.series30d[portfolio.series30d.length - 1];
    if (typeof last === 'number') return last;
  }
  return 0;
}

export function PortfoliosListTable() {
  const router = useRouter();
  const [deleting, setDeleting] = React.useState<{ id: string; title: string } | null>(null);
  const queryClient = useQueryClient();
  const { isAuthenticated, isReady } = useSession();
  const { portfolios, isLoading, isError, refetch } = usePortfoliosList();

  const [q] = useQueryState('q', dashboardQParser);
  const [sort, setSort] = useQueryState('sort', sortParser);
  const [dir, setDir] = useQueryState('dir', dirParser);

  React.useEffect(() => {
    if (isReady && !isAuthenticated) {
      router.replace('/entrar');
    }
  }, [isReady, isAuthenticated, router]);

  const handleSort = (column: SortColumn) => {
    if (sort === column) {
      void setDir(dir === 'asc' ? 'desc' : 'asc');
    } else {
      void setSort(column);
      void setDir('desc');
    }
  };

  const handleDelete = async (id: string, title: string) => {
    if (!deleting) {
      setDeleting({ id, title });
      return;
    }
    try {
      await deletePortfolio(id);
      toast.success('Carteira excluída.');
      await queryClient.invalidateQueries({ queryKey: ['portfolios'] });
    } catch {
      toast.error('Não foi possível excluir a carteira. Tente novamente.');
    }
  };

  const filteredPortfolios = React.useMemo(() => {
    const list = portfolios ?? [];
    const term = (q || '').trim().toLowerCase();
    const matches = term ? list.filter((p) => p.title.toLowerCase().includes(term)) : list;

    return [...matches].sort((a, b) => {
      let valA = 0;
      let valB = 0;

      if (sort === 'value') {
        valA = getLatestValue(a);
        valB = getLatestValue(b);
      } else if (sort === 'month') {
        valA = a.returnPercentMonth ?? -Infinity;
        valB = b.returnPercentMonth ?? -Infinity;
      } else if (sort === 'year') {
        valA = a.returnPercentTotal ?? -Infinity;
        valB = b.returnPercentTotal ?? -Infinity;
      }

      if (valA === valB) return 0;
      return dir === 'asc' ? (valA > valB ? 1 : -1) : valA < valB ? 1 : -1;
    });
  }, [portfolios, q, sort, dir]);

  // Loading state
  if (!isReady || isLoading) {
    return <PortfoliosLoadingSkeleton />;
  }

  // Error state
  if (isError) {
    return (
      <div className="flex flex-col items-center justify-center rounded-xl border border-destructive/20 bg-card p-12 text-center shadow-xs">
        <div className="flex size-12 items-center justify-center rounded-full bg-destructive/10 text-destructive">
          <RefreshCw className="size-6" />
        </div>
        <h3 className="mt-4 font-heading text-lg font-semibold text-foreground">
          Erro ao carregar carteiras
        </h3>
        <p className="mt-1.5 max-w-sm text-sm text-muted-foreground">
          Não foi possível carregar suas carteiras agora. Tente novamente.
        </p>
        <Button variant="outline" className="mt-6 gap-2" onClick={() => void refetch()}>
          <RefreshCw className="size-4" />
          Tentar novamente
        </Button>
      </div>
    );
  }

  // Empty state (0 portfolios created yet)
  if (!portfolios || portfolios.length === 0) {
    return <PortfoliosEmptyGhostState />;
  }

  const renderSortIcon = (column: SortColumn) => {
    if (sort !== column) {
      return <ArrowUpDown className="ml-1.5 size-3.5 opacity-40 group-hover:opacity-100" />;
    }
    return dir === 'asc' ? (
      <ArrowUp className="ml-1.5 size-3.5 text-primary" />
    ) : (
      <ArrowDown className="ml-1.5 size-3.5 text-primary" />
    );
  };

  const hasSearchFilter = Boolean((q || '').trim());

  return (
    <div className="space-y-4">
      {deleting && (
        <DeleteConfirmation
          title={deleting.title}
          onClose={() => setDeleting(null)}
          onConfirm={() => handleDelete(deleting.id, deleting.title)}
        />
      )}
      {/* Header com total e CTA de criar carteira */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold tracking-tight text-foreground">Carteiras</h2>
          <p className="text-xs text-muted-foreground">
            {portfolios.length}{' '}
            {portfolios.length === 1 ? 'carteira cadastrada' : 'carteiras cadastradas'}
            {hasSearchFilter ? ` · ${filteredPortfolios.length} encontrada(s)` : ''}
          </p>
        </div>
        <Button
          render={<Link href="/dashboard/carteiras/nova" />}
          nativeButton={false}
          size="sm"
          className="gap-1.5"
        >
          <Plus className="size-4" />
          Nova carteira
        </Button>
      </div>

      {hasSearchFilter && filteredPortfolios.length === 0 ? (
        <div className="rounded-xl border border-dashed bg-card/50 p-8 text-center">
          <p className="text-sm text-muted-foreground">
            Nenhuma carteira encontrada com o termo &ldquo;{q}&rdquo;.
          </p>
        </div>
      ) : (
        <>
          {/* Desktop Table View (>= md) */}
          <div className="hidden overflow-hidden rounded-xl border bg-card shadow-xs md:block">
            <Table>
              <TableHeader>
                <TableRow className="hover:bg-transparent">
                  <TableHead className="w-[300px] pl-4">Carteira</TableHead>
                  <TableHead className="text-right">
                    <button
                      type="button"
                      onClick={() => handleSort('value')}
                      className="group inline-flex items-center justify-end font-medium text-muted-foreground transition-colors hover:text-foreground"
                    >
                      Patrimônio
                      {renderSortIcon('value')}
                    </button>
                  </TableHead>
                  <TableHead className="text-right">
                    <button
                      type="button"
                      onClick={() => handleSort('month')}
                      className="group inline-flex items-center justify-end font-medium text-muted-foreground transition-colors hover:text-foreground"
                    >
                      Rent. mês
                      {renderSortIcon('month')}
                    </button>
                  </TableHead>
                  <TableHead className="text-right">
                    <button
                      type="button"
                      onClick={() => handleSort('year')}
                      className="group inline-flex items-center justify-end font-medium text-muted-foreground transition-colors hover:text-foreground"
                    >
                      Retorno total
                      {renderSortIcon('year')}
                    </button>
                  </TableHead>
                  <TableHead className="w-[140px] text-center">Evolução 30d</TableHead>
                  <TableHead className="w-[50px] pr-4">
                    <span className="sr-only">Ações</span>
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredPortfolios.map((portfolio) => {
                  const latestVal = getLatestValue(portfolio);
                  const retMonth = portfolio.returnPercentMonth;
                  const retTotal = portfolio.returnPercentTotal;
                  const series = portfolio.series30d ?? [];

                  return (
                    <TableRow
                      key={portfolio.id}
                      className="cursor-pointer group hover:bg-muted/40 transition-colors"
                      onClick={() => router.push(`/dashboard/c/${portfolio.id}`)}
                    >
                      {/* Nome + Avatar Cor + Visibilidade */}
                      <TableCell className="pl-4 font-medium">
                        <div className="flex items-center gap-3">
                          <Avatar
                            className="size-8 shrink-0 rounded-lg"
                            style={portfolioAvatarStyle(portfolio.id)}
                          >
                            <AvatarFallback className="rounded-lg text-xs font-semibold">
                              {portfolioInitial(portfolio.title)}
                            </AvatarFallback>
                          </Avatar>
                          <div className="min-w-0">
                            <div className="flex items-center gap-2">
                              <Link
                                href={`/dashboard/c/${portfolio.id}`}
                                className="truncate font-semibold text-foreground group-hover:text-primary transition-colors"
                                onClick={(event) => event.stopPropagation()}
                              >
                                {portfolio.title}
                              </Link>
                              {portfolio.visibility === 'public' ? (
                                <Badge
                                  variant="outline"
                                  className="gap-1 text-[10px] py-0 px-1.5 font-normal text-muted-foreground"
                                >
                                  <Globe className="size-2.5" />
                                  Pública
                                </Badge>
                              ) : portfolio.visibility === 'link' ? (
                                <Badge
                                  variant="outline"
                                  className="gap-1 text-[10px] py-0 px-1.5 font-normal text-muted-foreground"
                                >
                                  <Share2 className="size-2.5" />
                                  Link
                                </Badge>
                              ) : (
                                <Badge
                                  variant="outline"
                                  className="gap-1 text-[10px] py-0 px-1.5 font-normal text-muted-foreground"
                                >
                                  <Lock className="size-2.5" />
                                  Privada
                                </Badge>
                              )}
                            </div>
                            {portfolio.description ? (
                              <p className="line-clamp-1 text-xs text-muted-foreground font-normal">
                                {portfolio.description}
                              </p>
                            ) : null}
                          </div>
                        </div>
                      </TableCell>

                      {/* Patrimônio (MaskedMoney) */}
                      <TableCell className="text-right">
                        <MaskedValue className="font-mono font-medium text-foreground">
                          {brlFormatter.format(latestVal)}
                        </MaskedValue>
                      </TableCell>

                      {/* Rent. mês % */}
                      <TableCell className="text-right">
                        <MaskedValue>
                          <span
                            className={
                              retMonth === null || retMonth === undefined
                                ? 'font-mono text-muted-foreground'
                                : retMonth > 0
                                  ? 'font-mono font-medium text-[#16a34a] dark:text-emerald-400'
                                  : retMonth < 0
                                    ? 'font-mono font-medium text-[#dc2626] dark:text-rose-400'
                                    : 'font-mono text-muted-foreground'
                            }
                          >
                            {formatPercent(retMonth)}
                          </span>
                        </MaskedValue>
                      </TableCell>

                      {/* Retorno total % */}
                      <TableCell className="text-right">
                        <MaskedValue>
                          <span
                            className={
                              retTotal === null || retTotal === undefined
                                ? 'font-mono text-muted-foreground'
                                : retTotal > 0
                                  ? 'font-mono font-medium text-[#16a34a] dark:text-emerald-400'
                                  : retTotal < 0
                                    ? 'font-mono font-medium text-[#dc2626] dark:text-rose-400'
                                    : 'font-mono text-muted-foreground'
                            }
                          >
                            {formatPercent(retTotal)}
                          </span>
                        </MaskedValue>
                      </TableCell>

                      {/* Sparkline 30d envolto em BlurChart */}
                      <TableCell className="text-center" onClick={(e) => e.stopPropagation()}>
                        <BlurChart className="flex justify-center">
                          <Sparkline data={series} width={110} height={26} />
                        </BlurChart>
                      </TableCell>

                      {/* Ações / Dropdown */}
                      <TableCell className="pr-4 text-right" onClick={(e) => e.stopPropagation()}>
                        <DropdownMenu>
                          <DropdownMenuTrigger
                            render={
                              <Button
                                variant="ghost"
                                size="icon-sm"
                                aria-label={`Ações da carteira ${portfolio.title}`}
                                className="size-8 text-muted-foreground opacity-60 group-hover:opacity-100 hover:text-foreground"
                              >
                                <MoreHorizontal className="size-4" />
                              </Button>
                            }
                          />
                          <DropdownMenuContent align="end" className="w-44">
                            <DropdownMenuItem
                              render={
                                <Link
                                  href={`/dashboard/c/${portfolio.id}`}
                                  className="flex w-full items-center gap-2 cursor-pointer"
                                />
                              }
                            >
                              <ExternalLink className="size-4 text-muted-foreground" />
                              Abrir visão geral
                            </DropdownMenuItem>
                            <DropdownMenuItem
                              render={
                                <Link
                                  href={`/dashboard/c/${portfolio.id}/transacoes/nova`}
                                  className="flex w-full items-center gap-2 cursor-pointer"
                                />
                              }
                            >
                              <Plus className="size-4 text-muted-foreground" />
                              Nova transação
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              variant="destructive"
                              className="cursor-pointer gap-2"
                              onClick={() => void handleDelete(portfolio.id, portfolio.title)}
                            >
                              <Trash2 className="size-4" />
                              Excluir carteira
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>

          {/* Mobile Stacked Card View (< md) */}
          <div className="grid grid-cols-1 gap-3 md:hidden">
            {filteredPortfolios.map((portfolio) => {
              const latestVal = getLatestValue(portfolio);
              const retMonth = portfolio.returnPercentMonth;
              const retTotal = portfolio.returnPercentTotal;
              const series = portfolio.series30d ?? [];

              return (
                <div
                  key={portfolio.id}
                  onClick={() => router.push(`/dashboard/c/${portfolio.id}`)}
                  className="group relative flex flex-col gap-3 rounded-xl border bg-card p-4 transition-colors hover:border-primary/40 active:bg-muted/30"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-2.5 min-w-0">
                      <Avatar
                        className="size-8 shrink-0 rounded-lg"
                        style={portfolioAvatarStyle(portfolio.id)}
                      >
                        <AvatarFallback className="rounded-lg text-xs font-semibold">
                          {portfolioInitial(portfolio.title)}
                        </AvatarFallback>
                      </Avatar>
                      <div className="min-w-0">
                        <h3 className="truncate font-semibold text-foreground text-sm">
                          <Link
                            href={`/dashboard/c/${portfolio.id}`}
                            onClick={(event) => event.stopPropagation()}
                          >
                            {portfolio.title}
                          </Link>
                        </h3>
                        <p className="text-[11px] text-muted-foreground">
                          {portfolio.visibility === 'public'
                            ? 'Pública'
                            : portfolio.visibility === 'link'
                              ? 'Link restrito'
                              : 'Privada'}
                        </p>
                      </div>
                    </div>

                    <div onClick={(e) => e.stopPropagation()}>
                      <DropdownMenu>
                        <DropdownMenuTrigger
                          render={
                            <Button
                              variant="ghost"
                              size="icon-xs"
                              aria-label="Ações"
                              className="text-muted-foreground"
                            >
                              <MoreHorizontal className="size-4" />
                            </Button>
                          }
                        />
                        <DropdownMenuContent align="end" className="w-40">
                          <DropdownMenuItem
                            render={
                              <Link
                                href={`/dashboard/c/${portfolio.id}`}
                                className="flex w-full items-center gap-2"
                              />
                            }
                          >
                            <ExternalLink className="size-3.5" />
                            Ver detalhes
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            render={
                              <Link
                                href={`/dashboard/c/${portfolio.id}/transacoes/nova`}
                                className="flex w-full items-center gap-2"
                              />
                            }
                          >
                            <Plus className="size-3.5" />
                            Nova transação
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem
                            variant="destructive"
                            onClick={() => void handleDelete(portfolio.id, portfolio.title)}
                          >
                            <Trash2 className="size-3.5" />
                            Excluir
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </div>

                  <div className="grid grid-cols-3 gap-2 border-t pt-3">
                    <div>
                      <p className="text-[10px] text-muted-foreground uppercase font-medium">
                        Patrimônio
                      </p>
                      <MaskedValue className="font-mono text-sm font-semibold text-foreground">
                        {brlFormatter.format(latestVal)}
                      </MaskedValue>
                    </div>
                    <div>
                      <p className="text-[10px] text-muted-foreground uppercase font-medium">
                        Rent. mês
                      </p>
                      <MaskedValue>
                        <span
                          className={
                            retMonth === null || retMonth === undefined
                              ? 'font-mono text-xs text-muted-foreground'
                              : retMonth > 0
                                ? 'font-mono text-xs font-semibold text-[#16a34a]'
                                : retMonth < 0
                                  ? 'font-mono text-xs font-semibold text-[#dc2626]'
                                  : 'font-mono text-xs text-muted-foreground'
                          }
                        >
                          {formatPercent(retMonth)}
                        </span>
                      </MaskedValue>
                    </div>
                    <div>
                      <p className="text-[10px] text-muted-foreground uppercase font-medium">
                        Retorno total
                      </p>
                      <MaskedValue>
                        <span
                          className={
                            retTotal === null || retTotal === undefined
                              ? 'font-mono text-xs text-muted-foreground'
                              : retTotal > 0
                                ? 'font-mono text-xs font-semibold text-[#16a34a]'
                                : retTotal < 0
                                  ? 'font-mono text-xs font-semibold text-[#dc2626]'
                                  : 'font-mono text-xs text-muted-foreground'
                          }
                        >
                          {formatPercent(retTotal)}
                        </span>
                      </MaskedValue>
                    </div>
                  </div>

                  {series.length > 0 ? (
                    <BlurChart className="mt-1 w-full">
                      <Sparkline data={series} width={280} height={28} className="w-full" />
                    </BlurChart>
                  ) : null}
                </div>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}

function PortfoliosLoadingSkeleton() {
  return (
    <div className="space-y-4" aria-busy="true">
      <div className="flex items-center justify-between">
        <Skeleton className="h-6 w-32" />
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

function PortfoliosEmptyGhostState() {
  return (
    <div className="relative overflow-hidden rounded-xl border border-dashed bg-card/40 p-8 text-center shadow-xs md:p-12">
      {/* Silhueta da tabela com 3 rows em pulsar blocks sutis */}
      <div
        className="pointer-events-none absolute inset-x-8 top-6 -z-10 opacity-30 blur-[1px]"
        aria-hidden="true"
      >
        <div className="space-y-3">
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              className="flex h-11 items-center justify-between rounded-lg border border-border/40 bg-muted/40 px-4"
            >
              <div className="flex items-center gap-3">
                <div className="size-6 rounded-md bg-muted-foreground/20" />
                <div className="h-3 w-28 rounded-sm bg-muted-foreground/20" />
              </div>
              <div className="h-3 w-20 rounded-sm bg-muted-foreground/20" />
              <div className="h-3 w-14 rounded-sm bg-muted-foreground/20" />
              <div className="h-3 w-20 rounded-sm bg-muted-foreground/20" />
            </div>
          ))}
        </div>
      </div>

      {/* Conteúdo foreground do empty state */}
      <div className="mx-auto flex max-w-md flex-col items-center">
        <div className="flex size-14 items-center justify-center rounded-2xl bg-primary/10 text-primary ring-8 ring-primary/5">
          <FolderPlus className="size-7" />
        </div>

        <h3 className="mt-5 font-heading text-xl font-semibold tracking-tight text-foreground">
          Crie sua primeira carteira
        </h3>

        <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
          Acompanhe patrimônio, rentabilidade mensal, evolução em 30 dias e deduções fiscais com
          cotações locais da B3.
        </p>

        <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
          <Button
            render={<Link href="/dashboard/carteiras/nova" />}
            nativeButton={false}
            className="gap-2 shadow-xs"
          >
            <Plus className="size-4" />
            Criar primeira carteira
          </Button>
          <Button
            variant="outline"
            render={<Link href="/rankings" />}
            nativeButton={false}
            className="gap-1.5"
          >
            <TrendingUp className="size-4 text-muted-foreground" />
            Explorar rankings
          </Button>
        </div>
      </div>
    </div>
  );
}
