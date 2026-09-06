'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createGoal, deleteGoal, fetchGoals, type GoalDto } from '@/lib/api-client';
import { parsePortfolioNumber, validPortfolioDate } from '@/lib/portfolio-input';
import { MaskedValue } from '@/components/privacy/masked-value';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { DeleteConfirmation } from '@/components/portfolio/delete-confirmation';
import { toast } from 'sonner';

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

export function GoalsPanel({
  portfolioId,
  totalValue,
}: {
  portfolioId: string;
  totalValue: number;
}) {
  const queryClient = useQueryClient();
  const [removing, setRemoving] = useState<string | null>(null);
  const [kind, setKind] = useState<GoalDto['kind']>('TARGET_AMOUNT');
  const [target, setTarget] = useState('');
  const [monthly, setMonthly] = useState('');
  const [rate, setRate] = useState('');
  const query = useQuery({
    queryKey: ['portfolio', portfolioId, 'goals', totalValue],
    queryFn: () => fetchGoals(portfolioId, totalValue),
  });
  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ['portfolio', portfolioId, 'goals'] });
  const valid =
    (kind === 'TARGET_DATE' ? validPortfolioDate(target) : parsePortfolioNumber(target) > 0) &&
    (!monthly || parsePortfolioNumber(monthly) >= 0) &&
    (!rate || parsePortfolioNumber(rate) >= 0);
  const mutation = useMutation({
    mutationFn: () => {
      if (!valid) throw new Error('invalid input');
      return createGoal(portfolioId, {
        kind,
        ...(kind === 'TARGET_DATE'
          ? { targetDate: target }
          : kind === 'TARGET_AMOUNT'
            ? { targetValue: parsePortfolioNumber(target) }
            : { targetPct: parsePortfolioNumber(target) }),
        monthlyContribution: monthly ? parsePortfolioNumber(monthly) : undefined,
        assumedAnnualRate: rate ? parsePortfolioNumber(rate) : undefined,
      });
    },
    onSuccess: () => {
      setTarget('');
      setMonthly('');
      setRate('');
      void invalidate();
      toast.success('Meta criada!');
    },
    onError: () =>
      toast.error('Não foi possível criar a meta. Confira os campos e tente novamente.'),
  });
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      {removing && (
        <DeleteConfirmation
          title="esta meta"
          onClose={() => setRemoving(null)}
          onConfirm={async () => {
            try {
              await deleteGoal(portfolioId, removing);
              await invalidate();
              toast.success('Meta removida.');
            } catch {
              toast.error('Não foi possível remover a meta. Tente novamente.');
            }
          }}
        />
      )}
      <Card>
        <CardHeader>
          <CardTitle>Suas metas</CardTitle>
          <CardDescription>Acompanhe seu progresso e planeje os próximos passos.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {query.isLoading ? (
            <Skeleton className="h-32 w-full" />
          ) : query.isError ? (
            <p role="alert">
              Não foi possível carregar as metas.{' '}
              <Button variant="link" onClick={() => void query.refetch()}>
                Tentar novamente
              </Button>
            </p>
          ) : !query.data?.length ? (
            <p>Nenhuma meta ainda. Use o formulário para criar a primeira.</p>
          ) : (
            query.data.map(({ goal, currentValue, progressPercent }) => (
              <div key={goal.id} className="flex flex-col gap-2 rounded-lg border p-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-medium">
                    <MaskedValue>
                      {goal.kind === 'TARGET_AMOUNT'
                        ? `Chegar a ${brl.format(goal.targetValue ?? 0)}`
                        : goal.kind === 'TARGET_RETURN_PCT'
                          ? `Retorno de ${goal.targetPct}%`
                          : `Manter a carteira até ${new Date(`${goal.targetDate}T12:00:00`).toLocaleDateString('pt-BR')}`}
                    </MaskedValue>
                  </p>
                  <Button variant="ghost" size="sm" onClick={() => setRemoving(goal.id)}>
                    Remover
                  </Button>
                </div>
                {progressPercent === null ? (
                  <p className="text-sm text-muted-foreground">Progresso ainda indisponível.</p>
                ) : (
                  <>
                    <progress
                      className="h-2 w-full accent-primary"
                      max={100}
                      value={Math.max(0, Math.min(100, progressPercent))}
                      aria-label="Progresso da meta"
                    />
                    <p className="text-sm">
                      {progressPercent.toLocaleString('pt-BR', { maximumFractionDigits: 0 })}%
                      concluído
                    </p>
                  </>
                )}
                <p className="text-sm text-muted-foreground">
                  Patrimônio atual: <MaskedValue>{brl.format(currentValue)}</MaskedValue>
                </p>
              </div>
            ))
          )}
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Nova meta</CardTitle>
          <CardDescription>Escolha um objetivo que faça sentido para você.</CardDescription>
        </CardHeader>
        <CardContent>
          <form
            className="flex flex-col gap-4"
            onSubmit={(event) => {
              event.preventDefault();
              if (valid && !mutation.isPending) mutation.mutate();
            }}
          >
            <fieldset className="flex flex-wrap gap-3">
              <legend className="mb-2 text-sm font-medium">Tipo de meta</legend>
              {(
                [
                  { value: 'TARGET_AMOUNT', label: 'Valor' },
                  { value: 'TARGET_RETURN_PCT', label: 'Retorno' },
                  { value: 'TARGET_DATE', label: 'Prazo' },
                ] as const
              ).map((option) => (
                <label key={option.value} className="flex items-center gap-2 text-sm">
                  <input
                    type="radio"
                    name="goal-kind"
                    checked={kind === option.value}
                    onChange={() => {
                      setKind(option.value);
                      setTarget('');
                    }}
                  />
                  {option.label}
                </label>
              ))}
            </fieldset>
            <label className="flex flex-col gap-2 text-sm">
              {kind === 'TARGET_DATE'
                ? 'Data desejada'
                : kind === 'TARGET_AMOUNT'
                  ? 'Valor desejado (R$)'
                  : 'Retorno desejado (%)'}
              <Input
                required
                type={kind === 'TARGET_DATE' ? 'date' : 'text'}
                inputMode={kind === 'TARGET_DATE' ? undefined : 'decimal'}
                value={target}
                onChange={(event) => setTarget(event.target.value)}
                aria-invalid={
                  !!target &&
                  (kind === 'TARGET_DATE'
                    ? !validPortfolioDate(target)
                    : !(parsePortfolioNumber(target) > 0))
                }
              />
            </label>
            <details>
              <summary className="cursor-pointer text-sm">
                Aportes e taxa estimada (opcional)
              </summary>
              <div className="mt-3 flex flex-col gap-3">
                <label className="flex flex-col gap-2 text-sm">
                  Aporte mensal (R$)
                  <Input
                    inputMode="decimal"
                    value={monthly}
                    onChange={(event) => setMonthly(event.target.value)}
                  />
                </label>
                <label className="flex flex-col gap-2 text-sm">
                  Taxa anual estimada (%)
                  <Input
                    inputMode="decimal"
                    value={rate}
                    onChange={(event) => setRate(event.target.value)}
                  />
                </label>
                <p className="text-xs text-muted-foreground">
                  Premissas de planejamento, não garantia de retorno.
                </p>
              </div>
            </details>
            {!valid && target && (
              <p role="alert" className="text-sm text-destructive">
                Informe uma meta válida. Valores devem ser positivos; aportes e taxa não podem ser
                negativos.
              </p>
            )}
            <Button type="submit" disabled={!valid || mutation.isPending}>
              {mutation.isPending ? 'Criando…' : 'Criar meta'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
