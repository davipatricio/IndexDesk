'use client';

import Link from 'next/link';
import { type PositionDto } from '@/lib/api-client';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { MaskedValue } from '@/components/privacy/masked-value';

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

function formatQty(q: number): string {
  return q % 1 === 0 ? String(q) : q.toLocaleString('pt-BR', { maximumFractionDigits: 8 });
}

/**
 * Tabela de posições. Sensíveis (valores/retorno) envoltos em <MaskedValue>;
 * coluna Peso (% do patrimônio — composição) continua visível por decisão.
 */
export function PositionsTable({ positions }: { positions: PositionDto[] }) {
  const totalValue = positions.reduce((acc, p) => acc + p.currentValue, 0);
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Ativo</TableHead>
          <TableHead>Corretora</TableHead>
          <TableHead className="text-right">Quantidade</TableHead>
          <TableHead className="text-right">Preço médio</TableHead>
          <TableHead className="text-right">Atual</TableHead>
          <TableHead className="text-right">Valor</TableHead>
          <TableHead className="text-right">Peso</TableHead>
          <TableHead className="text-right" title="Fatia do lucro total gerado pela posição">
            Contrib.
          </TableHead>
          <TableHead className="text-right">Resultado</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {positions.map((p) => (
          <TableRow key={`${p.ticker}-${p.broker}-${p.assetId ?? 'na'}`}>
            <TableCell>
              {p.assetId ? (
                <Link
                  href={`/ativos/${encodeURIComponent(p.ticker.toLowerCase())}`}
                  className="font-medium hover:underline"
                >
                  {p.ticker}
                </Link>
              ) : (
                <span className="font-medium">{p.ticker}</span>
              )}
              <span className="block text-xs text-muted-foreground">{p.name}</span>
            </TableCell>
            <TableCell>{p.broker}</TableCell>
            <TableCell className="text-right tabular-nums">
              <MaskedValue>{formatQty(p.quantity)}</MaskedValue>
            </TableCell>
            <TableCell className="text-right tabular-nums">
              <MaskedValue>{brl.format(p.averagePrice)}</MaskedValue>
            </TableCell>
            <TableCell className="text-right tabular-nums">
              {p.hasMarketPrice ? <MaskedValue>{brl.format(p.currentPrice)}</MaskedValue> : '—'}
            </TableCell>
            <TableCell className="text-right tabular-nums">
              <MaskedValue>{brl.format(p.currentValue)}</MaskedValue>
            </TableCell>
            {/* Peso = composição % — permanece visível */}
            <TableCell className="text-right tabular-nums text-muted-foreground">
              {totalValue > 0 ? `${((p.currentValue / totalValue) * 100).toFixed(1)}%` : '—'}
            </TableCell>
            <TableCell className="text-right tabular-nums text-muted-foreground">
              <MaskedValue>
                {p.contributionPercent !== null && p.contributionPercent !== undefined
                  ? `${p.contributionPercent.toFixed(1)}%`
                  : '—'}
              </MaskedValue>
            </TableCell>
            <TableCell
              className={`text-right tabular-nums ${
                p.unrealizedPnl >= 0 ? 'text-emerald-600' : 'text-red-600'
              }`}
            >
              <MaskedValue>{brl.format(p.unrealizedPnl)}</MaskedValue>
              {!p.hasMarketPrice ? (
                <span className="block text-[10px] text-muted-foreground">
                  Sem cotação: valor estimado pelo custo
                </span>
              ) : null}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
