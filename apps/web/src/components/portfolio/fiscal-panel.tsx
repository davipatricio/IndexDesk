'use client';

import * as React from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import {
  simulateRedemption,
  fetchTaxProjection,
  type PositionDto,
  type TaxProjectionDto,
} from '@/lib/api-client';
import { MaskedValue } from '@/components/privacy/masked-value';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { toast } from 'sonner';

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

/**
 * Aba Fiscal (M-P4): simulador de resgate com IR/IOF/come-cotas/isenções
 * e projeção de DARF "se vender hoje". Camada educacional — sempre com disclaimers.
 */
export function FiscalPanel({
  portfolioId,
  positions,
}: {
  portfolioId: string;
  positions: PositionDto[];
}) {
  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      <RedemptionSimulator portfolioId={portfolioId} positions={positions} />
      <DarfCard portfolioId={portfolioId} />
    </div>
  );
}

function RedemptionSimulator({
  portfolioId,
  positions,
}: {
  portfolioId: string;
  positions: PositionDto[];
}) {
  const sellable = positions.filter((p) => p.assetId && p.quantity > 0);
  const [assetKey, setAssetKey] = React.useState('');
  const [quantity, setQuantity] = React.useState('');

  const selected = sellable.find((p) => `${p.assetId}:${p.broker}` === assetKey);

  const mutation = useMutation({
    mutationFn: () =>
      simulateRedemption(portfolioId, {
        assetId: selected!.assetId!,
        broker: selected!.broker,
        quantity: quantity.trim() ? Number(quantity.replace(',', '.')) : undefined,
      }),
    onSuccess: () => toast.success('Simulação concluída.'),
    onError: (e: Error) => toast.error(e.message),
  });

  if (sellable.length === 0) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Simulador de resgate</CardTitle>
          <CardDescription>Sem posições vendáveis ainda.</CardDescription>
        </CardHeader>
      </Card>
    );
  }

  const r = mutation.data;

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Simulador de resgate</CardTitle>
        <CardDescription>
          IR pela tabela regressiva, IOF, come-cotas e isenções. Parcial ou total.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="space-y-1">
          <label className="text-xs text-muted-foreground" htmlFor="sim-ativo">
            Posição
          </label>
          <select
            id="sim-ativo"
            value={assetKey}
            onChange={(e) => setAssetKey(e.target.value)}
            className="w-full rounded-lg border bg-background px-2 py-1.5 text-sm"
          >
            <option value="">Selecione…</option>
            {sellable.map((p) => (
              <option key={`${p.assetId}:${p.broker}`} value={`${p.assetId}:${p.broker}`}>
                {p.ticker} · {p.broker} · {p.quantity}
              </option>
            ))}
          </select>
        </div>

        <div className="space-y-1">
          <label className="text-xs text-muted-foreground" htmlFor="sim-qtd">
            Quantidade (vazio = total)
          </label>
          <Input
            id="sim-qtd"
            inputMode="decimal"
            placeholder={`Até ${selected?.quantity ?? 0}`}
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
          />
        </div>

        <Button
          disabled={!selected || mutation.isPending}
          onClick={() => mutation.mutate()}
          className="w-full"
        >
          {mutation.isPending ? 'Calculando…' : 'Simular resgate'}
        </Button>

        {r ? (
          <div className="space-y-2 rounded-lg border p-3 text-sm">
            <Row
              label="Valor bruto"
              value={<MaskedValue>{brl.format(r.grossAmount)}</MaskedValue>}
            />
            <Row label="Custo" value={<MaskedValue>{brl.format(r.costBasis)}</MaskedValue>} />
            <Row label="Lucro" value={<MaskedValue>{brl.format(r.profit)}</MaskedValue>} />
            {r.comeCotasAlreadyPaid > 0 ? (
              <Row
                label="Come-cotas já pago"
                value={<MaskedValue>{brl.format(r.comeCotasAlreadyPaid)}</MaskedValue>}
                hint="Antecipações semestrais reduziram o rendimento"
              />
            ) : null}
            {r.exemptApplied ? (
              <div className="flex items-center gap-2">
                <Badge>Isento</Badge>
                <span className="text-xs text-muted-foreground">{r.exemptReason}</span>
              </div>
            ) : (
              <>
                <Row
                  label={`IR (${r.irPercent.toFixed(2)}%)`}
                  value={<MaskedValue>{brl.format(-r.irAmount)}</MaskedValue>}
                />
                {r.iofAmount > 0 ? (
                  <Row label="IOF" value={<MaskedValue>{brl.format(-r.iofAmount)}</MaskedValue>} />
                ) : null}
              </>
            )}
            <div className="border-t pt-2">
              <Row
                label="Líquido"
                value={<MaskedValue>{brl.format(r.netAmount)}</MaskedValue>}
                strong
              />
            </div>
            <details className="text-[11px] text-muted-foreground">
              <summary className="cursor-pointer">Premissas</summary>
              <ul className="mt-1 list-inside list-disc space-y-0.5">
                {r.premises.map((p) => (
                  <li key={p}>{p}</li>
                ))}
              </ul>
              <p className="mt-2">{r.disclaimer}</p>
            </details>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function DarfCard({ portfolioId }: { portfolioId: string }) {
  const now = new Date();
  const [monthOffset, setMonthOffset] = React.useState(0);

  const target = new Date(now.getFullYear(), now.getMonth() + monthOffset, 1);
  const year = target.getFullYear();
  const month = target.getMonth() + 1;

  const query = useQuery({
    queryKey: ['portfolio', portfolioId, 'tax', year, month],
    queryFn: () => fetchTaxProjection(portfolioId, year, month),
    staleTime: 10 * 60 * 1000,
  });

  const d: TaxProjectionDto | undefined = query.data;

  return (
    <Card>
      <CardHeader className="gap-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">Projeção DARF</CardTitle>
          <div className="flex items-center gap-1 text-xs">
            <Button
              variant="outline"
              size="xs"
              aria-label="Mês anterior"
              onClick={() => setMonthOffset((m) => m - 1)}
            >
              ←
            </Button>
            <span className="tabular-nums">
              {String(month).padStart(2, '0')}/{year}
            </span>
            <Button
              variant="outline"
              size="xs"
              aria-label="Próximo mês"
              disabled={monthOffset >= 0}
              onClick={() => setMonthOffset((m) => Math.min(0, m + 1))}
            >
              →
            </Button>
          </div>
        </div>
        <CardDescription>Vendas realizadas no mês — código 6015.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2 text-sm">
        {query.isLoading ? (
          <p className="text-xs text-muted-foreground">Calculando…</p>
        ) : !d || d.items.length === 0 ? (
          <p className="text-xs text-muted-foreground">
            Nenhuma venda com imposto devido neste mês.
          </p>
        ) : (
          <>
            {d.items.map((i) => (
              <div key={i.assetClass} className="flex items-center justify-between">
                <span>{i.assetClass}</span>
                <span className="tabular-nums">
                  <MaskedValue>{brl.format(i.taxDue)}</MaskedValue>
                </span>
              </div>
            ))}
            <div className="flex items-center justify-between border-t pt-2 font-semibold">
              <span>Total devido</span>
              <span className="tabular-nums">
                <MaskedValue>
                  {brl.format(d.items.reduce((acc, i) => acc + i.taxDue, 0))}
                </MaskedValue>
              </span>
            </div>
            {d.items[0]?.dueDate ? (
              <p className="text-[11px] text-muted-foreground">
                Vencimento: {new Date(`${d.items[0].dueDate}T12:00:00`).toLocaleDateString('pt-BR')}
              </p>
            ) : null}
            <details className="text-[11px] text-muted-foreground">
              <summary className="cursor-pointer">Premissas</summary>
              <ul className="mt-1 list-inside list-disc space-y-0.5">
                {d.premises.map((p) => (
                  <li key={p}>{p}</li>
                ))}
              </ul>
              <p className="mt-2">{d.disclaimer}</p>
            </details>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Row({
  label,
  value,
  strong,
  hint,
}: {
  label: string;
  value: React.ReactNode;
  strong?: boolean;
  hint?: string;
}) {
  return (
    <div className="flex items-center justify-between gap-3">
      <span className="text-muted-foreground">
        {label}
        {hint ? <span className="block text-[10px]">{hint}</span> : null}
      </span>
      <span className={`tabular-nums ${strong ? 'font-semibold' : ''}`}>{value}</span>
    </div>
  );
}
