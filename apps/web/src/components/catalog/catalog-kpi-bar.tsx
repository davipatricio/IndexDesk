import * as React from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { formatCurrencyBRL } from '@/lib/utils';
import type { CatalogStatsSummary } from '@/lib/catalog-stats';
import { Building2, Gauge, PieChart, Users } from 'lucide-react';

interface CatalogKpiBarProps {
  stats: CatalogStatsSummary;
  totalAssetsCount: number;
}

export function CatalogKpiBar({ stats, totalAssetsCount }: CatalogKpiBarProps) {
  const isFiltered = stats.assetCount !== totalAssetsCount;
  const totalAssets = stats.totalNetAssets == null ? '—' : formatCurrencyBRL(stats.totalNetAssets);
  const shareholders =
    stats.totalShareholders == null
      ? 'Disponível no detalhe'
      : `${stats.totalShareholders.toLocaleString('pt-BR')} investidores`;

  return (
    <div className="grid grid-cols-2 gap-3 sm:gap-4 md:grid-cols-4">
      <Card className="p-3 sm:p-4">
        <CardContent className="flex items-center justify-between gap-2 p-0">
          <div className="min-w-0">
            <span className="text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
              Ativos listados
            </span>
            <div className="flex items-baseline gap-1.5">
              <span className="font-mono text-xl font-bold sm:text-2xl">{stats.assetCount}</span>
              {isFiltered ? (
                <span className="font-mono text-xs text-muted-foreground">
                  / {totalAssetsCount}
                </span>
              ) : null}
            </div>
            <span className="text-[10px] text-muted-foreground">
              {isFiltered ? 'Filtro aplicado' : 'Todos os ativos disponíveis'}
            </span>
          </div>
          <Building2 className="size-4 shrink-0 text-muted-foreground" />
        </CardContent>
      </Card>
      <Card className="p-3 sm:p-4">
        <CardContent className="flex items-center justify-between gap-2 p-0">
          <div className="min-w-0">
            <span className="text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
              PL consolidado
            </span>
            <span className="block truncate font-mono text-lg font-bold sm:text-xl">
              {totalAssets}
            </span>
            <span className="text-[10px] text-muted-foreground">
              Informação disponível na ficha
            </span>
          </div>
          <Gauge className="size-4 shrink-0 text-muted-foreground" />
        </CardContent>
      </Card>
      <Card className="p-3 sm:p-4">
        <CardContent className="flex items-center justify-between gap-2 p-0">
          <div className="min-w-0">
            <span className="text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
              {stats.primaryMetric.label}
            </span>
            <span className="block truncate font-mono text-lg font-bold text-primary sm:text-xl">
              {stats.primaryMetric.value}
            </span>
            <span className="text-[10px] text-muted-foreground">Somente dados disponíveis</span>
          </div>
          <PieChart className="size-4 shrink-0 text-primary" />
        </CardContent>
      </Card>
      <Card className="p-3 sm:p-4">
        <CardContent className="flex items-center justify-between gap-2 p-0">
          <div className="min-w-0">
            <span className="text-[11px] font-medium uppercase tracking-wider text-muted-foreground">
              {stats.secondaryMetric.label}
            </span>
            <span className="block truncate font-mono text-lg font-bold sm:text-xl">
              {stats.secondaryMetric.value}
            </span>
            <span className="text-[10px] text-muted-foreground">{shareholders}</span>
          </div>
          <Users className="size-4 shrink-0 text-muted-foreground" />
        </CardContent>
      </Card>
    </div>
  );
}
