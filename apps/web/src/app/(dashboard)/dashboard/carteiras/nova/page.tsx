'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { createPortfolio, type PortfolioDto } from '@/lib/api-client';
import { useSession } from '@/hooks/use-session';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { toast } from 'sonner';

const profiles = [
  {
    value: 'conservador',
    label: 'Conservadora',
    hint: 'Foco em preservar capital: renda fixa e caixa.',
  },
  {
    value: 'moderado',
    label: 'Moderada',
    hint: 'Equilíbrio entre renda fixa e renda variável.',
  },
  {
    value: 'arrojado',
    label: 'Arrojada',
    hint: 'Maior exposição a risco em troca de retorno potencial.',
  },
] as const;

/** Wizard de criação de carteira (título/descrição/perfil — visibilidade fica pra M-P5). */
export default function NovaCarteiraPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { isAuthenticated, isReady } = useSession();

  const [title, setTitle] = React.useState('');
  const [description, setDescription] = React.useState('');
  const [riskProfile, setRiskProfile] =
    React.useState<(typeof profiles)[number]['value']>('moderado');

  React.useEffect(() => {
    if (isReady && !isAuthenticated) router.replace('/entrar');
  }, [isReady, isAuthenticated, router]);

  const mutation = useMutation({
    mutationFn: createPortfolio,
    onSuccess: (portfolio: PortfolioDto) => {
      void queryClient.invalidateQueries({ queryKey: ['portfolios'] });
      toast.success('Carteira criada!');
      router.push(`/dashboard/c/${portfolio.id}`);
    },
    onError: () => toast.error('Não foi possível criar a carteira. Tente novamente.'),
  });

  return (
    <div className="mx-auto w-full max-w-xl space-y-6 px-4 py-8">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Nova carteira</h1>
        <p className="text-sm text-muted-foreground">
          Dê um nome, descreva seu objetivo e escolha o perfil da carteira.
        </p>
      </header>

      <form
        className="space-y-5"
        onSubmit={(e) => {
          e.preventDefault();
          if (!title.trim()) return;
          mutation.mutate({
            title: title.trim(),
            description: description.trim() || undefined,
            riskProfile,
          });
        }}
      >
        <div className="space-y-2">
          <label className="text-sm font-medium" htmlFor="titulo">
            Título
          </label>
          <Input
            id="titulo"
            placeholder="Ex.: Aposentadoria 2045"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            maxLength={120}
            required
          />
        </div>

        <div className="space-y-2">
          <label className="text-sm font-medium" htmlFor="descricao">
            Descrição (opcional)
          </label>
          <Textarea
            id="descricao"
            placeholder="Qual o objetivo desta carteira?"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={1000}
            rows={3}
          />
        </div>

        <fieldset className="space-y-2">
          <legend className="mb-1 text-sm font-medium">Perfil</legend>
          <div className="grid gap-2">
            {profiles.map((p) => (
              <label
                key={p.value}
                className={`flex cursor-pointer items-start gap-3 rounded-lg border p-3 transition-colors ${
                  riskProfile === p.value ? 'border-primary bg-primary/5' : 'hover:bg-muted/50'
                }`}
              >
                <input
                  type="radio"
                  name="perfil"
                  className="mt-1"
                  checked={riskProfile === p.value}
                  onChange={() => setRiskProfile(p.value)}
                />
                <span>
                  <span className="block text-sm font-medium">{p.label}</span>
                  <span className="block text-xs text-muted-foreground">{p.hint}</span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={() => router.back()}>
            Cancelar
          </Button>
          <Button type="submit" disabled={mutation.isPending || !title.trim()}>
            {mutation.isPending ? 'Criando…' : 'Criar carteira'}
          </Button>
        </div>
      </form>
    </div>
  );
}
