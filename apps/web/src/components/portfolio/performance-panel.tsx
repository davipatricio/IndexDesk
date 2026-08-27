'use client';

import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useQueryStates, parseAsIsoDate, parseAsString } from 'nuqs';
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
import { fetchPortfolioPerformance } from '@/lib/api-client';
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

type Period = (typeof PERIODS)[number]['value'];

const BENCHMARK_LABELS: Record<string, string> = {
  CDI: 'CDI',
  IPCA: 'IPCA',
  IBOV: 'Ibovespa',
};

function periodToDate(period: Period): string | undefined {
  if (period === 'TUDO') return undefined;
  const days = { '1M': 30, '3M': 91, '6M': 182, '1A': 365 }[period];
  const d = new Date();
  d.setDate(d.getDate() - days);
  return d.toISOString().slice(0, 10);
}

const seriesColor = (index: number) =>
  chartColor(`--chart-${index + 1}`, ['#16a34a', '#2563eb', '#d97706'][index] ?? '#71717a');

/**
 * Painel de rentabilidade da carteira (M-P2): área do patrimônio com benchmarks
 * sobrepostos (base 100 → R$ inicial) e cards de métricas. Período via nuqs.
 */
export function PerformancePanel({ portfolioId }: { portfolioId: string }) {
  const [periodParams, setPeriodParams] = useQueryStates({
    p: parseAsString.withDefault('TUDO'),
    de: parseAsIsoDate,
    ate: parseAsIsoDate,
  });

  const from =
    periodParams.de?.toISOString().slice(0, 10) ??
    periodToDate((periodParams.p || 'TUDO') as Period);

  const query = useQuery({
    queryKey: ['portfolio', portfolioId, 'performance', from, periodParams.ate],
    queryFn: () =>
      fetchPortfolioPerformance(portfolioId, {
        from,
        to: periodParams.ate?.toISOString().slice(0, 10),
        benchmarks: 'CDI,IBOV',
      }),
    staleTime: 5 * 60 * 1000,
  });

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
          <CardTitle className="text-base">Rentabilidade</CardTitle>
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
        {query.isLoading ? (
          <Skeleton className="h-64 w-full" />
        ) : query.isError ? (
          <p className="py-8 text-center text-sm text-muted-foreground">
            Sem dados suficientes para o período selecionado.
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
              <Metric label="TWR período" value={fmtPct(data.perf.twrPercentPeriod)} masked />
              <Metric
                label="MWR a.a."
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
              Linhas tracejadas: benchmarks normalizados ao valor inicial da carteira. Métricas
              calculadas com preços locais (fechamento); TWR neutraliza aportes e resgates.
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
