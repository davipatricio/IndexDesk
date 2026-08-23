import { Info } from 'lucide-react';

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import type { AssetDividendsDto } from '@/lib/api-client';
import { cn, formatCurrencyBRL, formatPercent } from '@/lib/utils';

interface DividendSummaryProps {
  data: AssetDividendsDto;
  className?: string;
}

const eventDateFormatter = new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: 'UTC',
});

function formatDate(iso: string): string {
  return eventDateFormatter.format(new Date(`${iso}T00:00:00Z`));
}

/**
 * "Quanto o ativo pagou" panel — trailing-12-months cash per quote, yield and
 * the full local payment history. Server-rendered; hidden by the caller when
 * the asset has no events.
 */
export function DividendSummary({ data, className }: DividendSummaryProps) {
  const recent = [...data.events].reverse().slice(0, 8);
  const dyHelp =
    'Soma dos proventos por cota dos últimos 12 meses dividida pelo preço atual. Estimativa baseada no histórico pago — rendimento passado não garante retorno futuro.';

  const summary = [
    {
      label: 'Proventos (12 meses)',
      value: formatCurrencyBRL(data.last12mTotal),
      help: 'Total de dinheiro distribuído por cota nos últimos 12 meses, somando todos os pagamentos.',
    },
    {
      label: 'Dividend Yield (12m)',
      value:
        data.dividendYield12mPercent != null ? formatPercent(data.dividendYield12mPercent) : '—',
      help: dyHelp,
    },
    {
      label: 'Pagamentos registrados',
      value: String(data.totalCount),
      help: 'Quantidade de eventos de provento no histórico local do ativo. Novos pagamentos aparecem após cada sincronização diária.',
    },
  ];

  return (
    <section
      aria-label={`Proventos de ${data.ticker}`}
      className={cn('flex flex-col gap-3', className)}
    >
      <div className="flex flex-col gap-1">
        <h2 className="font-heading text-base font-semibold">Proventos</h2>
        <p className="text-xs text-muted-foreground">
          Rendimentos em dinheiro pagos por cota ao longo da história recente.
        </p>
      </div>

      <TooltipProvider>
        <dl className="grid grid-cols-1 divide-border overflow-hidden rounded-lg border bg-background/40 sm:grid-cols-3 sm:divide-x">
          {summary.map((item) => (
            <div key={item.label} className="flex flex-col gap-0.5 px-3 py-2">
              <dt className="flex items-center gap-1 text-[11px] font-medium text-muted-foreground">
                {item.label}
                <Tooltip>
                  <TooltipTrigger
                    type="button"
                    aria-label={`O que significa ${item.label}?`}
                    className="inline-flex text-muted-foreground/60 transition-colors hover:text-foreground"
                  >
                    <Info className="size-3" aria-hidden="true" />
                  </TooltipTrigger>
                  <TooltipContent className="max-w-60 text-xs leading-relaxed" sideOffset={6}>
                    {item.help}
                  </TooltipContent>
                </Tooltip>
              </dt>
              <dd className="font-mono text-sm font-semibold tabular-nums text-foreground">
                {item.value}
              </dd>
            </div>
          ))}
        </dl>
      </TooltipProvider>

      <details className="text-xs">
        <summary className="cursor-pointer select-none font-medium text-muted-foreground hover:text-foreground">
          Ver histórico de pagamentos ({data.totalCount} registros)
        </summary>
        <div className="mt-2 max-h-56 overflow-auto rounded-lg border">
          <table className="w-full text-left">
            <caption className="sr-only">Proventos pagos por cota</caption>
            <thead className="sticky top-0 bg-muted text-[11px] uppercase tracking-wide">
              <tr>
                <th scope="col" className="px-2 py-1.5 font-medium">
                  Data-com
                </th>
                <th scope="col" className="px-2 py-1.5 text-right font-medium">
                  Valor/cota
                </th>
                <th scope="col" className="px-2 py-1.5 font-medium">
                  Tipo
                </th>
              </tr>
            </thead>
            <tbody>
              {recent.map((event) => (
                <tr key={`${event.comDate}-${event.rate}`} className="border-t">
                  <td className="px-2 py-1.5 tabular-nums">{formatDate(event.comDate)}</td>
                  <td className="px-2 py-1.5 text-right font-mono tabular-nums text-foreground">
                    {formatCurrencyBRL(event.rate)}
                  </td>
                  <td className="px-2 py-1.5 text-muted-foreground">{event.type}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {data.events.length > recent.length ? (
            <p className="border-t px-2 py-1.5 text-[11px] text-muted-foreground">
              Exibindo os {recent.length} pagamentos mais recentes de {data.totalCount}.
            </p>
          ) : null}
        </div>
      </details>

      <p className="text-[11px] leading-relaxed text-muted-foreground">
        Valores por cota na data de pagamento. O histórico local começa na primeira coleta e pode
        ser menor que a vida real do fundo; confira o informe oficial para fins de imposto de renda.
      </p>
    </section>
  );
}
