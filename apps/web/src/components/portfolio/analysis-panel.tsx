'use client';

import * as React from 'react';
import { usePortfolioPerformance } from '@/hooks/use-portfolio-performance';
import { MaskedValue } from '@/components/privacy/masked-value';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';

const pct = (v: number) =>
  `${v.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;

interface DailyMove {
  date: string;
  changePercent: number;
}

/**
 * Aba Análise (M-P2): métricas de risco da carteira e dias extremos,
 * derivados da mesma série diária do painel de rentabilidade.
 */
export function AnalysisPanel({ portfolioId }: { portfolioId: string }) {
  const { query, invalidRange } = usePortfolioPerformance(portfolioId);

  const moves = React.useMemo<DailyMove[]>(() => {
    const series = query.data?.series;
    if (!series || series.length < 2) return [];
    const out: DailyMove[] = [];
    for (let i = 1; i < series.length; i++) {
      const prev = series[i - 1]!.value;
      const curr = series[i]!.value;
      if (prev > 0 && series[i]!.externalFlow === 0) {
        out.push({ date: series[i]!.date, changePercent: (curr / prev - 1) * 100 });
      }
    }
    return out;
  }, [query.data]);

  if (invalidRange)
    return (
      <p role="alert">
        Confira as datas na aba Rentabilidade. A data inicial deve ser anterior à final.
      </p>
    );
  if (query.isLoading) return <Skeleton className="h-48 w-full" />;
  if (query.isError)
    return (
      <Card>
        <CardContent className="py-8 text-center text-sm text-muted-foreground">
          Não foi possível carregar a análise.{' '}
          <button className="underline" onClick={() => void query.refetch()}>
            Tentar novamente
          </button>
        </CardContent>
      </Card>
    );

  const d = query.data!;
  const best = [...moves].sort((a, b) => b.changePercent - a.changePercent).slice(0, 3);
  const worst = [...moves].sort((a, b) => a.changePercent - b.changePercent).slice(0, 3);

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <RiskCard
          label="Volatilidade anual"
          value={pct(d.volatilityPercentAnnualized)}
          hint="Quanto o patrimônio oscila em um ano típico."
        />
        <RiskCard
          label="Sharpe (excesso CDI)"
          value={d.sharpeRatio.toFixed(2)}
          hint="Retorno acima do CDI por unidade de risco."
        />
        <RiskCard
          label="Queda máxima"
          value={`-${pct(d.maxDrawdownPercent)}`}
          hint="Pior queda do pico ao vale na janela."
        />
        <RiskCard
          label="TWR período"
          value={pct(d.twrPercentPeriod)}
          hint="Rentabilidade da estratégia, ignorando aportes."
        />
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Maiores altas</CardTitle>
            <CardDescription>Dias sem aportes ou resgates</CardDescription>
          </CardHeader>
          <CardContent>
            <MoveList moves={best} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Maiores quedas</CardTitle>
            <CardDescription>Dias sem aportes ou resgates</CardDescription>
          </CardHeader>
          <CardContent>
            <MoveList moves={worst} />
          </CardContent>
        </Card>
      </div>

      <p className="text-[11px] text-muted-foreground">
        Métricas educacionais calculadas com preços de fechamento. Não constituem recomendação de
        investimento.
      </p>
    </div>
  );
}

function RiskCard({ label, value, hint }: { label: string; value: string; hint: string }) {
  return (
    <Card>
      <CardHeader className="pb-1">
        <CardDescription>{label}</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-lg font-semibold tabular-nums">
          <MaskedValue>{value}</MaskedValue>
        </p>
        <p className="mt-1 text-[11px] leading-snug text-muted-foreground">{hint}</p>
      </CardContent>
    </Card>
  );
}

function MoveList({ moves }: { moves: DailyMove[] }) {
  if (moves.length === 0)
    return <p className="text-xs text-muted-foreground">Sem variações registradas.</p>;
  return (
    <ul className="space-y-1.5">
      {moves.map((m) => (
        <li key={m.date} className="flex items-center justify-between text-xs">
          <span className="text-muted-foreground">
            {new Date(`${m.date}T12:00:00`).toLocaleDateString('pt-BR')}
          </span>
          <span
            className={`font-medium tabular-nums ${
              m.changePercent >= 0 ? 'text-emerald-600' : 'text-red-600'
            }`}
          >
            <MaskedValue>
              {m.changePercent >= 0 ? '+' : ''}
              {pct(m.changePercent)}
            </MaskedValue>
          </span>
        </li>
      ))}
    </ul>
  );
}
