'use client';

import * as React from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  attachFixedIncome,
  createTransaction,
  fetchAssetLookup,
  type AssetLookupDto,
  type FixedIncomeParamDto,
} from '@/lib/api-client';
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
  { value: 'CORP_ACTION', label: 'Evento corporativo' },
] as const;

type OperationType = (typeof operationTypes)[number]['value'];

const corpActionKinds = [
  { value: 'split', label: 'Desdobramento (split)', factorLabel: 'Fator (ex.: 2 dobra)' },
  { value: 'grupamento', label: 'Grupamento (inpc)', factorLabel: 'Fator (ex.: 2 agrupa)' },
  { value: 'bonificacao', label: 'Bonificação', factorLabel: '' },
  { value: 'subscricao', label: 'Subscrição', factorLabel: '' },
] as const;

const syntheticOptions = [
  { code: 'CDI', label: 'Caixa CDI' },
  { code: 'SELIC', label: 'Caixa Selic' },
] as const;

const indexerOptions = [
  { value: 'CDI_PERCENT', label: '% do CDI' },
  { value: 'CDI_PLUS', label: 'CDI +' },
  { value: 'PREFIXED', label: 'Prefixado a.a.' },
] as const;

interface WizardState {
  type: OperationType;
  ticker: string;
  asset: AssetLookupDto | null;
  syntheticCode: string | null;
  broker: string;
  tradeDate: string;
  quantity: string;
  unitPrice: string;
  fees: string;
  corpKind: string;
  corpFactor: string;
  corpPercent: string;
  rfIndexer: string;
  rfRate: string;
  rfMaturity: string;
}

function makeInitialState(): WizardState {
  return {
    type: 'BUY',
    ticker: '',
    asset: null,
    syntheticCode: null,
    broker: '',
    tradeDate: new Date().toISOString().slice(0, 10),
    quantity: '',
    unitPrice: '',
    fees: '0',
    corpKind: 'split',
    corpFactor: '2',
    corpPercent: '',
    rfIndexer: 'CDI_PERCENT',
    rfRate: '100',
    rfMaturity: '',
  };
}

/** Total em R$ que a operação movimenta; `null` = evento sem fluxo de caixa. */
function previewTotal(state: WizardState): number | null {
  const qty = Number(state.quantity.replace(',', '.')) || 0;
  const price = Number(state.unitPrice.replace(',', '.')) || 0;
  if (state.type === 'INCOME') return qty;
  if (state.type === 'CORP_ACTION') return state.corpKind === 'subscricao' ? qty * price : null;
  return state.syntheticCode ? qty : qty * price;
}

function corpActionLabel(state: WizardState): string {
  if (state.corpKind === 'bonificacao') return `Bonificação de ${state.corpPercent}%`;
  if (state.corpKind === 'subscricao') return 'Subscrição';
  const label = corpActionKinds.find((k) => k.value === state.corpKind)?.label ?? state.corpKind;
  return `${label} ×${state.corpFactor}`;
}

function buildCorpActionJson(state: WizardState): string | null {
  if (state.type !== 'CORP_ACTION') return null;
  if (state.corpKind === 'bonificacao') {
    const pctNum = Number(state.corpPercent.replace(',', '.')) || 0;
    return JSON.stringify({ kind: 'bonificacao', percent: pctNum });
  }
  const factorNum = Number(state.corpFactor.replace(',', '.')) || 1;
  return JSON.stringify({ kind: state.corpKind, factor: factorNum });
}

/** Wizard de transação em 3 etapas + revisão (decisão do grill §3). */
export default function NovaTransacaoPage() {
  const router = useRouter();
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const { isAuthenticated, isReady } = useSession();

  const [step, setStep] = React.useState(1);
  const [state, setState] = React.useState<WizardState>(makeInitialState);
  const [tickerError, setTickerError] = React.useState<string | null>(null);
  const [lookingUp, setLookingUp] = React.useState(false);

  React.useEffect(() => {
    if (isReady && !isAuthenticated) router.replace('/entrar');
  }, [isReady, isAuthenticated, router]);

  const patch = (partial: Partial<WizardState>) => setState((s) => ({ ...s, ...partial }));

  const lookupTicker = async () => {
    const ticker = state.ticker.trim().toUpperCase();
    if (!ticker) return;
    setLookingUp(true);
    try {
      const asset = await fetchAssetLookup(ticker);
      if (asset) {
        patch({ asset });
        setTickerError(null);
        setStep(2);
      } else {
        patch({ asset: null });
        setTickerError(`Não encontramos "${ticker}" no catálogo. Verifique o código do ativo.`);
      }
    } catch {
      // Falha de rede/HTTP no lookup não pode virar rejeição silenciosa.
      setTickerError('Não foi possível verificar o ativo agora. Tente novamente.');
    } finally {
      setLookingUp(false);
    }
  };

  const mutation = useMutation({
    mutationFn: async () => {
      const qty = Number(state.quantity.replace(',', '.')) || 0;
      const price = Number(state.unitPrice.replace(',', '.')) || 0;
      const fees = Number(state.fees.replace(',', '.')) || 0;
      const targetAssetId = state.syntheticCode ? null : (state.asset?.id ?? null);

      let created;
      if (state.type === 'INCOME') {
        // O campo "Valor total bruto" É o rendimento; preço por cota é opcional e informativo.
        created = await createTransaction(id, {
          type: 'INCOME',
          assetId: targetAssetId,
          syntheticIndexCode: state.syntheticCode,
          broker: state.broker,
          grossAmount: qty,
        });
      } else if (state.type === 'CORP_ACTION') {
        created = await createTransaction(id, {
          type: 'CORP_ACTION',
          assetId: targetAssetId,
          syntheticIndexCode: state.syntheticCode,
          broker: state.broker,
          quantity: qty,
          unitPrice: state.corpKind === 'subscricao' ? price : undefined,
          grossAmount: state.corpKind === 'subscricao' ? qty * price : 0,
          corpActionJson: buildCorpActionJson(state) ?? undefined,
          tradeDate: state.tradeDate,
        });
      } else {
        created = await createTransaction(id, {
          type: state.type,
          assetId: targetAssetId,
          syntheticIndexCode: state.syntheticCode,
          broker: state.broker,
          quantity: qty,
          unitPrice: state.syntheticCode ? 1 : price,
          grossAmount: state.syntheticCode ? qty : qty * price,
          fees: state.type === 'BUY' ? fees : undefined,
          tradeDate: state.tradeDate,
        });
      }

      // Parâmetros de rendimento para caixa sintético (accrual local-first).
      // Falha aqui NÃO deve rejeitar a mutation: a transação já foi criada —
      // retentar o fluxo inteiro duplicaria o lançamento.
      if (created && state.type === 'BUY' && state.syntheticCode && state.rfMaturity) {
        try {
          await attachFixedIncome(id, {
            syntheticIndexCode: state.syntheticCode,
            indexer: state.rfIndexer as FixedIncomeParamDto['indexer'],
            indexerRate: Number(state.rfRate.replace(',', '.')) || 100,
            principal: qty,
            startDate: state.tradeDate,
            maturityDate: state.rfMaturity,
          });
        } catch {
          toast.error('Transação lançada, mas não foi possível salvar o rendimento do caixa.');
        }
      }
      return created;
    },
    onSuccess: () => {
      toast.success('Transação lançada!');
      void queryClient.invalidateQueries({ queryKey: ['portfolio', id] });
      void queryClient.invalidateQueries({ queryKey: ['portfolios'] });
      router.push(`/dashboard/c/${id}`);
    },
    onError: (error: Error) => toast.error(error.message),
  });

  const total = previewTotal(state);

  const canAdvanceFrom2 =
    state.type === 'INCOME'
      ? Number(state.quantity.replace(',', '.')) > 0
      : state.type === 'CORP_ACTION'
        ? state.corpKind === 'subscricao'
          ? Number(state.quantity.replace(',', '.')) > 0 &&
            Number(state.unitPrice.replace(',', '.')) > 0
          : state.corpKind === 'bonificacao'
            ? Number(state.corpPercent.replace(',', '.')) > 0
            : Number(state.corpFactor.replace(',', '.')) > 1
        : state.syntheticCode
          ? Number(state.quantity.replace(',', '.')) > 0
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
              <label className="text-sm font-medium">Ou caixa sintético</label>
              <div className="flex gap-2">
                {syntheticOptions.map((o) => (
                  <button
                    key={o.code}
                    type="button"
                    onClick={() =>
                      patch({
                        syntheticCode: state.syntheticCode === o.code ? null : o.code,
                        ticker: '',
                        asset: null,
                      })
                    }
                    className={`rounded-lg border px-3 py-1.5 text-xs transition-colors ${
                      state.syntheticCode === o.code
                        ? 'border-primary bg-primary/5 font-medium'
                        : 'hover:bg-muted/50'
                    }`}
                  >
                    {o.label}
                  </button>
                ))}
              </div>
              <p className="text-xs text-muted-foreground">
                Para conta remunerada / caixa. ⛔ Tesouro e previdência como classe catalogada:
                bloqueados (curadoria de ativos pendente).
              </p>
            </div>

            {state.type === 'CORP_ACTION' ? (
              <fieldset className="space-y-2 rounded-lg border p-3">
                <legend className="px-1 text-sm font-medium">Evento</legend>
                <div className="grid grid-cols-3 gap-2">
                  {corpActionKinds.map((k) => (
                    <button
                      key={k.value}
                      type="button"
                      onClick={() => patch({ corpKind: k.value })}
                      className={`rounded-lg border p-2 text-xs transition-colors ${
                        state.corpKind === k.value
                          ? 'border-primary bg-primary/5 font-medium'
                          : 'hover:bg-muted/50'
                      }`}
                    >
                      {k.label}
                    </button>
                  ))}
                </div>
                {state.corpKind === 'bonificacao' ? (
                  <Input
                    inputMode="decimal"
                    placeholder="Percentual de bonificação (ex.: 10)"
                    value={state.corpPercent}
                    onChange={(e) => patch({ corpPercent: e.target.value })}
                  />
                ) : state.corpKind === 'subscricao' ? null : (
                  <Input
                    inputMode="decimal"
                    placeholder={
                      corpActionKinds.find((k) => k.value === state.corpKind)?.factorLabel
                    }
                    value={state.corpFactor}
                    onChange={(e) => patch({ corpFactor: e.target.value })}
                  />
                )}
                {state.corpKind === 'subscricao' ? (
                  <p className="text-xs text-muted-foreground">
                    Informe quantidade e preço unitário na próxima etapa.
                  </p>
                ) : (
                  <p className="text-xs text-muted-foreground">
                    A quantidade do evento é ajustada automaticamente sobre a posição existente.
                  </p>
                )}
              </fieldset>
            ) : null}

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
                disabled={
                  lookingUp ||
                  (!state.ticker.trim() && !state.syntheticCode) ||
                  !state.broker.trim()
                }
                onClick={() =>
                  state.syntheticCode
                    ? (patch({ asset: null }), setTickerError(null), setStep(2))
                    : void lookupTicker()
                }
              >
                {lookingUp ? 'Verificando…' : 'Continuar'}
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
                    {state.syntheticCode ? (
                      <>
                        <Input id="preco" value="R$ 1,00 (cota do caixa)" readOnly disabled />
                        <p className="text-xs text-muted-foreground">
                          Cada cota do caixa sintético vale R$ 1,00.
                        </p>
                      </>
                    ) : (
                      <Input
                        id="preco"
                        inputMode="decimal"
                        placeholder="0,00"
                        value={state.unitPrice}
                        onChange={(e) => patch({ unitPrice: e.target.value })}
                      />
                    )}
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
                {state.syntheticCode && state.type === 'BUY' ? (
                  <fieldset className="space-y-2 rounded-lg border p-3">
                    <legend className="px-1 text-sm font-medium">
                      Rendimento do caixa (opcional)
                    </legend>
                    <div className="grid grid-cols-3 gap-2">
                      {indexerOptions.map((o) => (
                        <button
                          key={o.value}
                          type="button"
                          onClick={() => patch({ rfIndexer: o.value })}
                          className={`rounded-lg border px-2 py-1.5 text-[11px] transition-colors ${
                            state.rfIndexer === o.value
                              ? 'border-primary bg-primary/5 font-medium'
                              : 'hover:bg-muted/50'
                          }`}
                        >
                          {o.label}
                        </button>
                      ))}
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <label className="text-xs text-muted-foreground" htmlFor="rf-taxa">
                          {state.rfIndexer === 'CDI_PERCENT' ? '% do CDI' : 'Taxa % a.a.'}
                        </label>
                        <Input
                          id="rf-taxa"
                          inputMode="decimal"
                          value={state.rfRate}
                          onChange={(e) => patch({ rfRate: e.target.value })}
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-xs text-muted-foreground" htmlFor="rf-venc">
                          Vencimento
                        </label>
                        <Input
                          id="rf-venc"
                          type="date"
                          value={state.rfMaturity}
                          onChange={(e) => patch({ rfMaturity: e.target.value })}
                        />
                      </div>
                    </div>
                    <p className="text-[11px] text-muted-foreground">
                      Correção calculada localmente pelas séries oficiais (CDI/Selic/IPCA).
                    </p>
                  </fieldset>
                ) : null}
                {total !== null ? (
                  <p className="text-sm tabular-nums text-muted-foreground">
                    Total: {brl.format(total)}
                  </p>
                ) : null}
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
              value={
                state.ticker
                  ? `${state.ticker}${state.asset ? ` — ${state.asset.name}` : ''}`
                  : state.syntheticCode
                    ? `Caixa ${state.syntheticCode}`
                    : '—'
              }
            />
            <ReviewRow label="Corretora" value={state.broker} />
            <ReviewRow
              label="Data"
              value={new Date(`${state.tradeDate}T12:00:00`).toLocaleDateString('pt-BR')}
            />
            {state.type === 'CORP_ACTION' ? (
              <>
                <ReviewRow label="Evento" value={corpActionLabel(state)} />
                {state.corpKind === 'subscricao' ? (
                  <>
                    <ReviewRow label="Quantidade" value={state.quantity} />
                    <ReviewRow
                      label="Preço unitário"
                      value={brl.format(Number(state.unitPrice.replace(',', '.') || 0))}
                    />
                  </>
                ) : null}
              </>
            ) : null}
            {state.type === 'INCOME' ? (
              <ReviewRow
                label="Bruto recebido"
                value={brl.format(Number(state.quantity.replace(',', '.') || 0))}
              />
            ) : state.type !== 'CORP_ACTION' ? (
              <>
                <ReviewRow label="Quantidade" value={state.quantity} />
                <ReviewRow
                  label="Preço unitário"
                  value={
                    state.syntheticCode
                      ? 'R$ 1,00'
                      : brl.format(Number(state.unitPrice.replace(',', '.') || 0))
                  }
                />
                {state.type === 'BUY' ? (
                  <ReviewRow
                    label="Despesas"
                    value={brl.format(Number(state.fees.replace(',', '.') || 0))}
                  />
                ) : null}
              </>
            ) : null}
            {total !== null ? <ReviewRow label="Total" value={brl.format(total)} strong /> : null}

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
