import * as React from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { formatCurrencyBRL } from '@/lib/utils';
import type { CategoryStatsSummary } from '@/lib/mock-catalog';
import { Building2, DollarSign, PieChart, Users } from 'lucide-react';

interface CatalogKpiBarProps {
  stats: CategoryStatsSummary;
  totalAssetsCount: number;
}

export function CatalogKpiBar({ stats, totalAssetsCount }: CatalogKpiBarProps) {
  const isFiltered = stats.assetCount !== totalAssetsCount;

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-3 sm:gap-4">
      {/* 1. Asset count */}
      <Card className="p-3 sm:p-4">
        <CardContent className="p-0 flex items-center justify-between gap-2">
          <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider truncate">
              Ativos Listados
            </span>
            <div className="flex items-baseline gap-1.5">
              <span className="text-xl sm:text-2xl font-bold tracking-tight text-foreground font-mono">
                {stats.assetCount}
              </span>
              {isFiltered && (
                <span className="text-xs text-muted-foreground font-mono">
                  / {totalAssetsCount}
                </span>
              )}
            </div>
            <span className="text-[10px] text-muted-foreground truncate">
              {isFiltered ? 'Filtro aplicado' : 'Universo total'}
            </span>
          </div>
          <div className="rounded-md bg-muted p-2 text-muted-foreground shrink-0">
            <Building2 className="size-4" />
          </div>
        </CardContent>
      </Card>

      {/* 2. Total Net Assets */}
      <Card className="p-3 sm:p-4">
        <CardContent className="p-0 flex items-center justify-between gap-2">
          <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider truncate">
              PL Consolidado
            </span>
            <span className="text-lg sm:text-xl font-bold tracking-tight text-foreground font-mono truncate">
              {formatCurrencyBRL(stats.totalNetAssets)}
            </span>
            <span className="text-[10px] text-muted-foreground truncate">
              Patrimônio sob gestão
            </span>
          </div>
          <div className="rounded-md bg-muted p-2 text-muted-foreground shrink-0">
            <DollarSign className="size-4" />
          </div>
        </CardContent>
      </Card>

      {/* 3. Primary Class Metric (Taxa Média / DY Médio / Ativos Subjacentes) */}
      <Card className="p-3 sm:p-4">
        <CardContent className="p-0 flex items-center justify-between gap-2">
          <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider truncate">
              {stats.primaryMetric.label}
            </span>
            <span className="text-lg sm:text-xl font-bold tracking-tight text-primary font-mono truncate">
              {stats.primaryMetric.value}
            </span>
            <span className="text-[10px] text-muted-foreground truncate">
              Média ponderada do grupo
            </span>
          </div>
          <div className="rounded-md bg-primary/10 p-2 text-primary shrink-0">
            <PieChart className="size-4" />
          </div>
        </CardContent>
      </Card>

      {/* 4. Secondary Class Metric (Cotistas / Menor Taxa / P/VP Médio) */}
      <Card className="p-3 sm:p-4">
        <CardContent className="p-0 flex items-center justify-between gap-2">
          <div className="flex flex-col gap-0.5 min-w-0">
            <span className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider truncate">
              {stats.secondaryMetric.label}
            </span>
            <span className="text-lg sm:text-xl font-bold tracking-tight text-foreground font-mono truncate">
              {stats.secondaryMetric.value}
            </span>
            <span className="text-[10px] text-muted-foreground truncate">
              {stats.totalShareholders > 0
                ? `${stats.totalShareholders.toLocaleString('pt-BR')} investidores`
                : 'Métrica de referência'}
            </span>
          </div>
          <div className="rounded-md bg-muted p-2 text-muted-foreground shrink-0">
            <Users className="size-4" />
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
