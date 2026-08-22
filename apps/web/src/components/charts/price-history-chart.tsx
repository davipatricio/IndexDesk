'use client';

import { useId, useMemo, useState } from 'react';
import {
  Area,
  Bar,
  CartesianGrid,
  ComposedChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { Button } from '@/components/ui/button';
import type { QuoteItem } from '@/lib/api-client';
import { cn, formatCurrencyBRL } from '@/lib/utils';

const TIMEFRAMES = [
  { value: '1M', label: '1M', months: 1 },
  { value: '3M', label: '3M', months: 3 },
  { value: '6M', label: '6M', months: 6 },
  { value: '1A', label: '1A', months: 12 },
  { value: 'ALL', label: 'TUDO', months: null },
] as const;

type Timeframe = (typeof TIMEFRAMES)[number]['value'];

interface PriceHistoryChartProps {
  quotes: QuoteItem[];
  ticker?: string;
  className?: string;
}

interface ChartQuote extends QuoteItem {
  timestamp: number;
}

const dateFormatter = new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
});

const axisDateFormatter = new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit',
  month: '2-digit',
});

const volumeFormatter = new Intl.NumberFormat('pt-BR', {
  notation: 'compact',
  maximumFractionDigits: 1,
});

function parseDate(value: string): number {
  const timestamp = Date.parse(value);
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

function formatDate(value: string): string {
  const timestamp = parseDate(value);
  return timestamp === 0 ? value : dateFormatter.format(new Date(timestamp));
}

function formatAxisDate(value: string): string {
  const timestamp = parseDate(value);
  return timestamp === 0 ? value : axisDateFormatter.format(new Date(timestamp));
}

function formatVolume(value: number): string {
  return `${volumeFormatter.format(value)} cotas`;
}

function filterQuotes(quotes: QuoteItem[], timeframe: Timeframe): ChartQuote[] {
  const sorted = quotes
    .map((quote) => ({ ...quote, timestamp: parseDate(quote.date) }))
    .sort((first, second) => first.timestamp - second.timestamp);

  const selected = TIMEFRAMES.find((item) => item.value === timeframe);
  if (!selected || selected.months === null || sorted.length === 0) return sorted;

  const latestQuote = sorted.at(-1);
  if (!latestQuote) return sorted;
  const latestTimestamp = latestQuote.timestamp;
  if (latestTimestamp === 0) return sorted;

  const start = new Date(latestTimestamp);
  start.setMonth(start.getMonth() - selected.months);
  const startTimestamp = start.getTime();
  const filtered = sorted.filter((quote) => quote.timestamp >= startTimestamp);

  // Keep the first available quote when a sparse series has no point inside the
  // requested window. This prevents an empty chart after a short offline sync.
  return filtered.length > 0 ? filtered : sorted.slice(-1);
}

function PriceTooltip({
  active,
  label,
  payload,
}: {
  active?: boolean;
  label?: string | number;
  payload?: Array<{ dataKey?: string | number; value?: number | string }>;
}) {
  if (!active || !payload?.length) return null;

  const close = payload.find((entry) => entry.dataKey === 'close');
  const volume = payload.find((entry) => entry.dataKey === 'volume');

  return (
    <div className="rounded-lg border border-border/80 bg-popover px-3 py-2 text-xs text-popover-foreground shadow-lg">
      <p className="mb-1 font-medium text-muted-foreground">
        {typeof label === 'string' ? formatDate(label) : label}
      </p>
      {close ? (
        <p className="font-mono font-semibold">
          Fechamento: {formatCurrencyBRL(Number(close.value))}
        </p>
      ) : null}
      {volume ? (
        <p className="font-mono text-muted-foreground">
          Volume: {formatVolume(Number(volume.value))}
        </p>
      ) : null}
    </div>
  );
}

export function PriceHistoryChart({ quotes, ticker, className }: PriceHistoryChartProps) {
  const [timeframe, setTimeframe] = useState<Timeframe>('6M');
  const gradientPrefix = useId().replace(/:/g, '');
  const chartData = useMemo(() => filterQuotes(quotes, timeframe), [quotes, timeframe]);
  const firstClose = chartData[0]?.close ?? 0;
  const lastClose = chartData[chartData.length - 1]?.close ?? 0;
  const trend = lastClose - firstClose;
  const trendName = trend >= 0 ? 'positive' : 'negative';
  const title = ticker ? `Histórico de preços — ${ticker}` : 'Histórico de preços';

  return (
    <section className={cn('flex flex-col gap-4 rounded-xl border bg-card p-4', className)}>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="font-heading text-base font-semibold">{title}</h2>
          <p className="text-xs text-muted-foreground">
            Fechamento ajustado e volume negociado na B3
          </p>
        </div>
        <div className="flex flex-wrap gap-1" aria-label="Período do gráfico">
          {TIMEFRAMES.map((item) => (
            <Button
              key={item.value}
              type="button"
              variant={timeframe === item.value ? 'secondary' : 'ghost'}
              size="sm"
              aria-pressed={timeframe === item.value}
              onClick={() => setTimeframe(item.value)}
              className="min-w-10 px-2 font-mono text-xs"
            >
              {item.label}
            </Button>
          ))}
        </div>
      </div>

      <div
        className="h-80 w-full"
        role="img"
        aria-label={`${title}, ${chartData.length} observações, tendência ${trendName}`}
      >
        {chartData.length > 0 ? (
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={chartData} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
              <defs>
                <linearGradient id={`${gradientPrefix}-area`} x1="0" y1="0" x2="0" y2="1">
                  <stop
                    offset="0%"
                    stopColor={trend >= 0 ? 'var(--positive)' : 'var(--negative)'}
                    stopOpacity={0.32}
                  />
                  <stop
                    offset="100%"
                    stopColor={trend >= 0 ? 'var(--positive)' : 'var(--negative)'}
                    stopOpacity={0.02}
                  />
                </linearGradient>
              </defs>
              <CartesianGrid vertical={false} stroke="var(--border)" strokeDasharray="3 3" />
              <XAxis
                dataKey="date"
                axisLine={false}
                tickLine={false}
                minTickGap={28}
                tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                tickFormatter={formatAxisDate}
              />
              <YAxis
                yAxisId="price"
                domain={['auto', 'auto']}
                axisLine={false}
                tickLine={false}
                tick={{ fill: 'var(--muted-foreground)', fontSize: 11 }}
                tickFormatter={(value: number) => formatCurrencyBRL(value)}
                width={66}
              />
              <YAxis yAxisId="volume" domain={[0, 'auto']} hide />
              <Tooltip
                content={<PriceTooltip />}
                cursor={{ stroke: 'var(--muted-foreground)', strokeDasharray: '4 4' }}
              />
              <Bar
                yAxisId="volume"
                dataKey="volume"
                name="Volume"
                fill="var(--chart-2)"
                fillOpacity={0.2}
                barSize={8}
                radius={[2, 2, 0, 0]}
              />
              <Area
                yAxisId="price"
                type="monotone"
                dataKey="close"
                name="Fechamento"
                stroke={trend >= 0 ? 'var(--positive)' : 'var(--negative)'}
                strokeWidth={2}
                fill={`url(#${gradientPrefix}-area)`}
                activeDot={{ r: 4, strokeWidth: 2, stroke: 'var(--card)' }}
                dot={false}
                connectNulls
              />
            </ComposedChart>
          </ResponsiveContainer>
        ) : (
          <div className="flex h-full items-center justify-center rounded-lg border border-dashed text-sm text-muted-foreground">
            Histórico de cotações indisponível.
          </div>
        )}
      </div>

      {chartData.length > 0 ? (
        <details className="text-xs text-muted-foreground">
          <summary className="cursor-pointer select-none font-medium hover:text-foreground">
            Ver dados do período ({chartData.length} pontos)
          </summary>
          <div className="mt-2 max-h-44 overflow-auto rounded-lg border">
            <table className="w-full text-left">
              <caption className="sr-only">Cotações históricas do período selecionado</caption>
              <thead className="sticky top-0 bg-muted text-[11px] uppercase tracking-wide">
                <tr>
                  <th className="px-2 py-1.5 font-medium">Data</th>
                  <th className="px-2 py-1.5 text-right font-medium">Fechamento</th>
                  <th className="px-2 py-1.5 text-right font-medium">Volume</th>
                </tr>
              </thead>
              <tbody>
                {chartData.map((quote) => (
                  <tr key={`${quote.date}-${quote.close}`} className="border-t">
                    <td className="px-2 py-1.5">{formatDate(quote.date)}</td>
                    <td className="px-2 py-1.5 text-right font-mono text-foreground">
                      {formatCurrencyBRL(quote.close)}
                    </td>
                    <td className="px-2 py-1.5 text-right font-mono">
                      {formatVolume(quote.volume)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </details>
      ) : null}
    </section>
  );
}

export type { PriceHistoryChartProps };
