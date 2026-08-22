import { cn } from '@/lib/utils';

const WIDTH = 100;
const HEIGHT = 32;
const PAD = 2;

function buildPath(values: number[]): { line: string; area: string } | null {
  if (values.length < 2) return null;

  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const stepX = (WIDTH - PAD * 2) / (values.length - 1);

  const points = values.map((value, index) => {
    const x = PAD + index * stepX;
    const y = HEIGHT - PAD - ((value - min) / span) * (HEIGHT - PAD * 2);
    return [Number(x.toFixed(2)), Number(y.toFixed(2))] as const;
  });

  const line = points.map(([x, y], index) => `${index === 0 ? 'M' : 'L'}${x} ${y}`).join(' ');
  // First point sits at x=PAD and last at x=WIDTH-PAD by construction.
  const area = `${line} L${WIDTH - PAD} ${HEIGHT - PAD} L${PAD} ${HEIGHT - PAD} Z`;

  return { line, area };
}

/**
 * Inline sparkline for closing-price windows. Pure server-renderable SVG —
 * no chart library. Trend colour: positive when last close >= first.
 */
export function Sparkline({
  values,
  className,
  strokeWidth = 1.5,
}: {
  values: number[];
  className?: string;
  strokeWidth?: number;
}) {
  const path = buildPath(values);
  const firstValue = values[0];
  const lastValue = values[values.length - 1];
  const positive = firstValue !== undefined && lastValue !== undefined && lastValue >= firstValue;

  if (!path) {
    return (
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        preserveAspectRatio="none"
        aria-hidden="true"
        className={cn('text-muted-foreground/40', className)}
      >
        <line
          x1={PAD}
          x2={WIDTH - PAD}
          y1={HEIGHT / 2}
          y2={HEIGHT / 2}
          stroke="currentColor"
          strokeWidth={strokeWidth}
          strokeDasharray="3 3"
        />
      </svg>
    );
  }

  return (
    <svg
      viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
      preserveAspectRatio="none"
      aria-hidden="true"
      className={cn(positive ? 'text-positive' : 'text-negative', className)}
    >
      <path d={path.area} fill="currentColor" opacity={0.12} />
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
