'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { Button } from '@/components/ui/button';
import { ScrubNumberField } from '@/components/ui/scrub-number-input';
import { fetchMacroRateSeries, fetchQuoteSparks } from '@/lib/api-client';
import {
  buildInvestmentSimulation,
  SIMULATION_PERIODS,
  summarize,
  type BenchmarkSeriesInput,
  type SeriesPoint,
  type SimulationPeriodKey,
} from '@/lib/simulation';
import { cn, formatCurrencyBRL, formatPercent } from '@/lib/utils';

interface InvestmentSimulationProps {
  ticker: string;
  /** Full closing-price history of the asset (server-fetched). */
  closes: SeriesPoint[];
  assetClass: string;
  className?: string;
}

interface SeriesStyle {
  color: string;
  strokeDasharray?: string;
}

const SERIES_STYLES: Record<string, SeriesStyle> = {
  asset: { color: 'var(--primary)' },
  IBOV: { color: 'var(--chart-2)' },
  IFIX: { color: 'var(--chart-3)' },
  CDI: { color: 'var(--muted-foreground)', strokeDasharray: '4 4' },
};

/** Benchmarks shown by default per asset class. */
function defaultBenchmarks(assetClass: string): Record<string, boolean> {
  const isFii = assetClass.toUpperCase() === 'FII';
  return { IBOV: !isFii, IFIX: isFii, CDI: true };
}

const axisDateFormatter = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit' });

function SimulationTooltip({
  active,
  label,
  payload,
}: {
  active?: boolean;
  label?: string | number;
  payload?: Array<{ name?: string; dataKey?: string | number; value?: number }>;
}) {
  if (!active || !payload?.length) return null;

  const date =
    typeof label === 'string' && !Number.isNaN(Date.parse(`${label}T00:00:00Z`))
      ? new Intl.DateTimeFormat('pt-BR', {
          day: '2-digit',
          month: 'short',
          year: 'numeric',
        }).format(new Date(`${label}T00:00:00Z`))
      : label;

  return (
    <div className="rounded-lg border border-border/80 bg-popover px-3 py-2 text-xs text-popover-foreground shadow-lg">
      <p className="mb-1 font-medium text-muted-foreground">{date}</p>
      {payload.map((entry) => (
        <p key={entry.dataKey} className="flex items-center gap-2 font-mono">
          <span
            aria-hidden="true"
            className="inline-block size-1.5 rounded-full"
            style={{ background: SERIES_STYLES[String(entry.dataKey)]?.color ?? 'currentColor' }}
          />
          <span className="text-muted-foreground">{entry.name}</span>
          <span className="font-semibold">{formatCurrencyBRL(Number(entry.value))}</span>
        </p>
      ))}
    </div>
  );
}

export function InvestmentSimulation({
  ticker,
  closes,
  assetClass,
  className,
}: InvestmentSimulationProps) {
  const [amount, setAmount] = useState(1000);
  const [periodKey, setPeriodKey] = useState<SimulationPeriodKey>('1A');
  const [enabled, setEnabled] = useState<Record<string, boolean>>(() =>
    defaultBenchmarks(assetClass),
  );

  const periodMonths = SIMULATION_PERIODS.find((period) => period.key === periodKey)?.months ?? 12;

  const benchmarksQuery = useQuery({
    queryKey: ['simulation-benchmarks'],
    queryFn: async () => {
      const [quotes, macro] = await Promise.all([
        fetchQuoteSparks(['IBOV', 'IFIX'], 7300),
        fetchMacroRateSeries(['CDI'], 7300),
      ]);
      return { quotes, macro };
    },
    staleTime: 15 * 60 * 1000,
    gcTime: 60 * 60 * 1000,
  });

  const benchmarks = useMemo<BenchmarkSeriesInput[]>(() => {
    const quotes = benchmarksQuery.data?.quotes ?? {};
    const macro = benchmarksQuery.data?.macro ?? {};
    return [
      {
        key: 'IBOV',
        label: 'Ibovespa',
        points: (quotes.IBOV ?? []).map((point) => ({ date: point.date, value: point.close })),
      },
      {
        key: 'IFIX',
        label: 'IFIX',
        points: (quotes.IFIX ?? []).map((point) => ({ date: point.date, value: point.close })),
      },
      {
        key: 'CDI',
        label: 'CDI',
        kind: 'rate',
        points: (macro.CDI ?? []).map((point) => ({ date: point.date, value: point.value })),
      },
    ];
  }, [benchmarksQuery.data]);

  const simulation = useMemo(
    () =>
      buildInvestmentSimulation({
        amount,
        periodMonths,
        assetPoints: closes,
        // All requested benchmarks are computed even when hidden so their
        // summary columns stay available for toggling.
        benchmarks,
      }),
    [amount, periodMonths, closes, benchmarks],
  );

  const hasData = simulation.dates.length > 0;
  const assetValues = simulation.series[0]?.values;
  const assetSummary = hasData && assetValues ? summarize(assetValues, amount) : null;

  const inceptionNote =
    simulation.inceptionDate &&
    periodKey !== '6M' &&
    simulation.startDate === simulation.inceptionDate
      ? `Histórico completo desde ${new Intl.DateTimeFormat('pt-BR').format(new Date(`${simulation.inceptionDate}T00:00:00Z`))}`
      : null;

  return (
    <section
      aria-label={`Simular investimento em ${ticker}`}
      className={cn('flex flex-col gap-4 border-t pt-6', className)}
    >
      <div className="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div className="max-w-[65ch]">
          <h2 className="font-heading text-base font-semibold">Simular investimento</h2>
          <p className="mt-0.5 text-xs leading-relaxed text-muted-foreground">
            Se você tivesse investido o valor abaixo neste ativo no início do período escolhido,
            quanto teria hoje — e como teria ficado frente aos indexadores?
          </p>
        </div>

        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
          <label className="flex items-center gap-2">
            <span className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
              Valor
            </span>
            <ScrubNumberField
              min={50}
              step={100}
              smallStep={50}
              largeStep={500}
              format={{ style: 'currency', currency: 'BRL', maximumFractionDigits: 0 }}
              value={amount}
              onValueChange={setAmount}
              className="w-32 font-mono text-sm"
            />
          </label>
          <div className="flex flex-wrap gap-1" role="group" aria-label="Período da simulação">
            {SIMULATION_PERIODS.map((period) => (
              <Button
                key={period.key}
                type="button"
                variant={period.key === periodKey ? 'secondary' : 'ghost'}
                size="sm"
                aria-pressed={period.key === periodKey}
                onClick={() => setPeriodKey(period.key)}
                className="min-w-14 px-2 font-mono text-xs"
              >
                {period.label}
              </Button>
            ))}
          </div>
        </div>
      </div>

      {hasData && assetSummary ? (
        <>
          <div
            className="grid grid-cols-2 divide-x divide-border overflow-hidden rounded-lg border bg-background/40 sm:grid-cols-3 lg:grid-cols-5"
            data-testid="simulation-summary"
          >
            {simulation.series.map((series, index) => {
              const summary = summarize(series.values, amount);
              const positive = summary.returnPercent >= 0;
              const isAsset = index === 0;
              const isOn = enabled[series.key] !== false;
              const column = (
                <>
                  <span className="flex items-center gap-1.5 text-[11px] font-medium text-muted-foreground">
                    <span
                      aria-hidden="true"
                      className="inline-block size-2 rounded-full"
                      style={{ background: SERIES_STYLES[series.key]?.color ?? 'var(--primary)' }}
                    />
                    {isAsset ? ticker : series.label}
                  </span>
                  <span className="font-mono text-sm font-semibold tabular-nums">
                    {formatCurrencyBRL(summary.finalValue)}
                  </span>
                  {isAsset ? (
                    <span
                      className={cn(
                        'font-mono text-xs tabular-nums',
                        positive ? 'text-positive' : 'text-negative',
                      )}
                      title="Variação de preço no período"
                    >
                      {positive ? '+' : ''}
                      {formatPercent(summary.returnPercent)} no período
                    </span>
                  ) : (
                    <span className="font-mono text-xs tabular-nums text-muted-foreground">
                      {positive ? '+' : ''}
                      {formatPercent(summary.returnPercent)}
                    </span>
                  )}
                </>
              );

              return isAsset ? (
                <div key={series.key} className="flex flex-col gap-1 px-3 py-2.5">
                  {column}
                </div>
              ) : (
                <button
                  key={series.key}
                  type="button"
                  aria-pressed={isOn}
                  title={isOn ? `Ocultar ${series.label}` : `Mostrar ${series.label}`}
                  onClick={() => setEnabled((current) => ({ ...current, [series.key]: !isOn }))}
                  className={cn(
                    'flex flex-col gap-1 px-3 py-2.5 text-left transition-opacity hover:bg-muted/40',
                    isOn ? '' : 'opacity-35',
                  )}
                >
                  {column}
                </button>
              );
            })}
          </div>

          <div
            className="h-72 w-full"
            role="img"
            aria-label={`Evolução simulada de R$ ${amount} em ${ticker} e indexadores`}
          >
            <ResponsiveContainer width="100%" height="100%">
              <LineChart
                data={simulation.dates.map((date, index) => ({
                  date,
                  ...Object.fromEntries(
                    simulation.series.map((series) => [series.key, series.values[index]]),
                  ),
                }))}
                margin={{ top: 8, right: 8, bottom: 0, left: 0 }}
              >
                <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
                <XAxis
                  dataKey="date"
                  axisLine={false}
                  tickLine={false}
                  minTickGap={36}
                  tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                  tickFormatter={(value: string) =>
                    axisDateFormatter.format(new Date(`${value}T00:00:00Z`))
                  }
                />
                <YAxis
                  domain={['auto', 'auto']}
                  axisLine={false}
                  tickLine={false}
                  width={64}
                  tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                  tickFormatter={(value: number) => formatCurrencyBRL(value)}
                />
                <Tooltip
                  content={<SimulationTooltip />}
                  cursor={{ stroke: 'var(--muted-foreground)', strokeDasharray: '4 4' }}
                />
                {simulation.series
                  .filter((series) => series.key === 'asset' || enabled[series.key] !== false)
                  .map((series) => (
                    <Line
                      key={series.key}
                      type="monotone"
                      dataKey={series.key}
                      name={series.key === 'asset' ? `${ticker} (variação de preço)` : series.label}
                      stroke={SERIES_STYLES[series.key]?.color ?? 'var(--primary)'}
                      strokeWidth={series.key === 'asset' ? 2.25 : 1.5}
                      strokeDasharray={SERIES_STYLES[series.key]?.strokeDasharray}
                      dot={false}
                      activeDot={{ r: 3, strokeWidth: 2, stroke: 'var(--card)' }}
                      connectNulls
                    />
                  ))}
              </LineChart>
            </ResponsiveContainer>
          </div>

          {simulation.unavailable.length > 0 || inceptionNote ? (
            <ul className="space-y-1 text-xs text-muted-foreground">
              {inceptionNote ? (
                <li>
                  · Janela ajustada ao início do histórico local (
                  {inceptionNote.replace('Histórico completo ', '')}).
                </li>
              ) : null}
              {simulation.unavailable.map((benchmark) => (
                <li key={benchmark.key}>
                  · {benchmark.label}: {benchmark.reason} — a coleta diária começou recentemente e a
                  série cresce a cada pregão.
                </li>
              ))}
            </ul>
          ) : null}
        </>
      ) : benchmarksQuery.isLoading ? (
        <p className="rounded-lg border border-dashed px-4 py-8 text-center text-sm text-muted-foreground">
          Carregando indexadores…
        </p>
      ) : (
        <p className="rounded-lg border border-dashed px-4 py-8 text-center text-sm text-muted-foreground">
          Histórico insuficiente para simular este ativo ainda.
        </p>
      )}

      <p className="text-[11px] leading-relaxed text-muted-foreground">
        Simulação educativa baseada apenas na variação de preço (não inclui proventos, taxas ou
        impostos). Indexadores: Ibovespa e IFIX pela cotação local diária; CDI acumulado pelas taxas
        diárias publicadas pelo Banco Central. A Selic acompanha o CDI de perto e fica fora do
        gráfico para manter a leitura limpa. Dados com defasagem de até um dia útil.
      </p>
    </section>
  );
}
