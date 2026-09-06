'use client';

import * as React from 'react';
import { usePortfolioPerformance } from '@/hooks/use-portfolio-performance';
import {
  Area,
  CartesianGrid,
  ComposedChart,
  Line,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { MaskedValue, BlurChart } from '@/components/privacy/masked-value';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { chartColor } from '@/lib/chart-colors';

const brl = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  maximumFractionDigits: 0,
});

const PERIODS = [
  { value: '1M', label: '1M' },
  { value: '3M', label: '3M' },
  { value: '6M', label: '6M' },
  { value: '1A', label: '1A' },
  { value: 'TUDO', label: 'Tudo' },
] as const;

const BENCHMARK_LABELS: Record<string, string> = {
  CDI: 'CDI',
  IPCA: 'IPCA',
  IBOV: 'Ibovespa',
};

const seriesColor = (index: number) =>
  chartColor(`--chart-${index + 1}`, ['#16a34a', '#2563eb', '#d97706'][index] ?? '#71717a');

/**
 * Painel de rentabilidade da carteira (M-P2): área do patrimônio com benchmarks
 * sobrepostos (base 100 → R$ inicial) e cards de métricas. Período via nuqs.
 */
export function PerformancePanel({ portfolioId }: { portfolioId: string }) {
  const { query, periodParams, setPeriodParams, invalidRange } =
    usePortfolioPerformance(portfolioId);

  const data = React.useMemo(() => {
    const perf = query.data;
    if (!perf) return null;
    const initialValue = perf.series[0]?.value ?? 0;
    const rows = perf.series.map((p) => ({
      date: p.date,
      patrimonio: Number(p.value),
      fluxo: p.externalFlow,
    })) as Array<Record<string, number | string>>;
    for (const bench of perf.benchmarks) {
      for (let i = 0; i < rows.length; i++) {
        const row = rows[i];
        if (!row) continue;
        const v = bench.normalizedValues[i];
        if (!v) continue; // antes do primeiro pregão do benchmark
        row[bench.code] = Number((v / 100) * initialValue);
      }
    }
    return {
      perf,
      rows: rows as Array<
        { date: string; patrimonio: number; fluxo: number } & Record<string, unknown>
      >,
    };
  }, [query.data]);

  return (
    <Card>
      <CardHeader className="gap-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <CardTitle className="text-base">Evolução do patrimônio</CardTitle>
          <div className="flex flex-wrap gap-1" role="group" aria-label="Período">
            {PERIODS.map((p) => (
              <button
                key={p.value}
                type="button"
                aria-pressed={periodParams.p === p.value && !periodParams.de && !periodParams.ate}
                onClick={() => setPeriodParams({ p: p.value, de: null, ate: null })}
                className={`rounded-md px-2 py-1 text-xs transition-colors ${
                  periodParams.p === p.value && !periodParams.de && !periodParams.ate
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:bg-muted'
                }`}
              >
                {p.label}
              </button>
            ))}
          </div>
        </div>
        {/* Datas livres */}
        <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
          <label className="flex items-center gap-1">
            De
            <input
              type="date"
              value={periodParams.de ? periodParams.de.toISOString().slice(0, 10) : ''}
              onChange={(e) =>
                setPeriodParams({ de: e.target.value ? new Date(e.target.value) : null })
              }
              className="rounded-md border bg-background px-1.5 py-0.5"
            />
          </label>
          <label className="flex items-center gap-1">
            Até
            <input
              type="date"
              value={periodParams.ate ? periodParams.ate.toISOString().slice(0, 10) : ''}
              onChange={(e) =>
                setPeriodParams({ ate: e.target.value ? new Date(e.target.value) : null })
              }
              className="rounded-md border bg-background px-1.5 py-0.5"
            />
          </label>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {invalidRange ? (
          <p role="alert">A data inicial deve ser anterior ou igual à data final.</p>
        ) : query.isLoading ? (
          <Skeleton className="h-64 w-full" />
        ) : query.isError ? (
          <p className="py-8 text-center text-sm text-muted-foreground">
            Não foi possível carregar o período.{' '}
            <button className="underline" onClick={() => void query.refetch()}>
              Tentar novamente
            </button>
          </p>
        ) : data ? (
          <>
            {/* Métricas */}
            <dl className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
              <Metric
                label="Retorno"
                value={`${fmtPct(data.perf.totalReturnPercent)}`}
                accent
                masked
              />
              <Metric
                label="Retorno sem efeito dos aportes"
                value={fmtPct(data.perf.twrPercentPeriod)}
                masked
              />
              <Metric
                label="Seu retorno anualizado"
                masked
                value={
                  data.perf.mwrPercentAnnualized !== null
                    ? fmtPct(data.perf.mwrPercentAnnualized)
                    : '—'
                }
              />
              <Metric
                label="Vol a.a."
                value={fmtPct(data.perf.volatilityPercentAnnualized)}
                masked
              />
              <Metric label="Sharpe" value={data.perf.sharpeRatio.toFixed(2)} masked />
              <Metric
                label="Queda máx."
                value={`-${fmtPct(data.perf.maxDrawdownPercent)}`}
                masked
              />
            </dl>

            <BlurChart>
              <div className="h-64 w-full sm:h-72">
                <ResponsiveContainer width="100%" height="100%">
                  <ComposedChart data={data.rows} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" vertical={false} />
                    <XAxis
                      dataKey="date"
                      tick={{ fontSize: 11 }}
                      tickFormatter={(v: string) => v.slice(5).replace('-', '/')}
                      minTickGap={40}
                    />
                    <YAxis
                      tick={{ fontSize: 11 }}
                      domain={['auto', 'auto']}
                      tickFormatter={(v: number) => `${Math.round(v / 1000)}k`}
                      width={38}
                    />
                    <Tooltip
                      formatter={(value: unknown, name: unknown): [string, string] => [
                        brl.format(Number(value ?? 0)),
                        name === 'patrimonio'
                          ? 'Carteira'
                          : (BENCHMARK_LABELS[String(name)] ?? String(name)),
                      ]}
                      labelFormatter={(l: unknown) =>
                        typeof l === 'string'
                          ? new Date(`${l}T12:00:00`).toLocaleDateString('pt-BR')
                          : String(l ?? '')
                      }
                    />
                    <Area
                      type="monotone"
                      dataKey="patrimonio"
                      stroke={seriesColor(0)}
                      fill={seriesColor(0)}
                      fillOpacity={0.15}
                      strokeWidth={2}
                      dot={false}
                    />
                    {data.perf.benchmarks.map((b, i) => (
                      <Line
                        key={b.code}
                        type="monotone"
                        dataKey={b.code}
                        stroke={seriesColor(i + 1)}
                        strokeWidth={1.5}
                        strokeDasharray="4 4"
                        dot={false}
                        connectNulls
                      />
                    ))}
                  </ComposedChart>
                </ResponsiveContainer>
              </div>
            </BlurChart>

            <p className="text-[11px] text-muted-foreground">
              O patrimônio inclui aportes e resgates. As linhas tracejadas mostram o valor inicial
              aplicado nos índices, sem novos aportes: não representam uma comparação direta de
              retorno. O retorno sem efeito dos aportes (TWR) mede a estratégia.
            </p>
          </>
        ) : null}
      </CardContent>
    </Card>
  );
}

function Metric({
  label,
  value,
  accent,
  masked,
}: {
  label: string;
  value: string;
  accent?: boolean;
  masked?: boolean;
}) {
  const negative = value.trim().startsWith('-');
  const content = masked ? <MaskedValue>{value}</MaskedValue> : value;
  return (
    <div className="rounded-lg border p-2">
      <dt className="text-[11px] text-muted-foreground">{label}</dt>
      <dd
        className={`mt-0.5 text-sm font-semibold tabular-nums ${
          accent ? (negative ? 'text-red-600' : 'text-emerald-600') : ''
        }`}
      >
        {content}
      </dd>
    </div>
  );
}

function fmtPct(v: number): string {
  return `${v >= 0 ? '' : '-'}${Math.abs(v).toLocaleString('pt-BR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}%`;
}
