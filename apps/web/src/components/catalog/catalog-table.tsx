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
import type { AssetCategory, CatalogAsset, EtfAsset, FiiAsset, BdrAsset } from '@/lib/mock-catalog';
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
import {
  ArrowUpDown,
  ArrowUp,
  ArrowDown,
  TrendingUp,
  TrendingDown,
  ExternalLink,
} from 'lucide-react';

const features = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  rowSortingFeature,
  filteredRowModel: createFilteredRowModel(),
  sortedRowModel: createSortedRowModel(),
  filterFns: { includesString: filterFn_includesString },
});

type CatalogColumnDef = ColumnDef<typeof features, CatalogAsset>;

interface CatalogTableProps {
  category: AssetCategory;
  data: CatalogAsset[];
  isLoading?: boolean;
}

function getSortIcon(isSorted: false | 'asc' | 'desc') {
  if (isSorted === 'asc') return <ArrowUp className="ml-1 size-3 text-primary" />;
  if (isSorted === 'desc') return <ArrowDown className="ml-1 size-3 text-primary" />;
  return <ArrowUpDown className="ml-1 size-3 text-muted-foreground/60" />;
}

function getAssetDetailHref(asset: CatalogAsset): string {
  const ticker = asset.ticker.toLowerCase();
  switch (asset.category) {
    case 'ETF':
      return `/etf/${ticker}`;
    case 'FII':
      return `/fii/${ticker}`;
    case 'BDR':
      return `/bdr/${ticker}`;
  }
}

export function CatalogTable({ category, data, isLoading = false }: CatalogTableProps) {
  const [sorting, setSorting] = React.useState<SortingState>([]);

  // Columns definition dynamically tailored per AssetCategory
  const columns = React.useMemo<CatalogColumnDef[]>(() => {
    const baseColumns: CatalogColumnDef[] = [
      {
        accessorKey: 'ticker',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            Ticker
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => {
          const asset = row.original;
          const href = getAssetDetailHref(asset);
          return (
            <Link
              href={href}
              className="font-semibold text-primary hover:underline inline-flex items-center gap-1.5 font-mono"
            >
              {asset.ticker}
              <Badge
                variant="outline"
                className="text-[10px] py-0 px-1 font-normal font-sans tracking-tight"
              >
                {asset.category}
              </Badge>
            </Link>
          );
        },
      },
      {
        accessorKey: 'name',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            Nome
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => (
          <div
            className="max-w-[240px] truncate text-xs text-muted-foreground font-medium"
            title={row.original.name}
          >
            {row.original.name}
          </div>
        ),
      },
      {
        accessorKey: 'manager',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            {category === 'BDR' ? 'Emissor' : 'Gestora'}
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => (
          <span className="text-xs text-foreground font-medium">{row.original.manager}</span>
        ),
      },
      {
        accessorKey: 'subCategory',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            {category === 'FII' ? 'Segmento' : 'Categoria'}
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => (
          <span className="text-xs text-muted-foreground">{row.original.subCategory}</span>
        ),
      },
    ];

    // Category specific metrics
    if (category === 'ETF') {
      baseColumns.push(
        {
          id: 'benchmark',
          accessorFn: (row) => (row as EtfAsset).benchmark,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              Benchmark
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => (
            <Badge variant="secondary" className="text-[11px] font-mono">
              {(row.original as EtfAsset).benchmark}
            </Badge>
          ),
        },
        {
          id: 'managementFee',
          accessorFn: (row) => (row as EtfAsset).managementFee,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              Taxa Adm.
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => (
            <span className="text-xs font-mono">
              {formatPercent((row.original as EtfAsset).managementFee)} a.a.
            </span>
          ),
        },
      );
    } else if (category === 'FII') {
      baseColumns.push(
        {
          id: 'dividendYield12m',
          accessorFn: (row) => (row as FiiAsset).dividendYield12m,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              DY (12M)
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => (
            <span className="text-xs font-mono font-semibold text-primary">
              {formatPercent((row.original as FiiAsset).dividendYield12m)}
            </span>
          ),
        },
        {
          id: 'pvp',
          accessorFn: (row) => (row as FiiAsset).pvp,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              P/VP
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => {
            const pvp = (row.original as FiiAsset).pvp;
            return (
              <span
                className={`text-xs font-mono font-semibold ${
                  pvp < 1 ? 'text-positive' : pvp > 1.05 ? 'text-amber-500' : 'text-foreground'
                }`}
              >
                {pvp.toFixed(2)}x
              </span>
            );
          },
        },
        {
          id: 'lastDividend',
          accessorFn: (row) => (row as FiiAsset).lastDividend,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              Últ. Rend.
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => (
            <span className="text-xs font-mono">
              {formatCurrencyBRL((row.original as FiiAsset).lastDividend)}
            </span>
          ),
        },
      );
    } else if (category === 'BDR') {
      baseColumns.push(
        {
          id: 'underlyingAsset',
          accessorFn: (row) => (row as BdrAsset).underlyingAsset,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              Ativo Base
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => (
            <Badge variant="secondary" className="text-[11px] font-mono">
              {(row.original as BdrAsset).underlyingAsset}
            </Badge>
          ),
        },
        {
          id: 'country',
          accessorFn: (row) => (row as BdrAsset).country,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              Origem
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => (
            <span className="text-xs text-muted-foreground">
              {(row.original as BdrAsset).country}
            </span>
          ),
        },
        {
          id: 'managementFee',
          accessorFn: (row) => (row as BdrAsset).managementFee,
          header: ({ column }) => (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
              className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
            >
              Taxa Base
              {getSortIcon(column.getIsSorted())}
            </Button>
          ),
          cell: ({ row }) => {
            const fee = (row.original as BdrAsset).managementFee;
            return (
              <span className="text-xs font-mono">
                {fee > 0 ? `${formatPercent(fee)} a.a.` : '—'}
              </span>
            );
          },
        },
      );
    }

    // Common Pricing & Return Columns
    baseColumns.push(
      {
        accessorKey: 'lastPrice',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            Cotação
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => (
          <span className="text-xs font-mono font-semibold text-foreground">
            {formatCurrencyBRL(row.original.lastPrice)}
          </span>
        ),
      },
      {
        accessorKey: 'changeDayPercent',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            Dia (%)
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => {
          const val = row.original.changeDayPercent;
          const isPos = val >= 0;
          return (
            <span
              className={`inline-flex items-center text-xs font-mono font-semibold ${
                isPos ? 'text-positive' : 'text-negative'
              }`}
            >
              {isPos ? (
                <TrendingUp className="size-3 mr-0.5" />
              ) : (
                <TrendingDown className="size-3 mr-0.5" />
              )}
              {formatPercent(val)}
            </span>
          );
        },
      },
      {
        accessorKey: 'changeYtdPercent',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            YTD (%)
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => {
          const val = row.original.changeYtdPercent;
          return (
            <span
              className={`text-xs font-mono font-semibold ${
                val >= 0 ? 'text-positive' : 'text-negative'
              }`}
            >
              {formatPercent(val)}
            </span>
          );
        },
      },
      {
        accessorKey: 'netAssets',
        header: ({ column }) => (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
            className="-ml-3 h-8 text-xs font-semibold hover:bg-muted/50"
          >
            PL
            {getSortIcon(column.getIsSorted())}
          </Button>
        ),
        cell: ({ row }) => (
          <span className="text-xs font-mono font-medium text-muted-foreground">
            {formatCurrencyBRL(row.original.netAssets)}
          </span>
        ),
      },
      {
        id: 'actions',
        header: '',
        cell: ({ row }) => {
          const href = getAssetDetailHref(row.original);
          return (
            <Link
              href={href}
              className="text-muted-foreground hover:text-primary p-1 rounded-sm inline-flex items-center"
              title={`Ver ficha completa de ${row.original.ticker}`}
            >
              <ExternalLink className="size-3.5" />
            </Link>
          );
        },
      },
    );

    return baseColumns;
  }, [category]);

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
    <div className="rounded-lg border bg-card shadow-sm overflow-hidden">
      <div className="overflow-x-auto">
        <Table>
          <TableHeader className="bg-muted/40">
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead
                    key={header.id}
                    className="text-xs uppercase font-semibold whitespace-nowrap"
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
                  Carregando ativos da B3...
                </TableCell>
              </TableRow>
            ) : table.getRowModel().rows.length ? (
              table.getRowModel().rows.map((row) => (
                <TableRow key={row.id} className="hover:bg-muted/30 transition-colors">
                  {row.getAllCells().map((cell) => (
                    <TableCell key={cell.id} className="whitespace-nowrap py-2.5">
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
