'use client';

import * as React from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { useQueryState, parseAsStringLiteral } from 'nuqs';
import {
  fetchAssetRankings,
  RANKING_METRICS,
  type AssetRankingDto,
  type RankingsMetric,
} from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { cn } from '@/lib/utils';
import { ArrowDownWideNarrow, ArrowUpNarrowWide, Trophy } from 'lucide-react';

const TIPOS = ['TODOS', 'ETF', 'BDR', 'FII'] as const;
const DIRECOES = ['desc', 'asc'] as const;

const METRIC_LABELS: Record<RankingsMetric, string> = {
  retorno12m: 'Retorno 12 meses',
  retorno30d: 'Retorno 30 dias',
  retorno6m: 'Retorno 6 meses',
  retornoano: 'Retorno no ano',
  variacaodia: 'Variação do dia',
  volatilidade: 'Volatilidade anual',
  sharpe: 'Sharpe',
  drawdown: 'Drawdown máximo',
  volume: 'Volume médio diário',
};

const EMPTY_ROWS: AssetRankingDto[] = [];

function tipoParam(tipo: string): string | undefined {
  return tipo === 'TODOS' ? undefined : tipo;
}

function detailHref(row: AssetRankingDto): string {
  const type = row.assetType.toUpperCase();
  const segment =
    type.includes('FII') || type.includes('REAL_ESTATE')
      ? 'fii'
      : type.includes('BDR')
        ? 'bdr'
        : 'etf';
  return `/${segment}/${encodeURIComponent(row.ticker.toLowerCase())}`;
}

function signed(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  return `${value > 0 ? '+' : ''}${formatPercent(value)}`;
}

function optionalCurrency(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  return formatCurrencyBRL(value);
}

export function RankingsExplorer() {
  const [tipo, setTipo] = useQueryState('tipo', parseAsStringLiteral(TIPOS).withDefault('TODOS'));
  const [metrica, setMetrica] = useQueryState(
    'metrica',
    parseAsStringLiteral(RANKING_METRICS).withDefault('retorno12m'),
  );
  const [direcao, setDirecao] = useQueryState(
    'direcao',
    parseAsStringLiteral(DIRECOES).withDefault('desc'),
  );

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['rankings', tipo, metrica, direcao],
    queryFn: () =>
      fetchAssetRankings({
        assetType: tipoParam(tipo),
        metric: metrica,
        orderDirection: direcao,
      }),
    staleTime: 60_000,
  });

  const rows = data ?? EMPTY_ROWS;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col justify-between gap-4 border-b pb-4 lg:flex-row lg:items-center">
        <Tabs value={tipo} onValueChange={(value) => void setTipo(value as (typeof TIPOS)[number])}>
          <TabsList className="h-10 p-1">
            {TIPOS.map((item) => (
              <TabsTrigger key={item} value={item} className="px-3 text-xs sm:text-sm">
                {item === 'TODOS'
                  ? 'Todos'
                  : item === 'BDR'
                    ? 'BDRs'
                    : item === 'FII'
                      ? 'FIIs'
                      : 'ETFs'}
              </TabsTrigger>
            ))}
          </TabsList>
        </Tabs>
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs font-medium text-muted-foreground">Ordenar por</span>
          {RANKING_METRICS.map((metric) => (
            <Button
              key={metric}
              variant={metric === metrica ? 'default' : 'outline'}
              size="sm"
              className="h-7 px-2.5 text-xs"
              onClick={() => void setMetrica(metric)}
            >
              {METRIC_LABELS[metric]}
            </Button>
          ))}
          <Button
            variant="outline"
            size="sm"
            className="h-7 gap-1.5 px-2.5 text-xs"
            aria-label={direcao === 'desc' ? 'Ordenação decrescente' : 'Ordenação crescente'}
            onClick={() => void setDirecao(direcao === 'desc' ? 'asc' : 'desc')}
          >
            {direcao === 'desc' ? (
              <ArrowDownWideNarrow className="size-3.5" />
            ) : (
              <ArrowUpNarrowWide className="size-3.5" />
            )}
            {direcao === 'desc' ? 'Maior primeiro' : 'Menor primeiro'}
          </Button>
        </div>
      </div>

      {isError ? (
        <div
          role="alert"
          className="flex items-center justify-between gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive"
        >
          <span>Não foi possível carregar o ranking agora. Tente novamente em instantes.</span>
          <button
            type="button"
            onClick={() => void refetch()}
            className="rounded-md border border-destructive/30 px-3 py-1.5 text-xs font-medium text-destructive hover:bg-destructive/10"
          >
            Tentar novamente
          </button>
        </div>
      ) : null}

      {!isError && isLoading ? (
        <p className="py-8 text-center text-sm text-muted-foreground">Calculando ranking…</p>
      ) : null}

      {!isError && !isLoading && rows.length === 0 ? (
        <p className="py-8 text-center text-sm text-muted-foreground">
          Ainda não há dados suficientes para este ranking.
        </p>
      ) : null}

      {!isError && rows.length > 0 ? (
        <div className="overflow-x-auto rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-14 text-center">#</TableHead>
                <TableHead>Ticker</TableHead>
                <TableHead className="hidden min-w-56 md:table-cell">Nome</TableHead>
                <TableHead className="text-right">Cotação</TableHead>
                <TableHead className="text-right">Dia</TableHead>
                <TableHead className="text-right">30 dias</TableHead>
                <TableHead className="text-right">No ano</TableHead>
                <TableHead className="text-right">12 meses</TableHead>
                <TableHead className="hidden text-right lg:table-cell">Vol. anual</TableHead>
                <TableHead className="hidden text-right lg:table-cell">Sharpe</TableHead>
                <TableHead className="hidden text-right xl:table-cell">Vol. médio (R$)</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((row) => (
                <TableRow key={row.ticker}>
                  <TableCell className="text-center">
                    <Badge
                      variant={row.rank <= 3 ? 'default' : 'secondary'}
                      className={cn('justify-center px-1.5 py-0 font-mono text-[11px]')}
                    >
                      {row.rank <= 3 ? <Trophy className="mr-0.5 size-3" /> : null}
                      {row.rank}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Link
                      href={detailHref(row)}
                      className="font-mono text-sm font-semibold text-primary hover:underline"
                    >
                      {row.ticker}
                    </Link>
                  </TableCell>
                  <TableCell className="hidden max-w-72 truncate text-sm text-muted-foreground md:table-cell">
                    {row.name}
                  </TableCell>
                  <TableCell className="text-right font-mono text-sm">
                    {optionalCurrency(row.lastPrice)}
                  </TableCell>
                  <TableCell
                    className={cn(
                      'text-right font-mono text-sm',
                      (row.changeDayPercent ?? 0) > 0 && 'text-emerald-600 dark:text-emerald-400',
                      (row.changeDayPercent ?? 0) < 0 && 'text-red-600 dark:text-red-400',
                    )}
                  >
                    {signed(row.changeDayPercent)}
                  </TableCell>
                  <TableCell
                    className={cn(
                      'text-right font-mono text-sm',
                      row.return30dPercent !== null &&
                        (row.return30dPercent > 0
                          ? 'text-emerald-600 dark:text-emerald-400'
                          : row.return30dPercent < 0
                            ? 'text-red-600 dark:text-red-400'
                            : undefined),
                    )}
                  >
                    {signed(row.return30dPercent)}
                  </TableCell>
                  <TableCell className="text-right font-mono text-sm">
                    {signed(row.returnYtdPercent)}
                  </TableCell>
                  <TableCell className="text-right font-mono text-sm font-semibold">
                    {signed(row.return12mPercent)}
                  </TableCell>
                  <TableCell className="hidden text-right font-mono text-sm lg:table-cell">
                    {signed(row.annualizedVolatilityPercent)}
                  </TableCell>
                  <TableCell className="hidden text-right font-mono text-sm lg:table-cell">
                    {row.sharpeRatio ?? '—'}
                  </TableCell>
                  <TableCell className="hidden text-right font-mono text-sm xl:table-cell">
                    {optionalCurrency(row.avgVolume30D)}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : null}

      <p className="text-xs leading-relaxed text-muted-foreground">
        Rankings calculados com base nas cotações locais consolidadas. Métricas de risco usam a
        série histórica disponível; ativos sem histórico suficiente ficam ao final da lista.
        Conteúdo educacional — não é recomendação de investimento.
      </p>
    </div>
  );
}
