'use client';

import { useEffect, useMemo, useRef } from 'react';
import { useTheme } from 'next-themes';
import {
  AreaSeries,
  createChart,
  HistogramSeries,
  type IChartApi,
  type ISeriesApi,
  type Time,
} from 'lightweight-charts';
import { parseAsString, useQueryState } from 'nuqs';

import { Button } from '@/components/ui/button';
import { chartColor } from '@/lib/chart-colors';
import { filterQuotesByPeriod, HERO_TIMEFRAMES, resolveHeroTimeframe } from '@/lib/chart-period';

export interface HeroQuotePoint {
  date: string;
  close: number;
  volume: number;
}

interface PriceHeroChartProps {
  ticker: string;
  quotes: HeroQuotePoint[];
}

function readChartColors(): {
  text: string;
  border: string;
  positive: string;
  negative: string;
  muted: string;
} {
  // Canvas charts need hex — Tailwind v4 tokens are oklch()/lab().
  return {
    text: chartColor('--foreground', '#111113'),
    border: chartColor('--border', '#e4e4e7'),
    positive: chartColor('--positive', '#16a34a'),
    negative: chartColor('--negative', '#dc2626'),
    muted: chartColor('--muted-foreground', '#71717a'),
  };
}

export function PriceHeroChart({ ticker, quotes }: PriceHeroChartProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const areaSeriesRef = useRef<ISeriesApi<'Area'> | null>(null);
  const volumeSeriesRef = useRef<ISeriesApi<'Histogram'> | null>(null);

  const [periodParam, setPeriodParam] = useQueryState('p', parseAsString.withDefault('6M'));
  const period = resolveHeroTimeframe(periodParam);
  const { resolvedTheme } = useTheme();

  const chartData = useMemo(() => filterQuotesByPeriod(quotes, period.months), [quotes, period]);

  // lightweight-charts touches the DOM directly — build it after mount and
  // rebuild whenever the resolved theme changes so colors follow the tokens.
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const colors = readChartColors();
    const lineColor =
      (chartData.at(-1)?.close ?? 0) >= (chartData[0]?.close ?? 0) ? colors.positive : colors.muted;
    const chart = createChart(container, {
      autoSize: true,
      layout: {
        background: { color: 'transparent' },
        textColor: colors.muted,
        fontFamily: 'var(--font-sans), sans-serif',
        attributionLogo: false,
      },
      grid: {
        vertLines: { visible: false },
        horzLines: { color: colors.border, style: 3 },
      },
      rightPriceScale: { borderVisible: false },
      timeScale: { borderVisible: false, rightOffset: 4 },
      // Mouse wheel scrolls/zooms the time axis, but pressed-mouse dragging is
      // kept off so users can't fling the series out of view (touch included).
      handleScroll: {
        mouseWheel: true,
        pressedMouseMove: false,
        horzTouchDrag: false,
        vertTouchDrag: false,
      },
      handleScale: {
        mouseWheel: true,
        pinch: true,
        axisPressedMouseMove: false,
        axisDoubleClickReset: true,
      },
      crosshair: {
        vertLine: { color: colors.muted, style: 3, labelBackgroundColor: colors.text },
        horzLine: { color: colors.muted, style: 3, labelBackgroundColor: colors.text },
      },
    });
    chartRef.current = chart;

    const area = chart.addSeries(AreaSeries, {
      lineColor,
      topColor: `${lineColor}52`,
      bottomColor: `${lineColor}08`,
      lineWidth: 2,
      priceFormat: { type: 'price', precision: 2, minMove: 0.01 },
      pointMarkersVisible: false,
    });
    area.priceScale().applyOptions({ scaleMargins: { top: 0.1, bottom: 0.24 } });
    areaSeriesRef.current = area;

    const volume = chart.addSeries(HistogramSeries, {
      priceFormat: { type: 'volume' },
      priceScaleId: '',
      color: colors.border,
      lastValueVisible: false,
      priceLineVisible: false,
    });
    volume.priceScale().applyOptions({ scaleMargins: { top: 0.82, bottom: 0 } });
    volumeSeriesRef.current = volume;

    area.setData(chartData.map((quote) => ({ time: quote.date as Time, value: quote.close })));
    volume.setData(
      chartData.map((quote, index) => ({
        time: quote.date as Time,
        value: quote.volume,
        color:
          index === 0 || quote.close >= (chartData[index - 1]?.close ?? quote.close)
            ? `${colors.positive}33`
            : `${colors.negative}33`,
      })),
    );
    chart.timeScale().fitContent();

    return () => {
      chart.remove();
      chartRef.current = null;
      areaSeriesRef.current = null;
      volumeSeriesRef.current = null;
    };
    // Recreate on theme change and on data changes to keep token colors fresh.
  }, [resolvedTheme, chartData]);

  if (quotes.length === 0) {
    return (
      <div className="flex h-80 items-center justify-center rounded-lg border border-dashed text-sm text-muted-foreground">
        Histórico de cotações indisponível.
      </div>
    );
  }

  return (
    <section aria-label={`Histórico de preços de ${ticker}`} className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-xs text-muted-foreground">
          Fechamento e volume negociado na B3 · {chartData.length} pregões
        </p>
        <div className="flex flex-wrap gap-1" role="group" aria-label="Período do gráfico">
          {HERO_TIMEFRAMES.map((timeframe) => (
            <Button
              key={timeframe.value}
              type="button"
              variant={timeframe.value === period.value ? 'secondary' : 'ghost'}
              size="sm"
              aria-pressed={timeframe.value === period.value}
              onClick={() => setPeriodParam(timeframe.value)}
              className="min-w-10 px-2 font-mono text-xs"
            >
              {timeframe.label}
            </Button>
          ))}
        </div>
      </div>

      <div ref={containerRef} className="h-[380px] w-full md:h-[440px]" />

      <p className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="inline-block h-0.5 w-4 rounded bg-primary" />
          Fechamento (linha)
        </span>
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="inline-block h-2 w-2 rounded-sm bg-positive/40" />
          Volume do dia em alta
        </span>
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="inline-block h-2 w-2 rounded-sm bg-negative/40" />
          Volume do dia em baixa
        </span>
        <span>Role com o mouse para navegar; duplo clique no eixo volta ao zoom padrão.</span>
      </p>
    </section>
  );
}
