'use client';

import * as React from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
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
import { fetchAssets, type AssetDto } from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { Input } from '@/components/ui/input';
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
import { Search, ArrowUpDown, TrendingUp, TrendingDown } from 'lucide-react';

const features = tableFeatures({
  columnFilteringFeature,
  globalFilteringFeature,
  rowSortingFeature,
  filteredRowModel: createFilteredRowModel(),
  sortedRowModel: createSortedRowModel(),
  filterFns: { includesString: filterFn_includesString },
});

type AssetColumnDef = ColumnDef<typeof features, AssetDto>;
const EMPTY_ASSETS: AssetDto[] = [];

const columns: AssetColumnDef[] = [
  {
    accessorKey: 'ticker',
    header: ({ column }) => (
      <Button
        variant="ghost"
        size="sm"
        onClick={() => column.toggleSorting(column.getIsSorted() === 'asc')}
        className="-ml-3 h-8 text-xs font-semibold"
      >
        Ticker
        <ArrowUpDown className="ml-1.5 size-3" />
      </Button>
    ),
    cell: ({ row }) => (
      <Link
        href={`/etf/${row.original.ticker.toLowerCase()}`}
        className="font-bold text-emerald-500 hover:underline inline-flex items-center gap-1.5"
      >
        {row.original.ticker}
        <Badge variant="outline" className="text-[10px] py-0 px-1 font-normal">
          {row.original.category}
        </Badge>
      </Link>
    ),
  },
  {
    accessorKey: 'name',
    header: 'Nome do Fundo',
    cell: ({ row }) => (
      <div
        className="max-w-[280px] truncate text-xs text-muted-foreground font-medium"
        title={row.original.name}
      >
        {row.original.name}
      </div>
    ),
  },
  {
    accessorKey: 'manager',
    header: 'Gestora',
    cell: ({ row }) => <span className="text-xs">{row.original.manager}</span>,
  },
  {
    accessorKey: 'benchmark',
    header: 'Índice Ref.',
    cell: ({ row }) => (
      <Badge variant="secondary" className="text-[11px] font-mono">
        {row.original.benchmark}
      </Badge>
    ),
  },
  {
    accessorKey: 'managementFee',
    header: 'Taxa Adm.',
    cell: ({ row }) => (
      <span className="text-xs font-mono">{formatPercent(row.original.managementFee)} a.a.</span>
    ),
  },
  {
    accessorKey: 'netAssets',
    header: 'Patrimônio Líquido',
    cell: ({ row }) => (
      <span className="text-xs font-mono font-medium">
        {formatCurrencyBRL(row.original.netAssets)}
      </span>
    ),
  },
  {
    accessorKey: 'lastPrice',
    header: 'Cotação',
    cell: ({ row }) => (
      <span className="text-xs font-mono font-semibold">
        {formatCurrencyBRL(row.original.lastPrice)}
      </span>
    ),
  },
  {
    accessorKey: 'changeDayPercent',
    header: 'Dia (%)',
    cell: ({ row }) => {
      const val = row.original.changeDayPercent;
      const isPos = val >= 0;
      return (
        <span
          className={`inline-flex items-center text-xs font-mono font-semibold ${isPos ? 'text-emerald-500' : 'text-rose-500'}`}
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
    header: 'YTD (%)',
    cell: ({ row }) => {
      const val = row.original.changeYtdPercent;
      return (
        <span
          className={`text-xs font-mono font-semibold ${val >= 0 ? 'text-emerald-500' : 'text-rose-500'}`}
        >
          {formatPercent(val)}
        </span>
      );
    },
  },
];

export function AssetCatalogTable() {
  const [sorting, setSorting] = React.useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = React.useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['assets'],
    queryFn: () => fetchAssets(),
  });
  const assets = data ?? EMPTY_ASSETS;

  const table = useTable(
    {
      key: 'asset-catalog',
      features,
      data: assets,
      columns,
      state: { sorting, globalFilter },
      onSortingChange: setSorting,
      onGlobalFilterChange: setGlobalFilter,
    },
    (state) => ({ sorting: state.sorting, globalFilter: state.globalFilter }),
  );

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div className="relative max-w-sm flex-1">
          <Search className="absolute left-2.5 top-2.5 size-4 text-muted-foreground" />
          <Input
            placeholder="Buscar por ticker, gestora ou nome..."
            value={globalFilter ?? ''}
            onChange={(e) => setGlobalFilter(e.target.value)}
            className="pl-9 text-xs"
          />
        </div>
        <div className="text-xs text-muted-foreground">
          Exibindo{' '}
          <span className="font-semibold text-foreground">{table.getRowModel().rows.length}</span>{' '}
          ativos
        </div>
      </div>

      <div className="rounded-lg border bg-card shadow-sm overflow-hidden">
        <Table>
          <TableHeader className="bg-muted/40">
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map((header) => (
                  <TableHead key={header.id} className="text-xs uppercase font-semibold">
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
                  Carregando catálogo de ETFs...
                </TableCell>
              </TableRow>
            ) : table.getRowModel().rows.length ? (
              table.getRowModel().rows.map((row) => (
                <TableRow key={row.id} className="hover:bg-muted/30 transition-colors">
                  {row.getAllCells().map((cell) => (
                    <TableCell key={cell.id}>
                      <table.FlexRender cell={cell} />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center text-sm text-muted-foreground"
                >
                  Nenhum ETF encontrado para a busca informada.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
