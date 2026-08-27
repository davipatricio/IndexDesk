'use client';

import * as React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createGoal, deleteGoal, fetchGoals, type GoalDto } from '@/lib/api-client';
import { MaskedValue } from '@/components/privacy/masked-value';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { toast } from 'sonner';

const brl = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  maximumFractionDigits: 0,
});

/**
 * Aba Metas (M-P5): multi-metas com progresso contra o patrimônio atual.
 * Projeções detalhadas (run-rate/juros) via endpoint /projection — exibidas na UI seguinte.
 */
export function GoalsPanel({
  portfolioId,
  totalValue,
}: {
  portfolioId: string;
  totalValue: number;
}) {
  const queryClient = useQueryClient();

  const query = useQuery({
    queryKey: ['portfolio', portfolioId, 'goals', Math.round(totalValue)],
    queryFn: () => fetchGoals(portfolioId, Math.round(totalValue)),
    enabled: totalValue >= 0,
  });

  const remove = useMutation({
    mutationFn: (goalId: string) => deleteGoal(portfolioId, goalId),
    onSuccess: () => {
      toast.success('Meta removida.');
      void queryClient.invalidateQueries({ queryKey: ['portfolio', portfolioId, 'goals'] });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  const goals = query.data ?? [];

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Suas metas</CardTitle>
          <CardDescription>Progresso contra o patrimônio atual.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {query.isLoading ? (
            <p className="text-xs text-muted-foreground">Carregando…</p>
          ) : goals.length === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">
              Nenhuma meta ainda. Crie a primeira ao lado.
            </p>
          ) : (
            goals.map((g) => (
              <GoalRow key={g.goal.id} data={g} onRemove={() => remove.mutate(g.goal.id)} />
            ))
          )}
        </CardContent>
      </Card>

      <NewGoalForm portfolioId={portfolioId} />
    </div>
  );
}

function GoalRow({
  data,
  onRemove,
}: {
  data: { goal: GoalDto; currentValue: number; progressPercent: number | null };
  onRemove: () => void;
}) {
  const g = data.goal;
  const label: React.ReactNode =
    g.kind === 'TARGET_AMOUNT' ? (
      <MaskedValue>Chegar a {brl.format(Number(g.targetValue))}</MaskedValue>
    ) : g.kind === 'TARGET_RETURN_PCT' ? (
      `Retornar ${Number(g.targetPct).toLocaleString('pt-BR')}%`
    ) : (
      `Ter uma carteira ativa até ${new Date(`${g.targetDate}T12:00:00`).toLocaleDateString('pt-BR')}`
    );

  const progress = data.progressPercent ?? 0;

  return (
    <div className="space-y-1.5 rounded-lg border p-3">
      <div className="flex items-start justify-between gap-2">
        <p className="text-sm font-medium">{label}</p>
        <Button variant="ghost" size="xs" onClick={onRemove} aria-label="Remover meta">
          ✕
        </Button>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-muted">
        <div
          className={`h-full rounded-full ${progress >= 100 ? 'bg-emerald-500' : 'bg-primary'}`}
          style={{ width: `${Math.min(100, Math.max(0, progress))}%` }}
        />
      </div>
      <div className="flex items-center justify-between text-[11px] text-muted-foreground">
        <span className="tabular-nums">
          <MaskedValue>{brl.format(data.currentValue)}</MaskedValue>
        </span>
        <Badge variant={progress >= 100 ? 'default' : 'secondary'}>{progress.toFixed(0)}%</Badge>
      </div>
    </div>
  );
}

function NewGoalForm({ portfolioId }: { portfolioId: string }) {
  const queryClient = useQueryClient();
  const [kind, setKind] = React.useState<GoalDto['kind']>('TARGET_AMOUNT');
  const [targetValue, setTargetValue] = React.useState('');
  const [targetPct, setTargetPct] = React.useState('');
  const [targetDate, setTargetDate] = React.useState('');
  const [monthly, setMonthly] = React.useState('');
  const [rate, setRate] = React.useState('');

  const mutation = useMutation({
    mutationFn: () =>
      createGoal(portfolioId, {
        kind,
        targetValue: Number(targetValue.replace(',', '.')) || undefined,
        targetPct: Number(targetPct.replace(',', '.')) || undefined,
        targetDate: targetDate || undefined,
        monthlyContribution: Number(monthly.replace(',', '.')) || undefined,
        assumedAnnualRate: Number(rate.replace(',', '.')) || undefined,
      }),
    onSuccess: () => {
      toast.success('Meta criada!');
      void queryClient.invalidateQueries({ queryKey: ['portfolio', portfolioId, 'goals'] });
    },
    onError: (e: Error) => toast.error(e.message),
  });

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Nova meta</CardTitle>
        <CardDescription>Valor, percentual ou prazo — quantas quiser.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="grid grid-cols-3 gap-2">
          {(
            [
              { value: 'TARGET_AMOUNT', label: 'Valor R$' },
              { value: 'TARGET_RETURN_PCT', label: '% retorno' },
              { value: 'TARGET_DATE', label: 'Prazo' },
            ] as const
          ).map((k) => (
            <button
              key={k.value}
              type="button"
              onClick={() => setKind(k.value)}
              className={`rounded-lg border p-2 text-xs transition-colors ${
                kind === k.value ? 'border-primary bg-primary/5 font-medium' : 'hover:bg-muted/50'
              }`}
            >
              {k.label}
            </button>
          ))}
        </div>

        {kind === 'TARGET_AMOUNT' ? (
          <Input
            inputMode="decimal"
            placeholder="Valor alvo (ex.: 100000)"
            value={targetValue}
            onChange={(e) => setTargetValue(e.target.value)}
          />
        ) : null}
        {kind === 'TARGET_RETURN_PCT' ? (
          <Input
            inputMode="decimal"
            placeholder="% de retorno alvo"
            value={targetPct}
            onChange={(e) => setTargetPct(e.target.value)}
          />
        ) : null}
        {kind === 'TARGET_DATE' ? (
          <Input type="date" value={targetDate} onChange={(e) => setTargetDate(e.target.value)} />
        ) : null}

        <details className="text-xs text-muted-foreground">
          <summary className="cursor-pointer">Aportes e taxa (para projeção)</summary>
          <div className="mt-2 grid grid-cols-2 gap-2">
            <Input
              inputMode="decimal"
              placeholder="Aporte mensal"
              value={monthly}
              onChange={(e) => setMonthly(e.target.value)}
            />
            <Input
              inputMode="decimal"
              placeholder="Taxa a.a. %"
              value={rate}
              onChange={(e) => setRate(e.target.value)}
            />
          </div>
        </details>

        <Button
          className="w-full"
          disabled={
            mutation.isPending ||
            (kind === 'TARGET_AMOUNT' && !targetValue) ||
            (kind === 'TARGET_RETURN_PCT' && !targetPct) ||
            (kind === 'TARGET_DATE' && !targetDate)
          }
          onClick={() => mutation.mutate()}
        >
          {mutation.isPending ? 'Criando…' : 'Criar meta'}
        </Button>
      </CardContent>
    </Card>
  );
}
