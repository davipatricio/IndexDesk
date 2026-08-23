import {
  AlertTriangle,
  CalendarClock,
  CircleDollarSign,
  Percent,
  PiggyBank,
  ReceiptText,
} from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';

interface FiiTaxCardProps {
  className?: string;
}

function TaxRule({
  icon: Icon,
  title,
  children,
  tone = 'default',
}: {
  icon: typeof ReceiptText;
  title: string;
  children: React.ReactNode;
  tone?: 'warning' | 'default';
}) {
  return (
    <div
      className={cn(
        'flex flex-col gap-1.5 rounded-lg border p-3',
        tone === 'warning' ? 'border-warning/30 bg-warning/5' : 'bg-background/60',
      )}
    >
      <h3 className="flex items-center gap-1.5 text-xs font-semibold text-foreground">
        <Icon className={cn('size-3.5', tone === 'warning' ? 'text-warning' : 'text-primary')} />
        {title}
      </h3>
      <p className="text-xs leading-relaxed text-muted-foreground">{children}</p>
    </div>
  );
}

/**
 * Educational tax panel for FIIs — rules differ from ETFs/BDRs (exempt monthly
 * income for individuals, capital-gains sale at 20%, DARF 8968, no come-cotas).
 */
export function FiiTaxCard({ className }: FiiTaxCardProps) {
  return (
    <Card variant="warning" className={cn('gap-4', className)}>
      <CardHeader className="gap-2">
        <CardTitle className="flex items-center gap-2 text-sm font-bold text-warning">
          <AlertTriangle className="size-4" aria-hidden="true" />
          Atenção tributária: FIIs na B3
        </CardTitle>
        <CardDescription className="text-xs leading-relaxed">
          Este resumo é informativo e não substitui orientação de um profissional tributário. Regras
          para pessoa física investidora de fundos imobiliários listados em bolsa.
        </CardDescription>
      </CardHeader>

      <CardContent className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <TaxRule icon={CircleDollarSign} title="Rendimentos mensais isentos">
          Para pessoa física, os rendimentos mensais distribuídos por FIIs são{' '}
          <strong className="font-semibold text-foreground">isentos de IR</strong> (Lei 8.668/93),
          desde que o fundo tenha cotas negociadas em bolsa e ao menos 50 cotistas. Devem ser
          declarados na ficha de rendimentos isentos e não tributáveis.
        </TaxRule>

        <TaxRule icon={Percent} title="Venda tributada em 20%">
          O ganho de capital na venda de cotas é tributado à alíquota de{' '}
          <strong className="font-semibold text-foreground">20%</strong> para pessoa física, no
          regime normal de apuração mensal.
        </TaxRule>

        <TaxRule icon={ReceiptText} title="Sem isenção de valor mínimo" tone="warning">
          Não existe isenção para vendas pequenas: qualquer ganho líquido é tributável, mesmo abaixo
          de R$ 35 mil no mês. A regra de isenção de alienações de pequeno valor não se aplica a
          cotas de FIIs.
        </TaxRule>

        <TaxRule icon={CalendarClock} title="DARF código 8968">
          O imposto sobre o ganho na venda é recolhido via{' '}
          <strong className="font-semibold text-foreground">DARF 8968</strong> até o último dia útil
          do mês seguinte à operação que gerou o lucro.
        </TaxRule>

        <TaxRule icon={PiggyBank} title="Sem Come-Cotas">
          FIIs não sofrem a antecipação semestral de IR conhecida como Come-Cotas. O imposto incide
          apenas quando há venda de cotas com ganho.
        </TaxRule>

        <TaxRule icon={AlertTriangle} title="Cuidado com o preço médio" tone="warning">
          Amortizações de capital reduzem o preço médio de aquisição em vez de entrar como
          rendimento isento. Ignorar isso infla o ganho de capital calculado na venda — confira o
          informe do administrador antes de apurar.
        </TaxRule>
      </CardContent>
    </Card>
  );
}
