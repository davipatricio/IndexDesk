'use client';

import * as React from 'react';
import Link from 'next/link';
import {
  columnFilteringFeature,
  createFilteredRowModel,
  createSortedRowModel,
  filterFn_includesString,
  globalFilteringFeature,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type ColumnDef,
  type SortingState,
} from '@tanstack/react-table';
import type { AssetDto } from '@/lib/api-client';
import { useQuery } from '@tanstack/react-query';
import { fetchQuoteSparks } from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent, cn } from '@/lib/utils';
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
import { Sparkline } from '@/components/charts/sparkline';
import {
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  ExternalLink,
  TrendingDown,
  TrendingUp,
} from 'lucide-react';

const features = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  rowSortingFeature,
  filteredRowModel: createFilteredRowModel(),
  sortedRowModel: createSortedRowModel(),
  filterFns: { includesString: filterFn_includesString },
});

type CatalogColumnDef = ColumnDef<typeof features, AssetDto>;
export type CatalogCategory = 'ETF' | 'FII' | 'BDR';

function field(asset: AssetDto, key: string): unknown {
  return (asset as unknown as Record<string, unknown>)[key];
}

function text(asset: AssetDto, ...keys: string[]): string {
  for (const key of keys) {
    const value = field(asset, key);
    if (typeof value === 'string' && value.trim()) return value;
  }
  return '';
}

function number(asset: AssetDto, ...keys: string[]): number | null {
  for (const key of keys) {
    const value = field(asset, key);
    if (typeof value === 'number' && Number.isFinite(value)) return value;
  }
  return null;
}

export function getAssetCategory(asset: AssetDto): CatalogCategory {
  const type = text(asset, 'assetType', 'category').toUpperCase();
  if (type.includes('FII') || type.includes('REAL_ESTATE')) return 'FII';
  if (type.includes('BDR')) return 'BDR';
  return 'ETF';
}

export function getAssetDetailHref(asset: AssetDto): string {
  return `/${getAssetCategory(asset).toLowerCase()}/${encodeURIComponent(asset.ticker.toLowerCase())}`;
}

function getSortIcon(isSorted: false | 'asc' | 'desc') {
  if (isSorted === 'asc') return <ArrowUp className="ml-1 size-3 text-primary" />;
  if (isSorted === 'desc') return <ArrowDown className="ml-1 size-3 text-primary" />;
  return <ArrowUpDown className="ml-1 size-3 text-muted-foreground/60" />;
}

function SortHeader({
  label,
  column,
  align = 'left',
}: {
  label: string;
  column: { getIsSorted: () => false | 'asc' | 'desc'; toggleSorting: (desc?: boolean) => void };
  align?: 'left' | 'right';
}) {
  return (
    <Button
      variant="ghost"
      size="sm"
      onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
      className={cn(
        '-ml-3 h-8 text-xs font-semibold hover:bg-muted/50',
        align === 'right' && 'w-full flex-row-reverse -mr-3 ml-0',
      )}
    >
      {label}
      {getSortIcon(column.getIsSorted())}
    </Button>
  );
}

interface CatalogTableProps {
  category: CatalogCategory;
  data: AssetDto[];
  isLoading?: boolean;
}

export function CatalogTable({ category, data, isLoading = false }: CatalogTableProps) {
  const [sorting, setSorting] = React.useState<SortingState>([]);
  const tickers = React.useMemo(() => data.map((asset) => asset.ticker), [data]);

  const { data: sparks } = useQuery({
    queryKey: ['quotes-batch', 'catalog', category, tickers.join(',')],
    queryFn: () => fetchQuoteSparks(tickers),
    enabled: tickers.length > 0,
    staleTime: 300_000,
  });

  const sortedColumnId = sorting[0]?.id;
  const columns = React.useMemo<CatalogColumnDef[]>(
    () => [
      {
        id: 'ticker',
        accessorFn: (asset) => asset.ticker,
        header: ({ column }) => <SortHeader label="Ticker" column={column} />,
        cell: ({ row }) => (
          <Link
            href={getAssetDetailHref(row.original)}
            className="inline-flex items-center gap-1.5 font-mono font-semibold text-primary hover:underline"
          >
            {row.original.ticker}
            <Badge variant="outline" className="px-1 py-0 text-[10px] font-normal font-sans">
              {getAssetCategory(row.original)}
            </Badge>
          </Link>
        ),
      },
      {
        id: 'name',
        accessorFn: (asset) => asset.name,
        header: ({ column }) => <SortHeader label="Nome" column={column} />,
        cell: ({ row }) => (
          <div
            className="max-w-[260px] truncate text-xs font-medium text-muted-foreground"
            title={row.original.name}
          >
            {row.original.name}
          </div>
        ),
      },
      {
        id: 'benchmark',
        accessorFn: (asset) => text(asset, 'benchmarkSymbol', 'benchmark'),
        header: ({ column }) => <SortHeader label="Benchmark" column={column} />,
        cell: ({ row }) => {
          const value = text(row.original, 'benchmarkSymbol', 'benchmark');
          return value ? (
            <Badge variant="secondary" className="text-[11px] font-mono">
              {value}
            </Badge>
          ) : (
            <span className="text-xs text-muted-foreground">—</span>
          );
        },
      },
      {
        id: 'spark',
        header: () => (
          <span className="text-[11px] tracking-wide text-muted-foreground uppercase">
            Tendência (90d)
          </span>
        ),
        cell: ({ row }) => {
          const series = sparks?.[row.original.ticker.toUpperCase()];
          return series && series.length >= 2 ? (
            <Sparkline values={series.map((point) => point.close)} className="h-7 w-20" />
          ) : null;
        },
      },
      {
        id: 'lastPrice',
        accessorFn: (asset) => number(asset, 'lastPrice'),
        header: ({ column }) => <SortHeader label="Cotação" column={column} align="right" />,
        cell: ({ row }) => {
          const value = number(row.original, 'lastPrice');
          return (
            <span className="block text-right text-xs font-mono font-semibold tabular-nums">
              {value == null ? '—' : formatCurrencyBRL(value)}
            </span>
          );
        },
      },
      {
        id: 'changeDayPercent',
        accessorFn: (asset) => number(asset, 'changeDayPercent'),
        header: ({ column }) => <SortHeader label="Dia (%)" column={column} align="right" />,
        cell: ({ row }) => {
          const value = number(row.original, 'changeDayPercent');
          if (value == null)
            return <span className="block text-right text-xs text-muted-foreground">—</span>;
          const positive = value >= 0;
          return (
            <span
              className={`inline-flex items-center justify-end text-xs font-mono font-semibold tabular-nums ${positive ? 'text-positive' : 'text-negative'}`}
            >
              {positive ? (
                <TrendingUp className="mr-0.5 size-3" />
              ) : (
                <TrendingDown className="mr-0.5 size-3" />
              )}
              {formatPercent(value)}
            </span>
          );
        },
      },
      {
        id: 'return12mPercent',
        accessorFn: (asset) => number(asset, 'return12mPercent', 'changeYtdPercent'),
        header: ({ column }) => <SortHeader label="12 meses" column={column} align="right" />,
        cell: ({ row }) => {
          const value = number(row.original, 'return12mPercent', 'changeYtdPercent');
          return (
            <span
              className={`block text-right text-xs font-mono font-semibold tabular-nums ${value == null ? 'text-muted-foreground' : value >= 0 ? 'text-positive' : 'text-negative'}`}
            >
              {value == null ? '—' : formatPercent(value)}
            </span>
          );
        },
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => (
          <Link
            href={getAssetDetailHref(row.original)}
            className="inline-flex rounded-sm p-1 text-muted-foreground hover:text-primary"
            title={`Ver ficha completa de ${row.original.ticker}`}
          >
            <ExternalLink className="size-3.5" />
          </Link>
        ),
      },
    ],
    [sparks],
  );

  const table = useTable(
    {
      key: `catalog-${category.toLowerCase()}`,
      features,
      data,
      columns,
      state: { sorting },
      onSortingChange: setSorting,
    },
    (state) => ({ sorting: state.sorting }),
  );

  return (
    <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
      <div className="overflow-x-auto">
        <Table>
          <TableHeader className="sticky top-14 z-10 bg-background shadow-[0_1px_0_0_var(--border)]">
            {table.getHeaderGroups().map((group) => (
              <TableRow key={group.id} className="hover:bg-transparent">
                {group.headers.map((header) => (
                  <TableHead
                    key={header.id}
                    className={cn(
                      'whitespace-nowrap text-xs font-semibold uppercase',
                      header.id === sortedColumnId && 'bg-primary/5',
                    )}
                  >
                    {header.isPlaceholder ? null : <table.FlexRender header={header} />}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-32 text-center text-sm text-muted-foreground"
                >
                  Carregando ativos...
                </TableCell>
              </TableRow>
            ) : table.getRowModel().rows.length ? (
              table.getRowModel().rows.map((row) => (
                <TableRow key={row.id} className="transition-colors hover:bg-muted/30">
                  {row.getAllCells().map((cell) => (
                    <TableCell
                      key={cell.id}
                      className={cn(
                        'whitespace-nowrap py-2',
                        cell.column.id === sortedColumnId && 'bg-primary/5',
                      )}
                    >
                      <table.FlexRender cell={cell} />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-28 text-center text-sm text-muted-foreground"
                >
                  Nenhum ativo encontrado para os filtros selecionados.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
