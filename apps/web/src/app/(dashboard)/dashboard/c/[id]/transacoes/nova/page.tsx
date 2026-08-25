'use client';

import * as React from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation } from '@tanstack/react-query';
import { createTransaction, fetchAssetLookup, type AssetLookupDto } from '@/lib/api-client';
import { useSession } from '@/hooks/use-session';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { toast } from 'sonner';

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

const operationTypes = [
  { value: 'BUY', label: 'Compra' },
  { value: 'SELL', label: 'Venda' },
  { value: 'INCOME', label: 'Provento / rendimento' },
] as const;

type OperationType = (typeof operationTypes)[number]['value'];

interface WizardState {
  type: OperationType;
  ticker: string;
  asset: AssetLookupDto | null;
  broker: string;
  tradeDate: string;
  quantity: string;
  unitPrice: string;
  fees: string;
}

const initialState: WizardState = {
  type: 'BUY',
  ticker: '',
  asset: null,
  broker: '',
  tradeDate: new Date().toISOString().slice(0, 10),
  quantity: '',
  unitPrice: '',
  fees: '0',
};

/** Wizard de transação em 3 etapas + revisão (decisão do grill §3). */
export default function NovaTransacaoPage() {
  const router = useRouter();
  const { id } = useParams<{ id: string }>();
  const { isAuthenticated, isReady } = useSession();

  const [step, setStep] = React.useState(1);
  const [state, setState] = React.useState<WizardState>(initialState);
  const [tickerError, setTickerError] = React.useState<string | null>(null);

  React.useEffect(() => {
    if (isReady && !isAuthenticated) router.replace('/entrar');
  }, [isReady, isAuthenticated, router]);

  const patch = (partial: Partial<WizardState>) => setState((s) => ({ ...s, ...partial }));

  const lookupTicker = async () => {
    const ticker = state.ticker.trim().toUpperCase();
    if (!ticker) return;
    const asset = await fetchAssetLookup(ticker);
    if (asset) {
      patch({ asset });
      setTickerError(null);
      setStep(2);
    } else {
      patch({ asset: null });
      setTickerError(`Não encontramos "${ticker}" no catálogo. Verifique o código do ativo.`);
    }
  };

  const mutation = useMutation({
    mutationFn: () => {
      const qty = Number(state.quantity.replace(',', '.'));
      const price = Number(state.unitPrice.replace(',', '.'));
      const fees = Number(state.fees.replace(',', '.')) || 0;

      if (state.type === 'INCOME') {
        return createTransaction(id, {
          type: 'INCOME',
          assetId: state.asset?.id ?? null,
          broker: state.broker,
          grossAmount: qty * price,
        });
      }
      return createTransaction(id, {
        type: state.type,
        assetId: state.asset?.id ?? null,
        broker: state.broker,
        quantity: qty,
        unitPrice: price,
        grossAmount: qty * price,
        fees: state.type === 'BUY' ? fees : undefined,
        tradeDate: state.tradeDate,
      });
    },
    onSuccess: () => {
      toast.success('Transação lançada!');
      router.push(`/dashboard/c/${id}`);
    },
    onError: (error: Error) => toast.error(error.message),
  });

  const total =
    Number(state.quantity.replace(',', '.') || 0) * Number(state.unitPrice.replace(',', '.') || 0);

  const canAdvanceFrom2 =
    state.type === 'INCOME'
      ? Number(state.quantity) >= 0 && Number(state.unitPrice) > 0
      : Number(state.quantity.replace(',', '.')) > 0 &&
        Number(state.unitPrice.replace(',', '.')) > 0;

  return (
    <div className="mx-auto w-full max-w-xl space-y-6 px-4 py-8">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Nova transação</h1>
        {/* Stepper */}
        <ol className="mt-3 flex items-center gap-2 text-xs text-muted-foreground">
          {['Identificação', 'Valores', 'Revisão'].map((label, i) => (
            <li key={label} className="flex items-center gap-2">
              <span
                className={`flex h-5 w-5 items-center justify-center rounded-full border ${
                  step > i + 1 ? 'bg-primary text-primary-foreground' : ''
                } ${step === i + 1 ? 'border-primary font-medium text-foreground' : ''}`}
              >
                {i + 1}
              </span>
              {label}
              {i < 2 ? <span aria-hidden>·</span> : null}
            </li>
          ))}
        </ol>
      </header>

      {step === 1 ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">O que você fez?</CardTitle>
            <CardDescription>Tipo de operação, ativo, corretora e data.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-3 gap-2">
              {operationTypes.map((t) => (
                <button
                  key={t.value}
                  type="button"
                  onClick={() => patch({ type: t.value })}
                  className={`rounded-lg border p-2 text-sm transition-colors ${
                    state.type === t.value
                      ? 'border-primary bg-primary/5 font-medium'
                      : 'hover:bg-muted/50'
                  }`}
                >
                  {t.label}
                </button>
              ))}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="ticker">
                Código do ativo
              </label>
              <Input
                id="ticker"
                placeholder="Ex.: IVVB11"
                value={state.ticker}
                onChange={(e) => patch({ ticker: e.target.value.toUpperCase() })}
                autoCapitalize="characters"
              />
              {tickerError ? (
                <p className="text-xs text-red-600">{tickerError}</p>
              ) : state.asset ? (
                <p className="text-xs text-emerald-600">
                  ✓ {state.asset.name} ({state.asset.assetType})
                </p>
              ) : null}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="broker">
                Corretora
              </label>
              <Input
                id="broker"
                placeholder="Ex.: XP, Nu Invest, Binance…"
                value={state.broker}
                onChange={(e) => patch({ broker: e.target.value })}
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="data">
                Data da operação
              </label>
              <Input
                id="data"
                type="date"
                value={state.tradeDate}
                onChange={(e) => patch({ tradeDate: e.target.value })}
              />
              <p className="text-xs text-muted-foreground">
                Pode ser uma data antiga — a carteira é recalculada desde lá.
              </p>
            </div>

            <div className="flex justify-between">
              <Button variant="ghost" onClick={() => router.back()}>
                Cancelar
              </Button>
              <Button
                disabled={!state.ticker.trim() || !state.broker.trim()}
                onClick={() => void lookupTicker()}
              >
                Continuar
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : step === 2 ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Quanto e por quanto?</CardTitle>
            <CardDescription>
              {state.type === 'INCOME'
                ? 'Informe o valor bruto recebido.'
                : 'Quantidade, preço unitário e despesas.'}
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            {state.type !== 'INCOME' ? (
              <>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-2">
                    <label className="text-sm font-medium" htmlFor="qtd">
                      Quantidade
                    </label>
                    <Input
                      id="qtd"
                      inputMode="decimal"
                      placeholder="0,00"
                      value={state.quantity}
                      onChange={(e) => patch({ quantity: e.target.value })}
                    />
                    <p className="text-xs text-muted-foreground">Frações são aceitas.</p>
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium" htmlFor="preco">
                      Valor unitário
                    </label>
                    <Input
                      id="preco"
                      inputMode="decimal"
                      placeholder="0,00"
                      value={state.unitPrice}
                      onChange={(e) => patch({ unitPrice: e.target.value })}
                    />
                  </div>
                </div>
                {state.type === 'BUY' ? (
                  <div className="space-y-2">
                    <label className="text-sm font-medium" htmlFor="taxas">
                      Despesas / taxas
                    </label>
                    <Input
                      id="taxas"
                      inputMode="decimal"
                      value={state.fees}
                      onChange={(e) => patch({ fees: e.target.value })}
                    />
                    <p className="text-xs text-muted-foreground">
                      Corretagem, emolumentos etc. — entram no preço médio.
                    </p>
                  </div>
                ) : null}
                <p className="text-sm tabular-nums text-muted-foreground">
                  Total: {brl.format(total)}
                </p>
              </>
            ) : (
              <>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-2">
                    <label className="text-sm font-medium" htmlFor="qtd-bruto">
                      Valor total bruto
                    </label>
                    <Input
                      id="qtd-bruto"
                      inputMode="decimal"
                      placeholder="0,00"
                      value={state.quantity}
                      onChange={(e) => patch({ quantity: e.target.value })}
                    />
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-medium" htmlFor="preco-cota">
                      Por cota / unidade (opcional)
                    </label>
                    <Input
                      id="preco-cota"
                      inputMode="decimal"
                      placeholder="0,00"
                      value={state.unitPrice}
                      onChange={(e) => patch({ unitPrice: e.target.value })}
                    />
                  </div>
                </div>
              </>
            )}

            <div className="flex justify-between">
              <Button variant="ghost" onClick={() => setStep(1)}>
                Voltar
              </Button>
              <Button disabled={!canAdvanceFrom2} onClick={() => setStep(3)}>
                Revisar
              </Button>
            </div>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Confira antes de salvar</CardTitle>
            <CardDescription>Nada é enviado até você confirmar.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            <ReviewRow
              label="Operação"
              value={operationTypes.find((t) => t.value === state.type)?.label ?? ''}
            />
            <ReviewRow
              label="Ativo"
              value={`${state.ticker}${state.asset ? ` — ${state.asset.name}` : ''}`}
            />
            <ReviewRow label="Corretora" value={state.broker} />
            <ReviewRow
              label="Data"
              value={new Date(`${state.tradeDate}T12:00:00`).toLocaleDateString('pt-BR')}
            />
            {state.type !== 'INCOME' ? (
              <>
                <ReviewRow label="Quantidade" value={state.quantity} />
                <ReviewRow
                  label="Preço unitário"
                  value={brl.format(Number(state.unitPrice.replace(',', '.') || 0))}
                />
                {state.type === 'BUY' ? (
                  <ReviewRow
                    label="Despesas"
                    value={brl.format(Number(state.fees.replace(',', '.') || 0))}
                  />
                ) : null}
              </>
            ) : (
              <ReviewRow label="Bruto recebido" value={brl.format(Number(state.quantity || 0))} />
            )}
            <ReviewRow label="Total" value={brl.format(total)} strong />

            <div className="flex justify-between pt-2">
              <Button variant="ghost" onClick={() => setStep(2)}>
                Voltar
              </Button>
              <Button onClick={() => mutation.mutate()} disabled={mutation.isPending}>
                {mutation.isPending ? 'Salvando…' : 'Confirmar transação'}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function ReviewRow({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={`flex items-center justify-between gap-4 ${strong ? 'font-medium' : ''}`}>
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right">{value}</span>
    </div>
  );
}
