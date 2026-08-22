import type { ReactNode } from 'react';

import {
  AlertTriangle,
  Building2,
  CircleDollarSign,
  Globe2,
  ReceiptText,
  ShieldCheck,
} from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';

type AssetTaxType = 'ETF' | 'BDR';

interface FiscalTaxCardProps {
  /** Changes the heading and context for B3 ETFs versus BDRs. */
  assetType?: AssetTaxType;
  /** Highlights the Ireland-domiciled UCITS rule when the asset is an international ETF. */
  isIrelandUcits?: boolean;
  className?: string;
}

interface TaxRuleProps {
  icon: typeof ReceiptText;
  title: string;
  children: ReactNode;
  tone?: 'warning' | 'default';
}

function TaxRule({ icon: Icon, title, children, tone = 'default' }: TaxRuleProps) {
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

export function FiscalTaxCard({
  assetType = 'ETF',
  isIrelandUcits = false,
  className,
}: FiscalTaxCardProps) {
  const assetLabel = assetType === 'BDR' ? 'BDRs' : 'ETFs';

  return (
    <Card variant="warning" className={cn('gap-4', className)}>
      <CardHeader className="gap-2">
        <CardTitle className="flex items-center gap-2 text-sm font-bold text-warning">
          <AlertTriangle className="size-4" aria-hidden="true" />
          Atenção tributária: {assetLabel} na B3
        </CardTitle>
        <CardDescription className="text-xs leading-relaxed">
          Este resumo é informativo e não substitui orientação de um profissional tributário. A
          apuração deve considerar preço médio, custos e todas as operações do mês.
        </CardDescription>
      </CardHeader>

      <CardContent className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <TaxRule icon={CircleDollarSign} title="15% no Swing Trade">
          Ganhos líquidos em operações comuns (Swing Trade) são tributados à alíquota de{' '}
          <strong className="font-semibold text-foreground">15%</strong>.
        </TaxRule>

        <TaxRule icon={Building2} title="20% no Day Trade">
          Operações iniciadas e encerradas no mesmo pregão têm alíquota de{' '}
          <strong className="font-semibold text-foreground">20%</strong> sobre o ganho líquido.
        </TaxRule>

        <TaxRule icon={ReceiptText} title="Sem isenção de R$ 20 mil" tone="warning">
          A isenção mensal para vendas de ações não se aplica a {assetLabel.toLowerCase()}. Qualquer
          lucro líquido tributável deve ser apurado, mesmo com vendas abaixo de R$ 20.000 no mês.
        </TaxRule>

        <TaxRule icon={ReceiptText} title="DARF código 6015">
          O imposto devido deve ser recolhido via DARF até o último dia útil do mês seguinte ao da
          operação, usando o código <strong className="font-semibold text-foreground">6015</strong>.
        </TaxRule>

        <TaxRule icon={ShieldCheck} title="Sem Come-Cotas">
          {assetLabel} negociados em bolsa não sofrem a antecipação semestral de IR conhecida como
          Come-Cotas. O evento tributável ocorre conforme a regra aplicável à operação.
        </TaxRule>

        <TaxRule icon={Globe2} title="Rendimentos no exterior">
          {isIrelandUcits ? (
            <>
              Em ETFs UCITS domiciliados na Irlanda, dividendos recebidos dos EUA normalmente têm
              retenção de <strong className="font-semibold text-foreground">15%</strong> na fonte.
              Em uma classe <strong className="font-semibold text-foreground">acumuladora</strong>,
              o dividendo é reinvestido no fundo, sem distribuição periódica ao cotista.
            </>
          ) : (
            <>
              Para ETFs globais, confirme o domicílio e a política de distribuição: a retenção
              estrangeira pode ser de <strong className="font-semibold text-foreground">15%</strong>{' '}
              (Irlanda) ou <strong className="font-semibold text-foreground">30%</strong> (EUA),
              conforme a estrutura do veículo.
            </>
          )}
        </TaxRule>
      </CardContent>
    </Card>
  );
}

export type { AssetTaxType, FiscalTaxCardProps };
