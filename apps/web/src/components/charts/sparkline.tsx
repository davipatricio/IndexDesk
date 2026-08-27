import { cn } from '@/lib/utils';

export interface SparklineProps {
  /**
   * Série temporal ordenada antigo → recente.
   * Valores aceitam array numérico (ex.: últimos 30 pontos de fechamento ou patrimônio).
   */
  data?: number[];
  /** Alias para data (retrocompatibilidade). */
  values?: number[];
  width?: number;
  height?: number;
  className?: string;
  strokeWidth?: number;
}

function buildPath(
  pointsData: number[],
  width: number,
  height: number,
  pad: number,
): { line: string; area: string } | null {
  if (pointsData.length < 2) return null;

  const min = Math.min(...pointsData);
  const max = Math.max(...pointsData);
  const span = max - min || 1;
  const stepX = (width - pad * 2) / (pointsData.length - 1);

  const points = pointsData.map((value, index) => {
    const x = pad + index * stepX;
    const y = height - pad - ((value - min) / span) * (height - pad * 2);
    return [Number(x.toFixed(2)), Number(y.toFixed(2))] as const;
  });

  const line = points.map(([x, y], index) => `${index === 0 ? 'M' : 'L'}${x} ${y}`).join(' ');
  const area = `${line} L${width - pad} ${height - pad} L${pad} ${height - pad} Z`;

  return { line, area };
}

/**
 * Sparkline SVG puro, zero-dep, leve para renderizar em lote em tabelas.
 * Área preenchida com ~10% de opacidade e cor dinâmica conforme a inclinação
 * (verde para alta, vermelho para baixa, cinza quando flat ou sem dados).
 */
export function Sparkline({
  data,
  values,
  width = 120,
  height = 28,
  className,
  strokeWidth = 1.5,
}: SparklineProps) {
  const series = data ?? values ?? [];
  const pad = 2;
  const path = buildPath(series, width, height, pad);

  const firstValue = series[0];
  const lastValue = series[series.length - 1];
  const isFlat = firstValue !== undefined && lastValue !== undefined && firstValue === lastValue;
  const isUp = firstValue !== undefined && lastValue !== undefined && lastValue > firstValue;

  if (!path || series.length < 2) {
    return (
      <svg
        viewBox={`0 0 ${width} ${height}`}
        width={width}
        height={height}
        preserveAspectRatio="none"
        aria-hidden="true"
        className={cn('text-muted-foreground/30', className)}
      >
        <line
          x1={pad}
          x2={width - pad}
          y1={height / 2}
          y2={height / 2}
          stroke="currentColor"
          strokeWidth={strokeWidth}
          strokeDasharray="3 3"
        />
      </svg>
    );
  }

  const trendColor = isFlat ? 'text-muted-foreground' : isUp ? 'text-positive' : 'text-negative';

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      width={width}
      height={height}
      preserveAspectRatio="none"
      aria-hidden="true"
      className={cn(trendColor, className)}
    >
      <path d={path.area} fill="currentColor" opacity={0.1} />
      <path
        d={path.line}
        fill="none"
        stroke="currentColor"
        strokeWidth={strokeWidth}
        strokeLinecap="round"
        strokeLinejoin="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
