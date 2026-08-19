import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { fetchAssetByTicker } from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  ShieldAlert,
  ArrowLeft,
  ExternalLink,
  Info,
  TrendingUp,
  Building2,
  Receipt,
  Layers,
} from 'lucide-react';

const integerFormatterBR = new Intl.NumberFormat('pt-BR');

interface PageProps {
  params: Promise<{ ticker: string }>;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { ticker } = await params;
  const asset = await fetchAssetByTicker(ticker);

  return {
    title: `${asset.ticker} — Lâmina CVM, Cotação e Tributação`,
    description: `Análise completa do ETF ${asset.ticker} (${asset.name}): taxa de administração de ${formatPercent(asset.managementFee)} a.a., patrimônio de ${formatCurrencyBRL(asset.netAssets)} e regras fiscais DARF sem isenção de 20k.`,
    openGraph: {
      title: `${asset.ticker} | IndexDesk B3`,
      description: `Lâmina e inteligência para o ETF ${asset.ticker} (${asset.name})`,
    },
  };
}

export default async function EtfDetailPage({ params }: PageProps) {
  const { ticker } = await params;
  if (!ticker) notFound();

  const asset = await fetchAssetByTicker(ticker);

  // FinancialProduct Structured Data for Google SEO
  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'FinancialProduct',
    name: asset.name,
    identifier: asset.ticker,
    category: asset.category,
    provider: {
      '@type': 'Organization',
      name: asset.manager,
    },
    feesAndCommissionsSpecification: `${asset.managementFee}% ao ano`,
    description: `Fundo de Índice ${asset.name} negociado na B3 sob o ticker ${asset.ticker}.`,
  };

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />

      {/* Navigation breadcrumb */}
      <div>
        <Link href="/">
          <Button variant="ghost" size="sm" className="gap-1.5 text-xs text-muted-foreground -ml-2">
            <ArrowLeft className="size-3.5" />
            Voltar ao Catálogo
          </Button>
        </Link>
      </div>

      {/* Header Info */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-6">
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center gap-2.5">
            <h1 className="text-3xl font-extrabold tracking-tight font-mono">{asset.ticker}</h1>
            <Badge variant="secondary" className="text-xs font-semibold">
              {asset.category}
            </Badge>
            <Badge variant="outline" className="text-xs">
              Classe: {asset.assetClass}
            </Badge>
          </div>
          <p className="text-muted-foreground text-sm font-medium">{asset.name}</p>
        </div>

        <div className="flex items-center gap-4">
          <div className="flex flex-col items-end">
            <span className="text-2xl font-extrabold font-mono">
              {formatCurrencyBRL(asset.lastPrice)}
            </span>
            <span
              className={`text-xs font-mono font-semibold inline-flex items-center ${
                asset.changeDayPercent >= 0 ? 'text-emerald-500' : 'text-rose-500'
              }`}
            >
              <TrendingUp className="size-3 mr-1" />
              {formatPercent(asset.changeDayPercent)} (Hoje)
            </span>
          </div>
          <a
            href={`https://br.tradingview.com/chart/?symbol=BMFBOVESPA%3A${asset.ticker}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button size="sm" className="gap-1.5 text-xs">
              Gráfico TradingView
              <ExternalLink className="size-3.5" />
            </Button>
          </a>
        </div>
      </div>

      {/* Fiscal & Tax Breakdown Warning (Crucial B3 Rule) */}
      <Card className="border-amber-500/30 bg-amber-500/5">
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-bold text-amber-500 flex items-center gap-2">
            <ShieldAlert className="size-4" />
            Atenção Tributária: Regra Fiscal e DARF para ETFs na B3
          </CardTitle>
          <CardDescription className="text-xs text-muted-foreground">
            ETFs de Renda Variável e Renda Fixa possuem regras fiscais específicas e distintas de
            ações individuais.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs text-muted-foreground">
          <div className="flex flex-col gap-1 p-3 rounded bg-background/60 border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Receipt className="size-3.5 text-amber-500" />
              Sem Isenção de R$ 20.000
            </span>
            <span>
              Não existe a faixa de isenção de vendas até R$ 20k/mês. Qualquer lucro líquido apurado
              na venda de cotas deve recolher IR via DARF (código 6015).
            </span>
          </div>
          <div className="flex flex-col gap-1 p-3 rounded bg-background/60 border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Building2 className="size-3.5 text-amber-500" />
              Alíquotas de Swing / Day Trade
            </span>
            <span>
              <strong>15%</strong> sobre os ganhos líquidos em operações de Swing Trade e{' '}
              <strong>20%</strong> em operações de Day Trade.
            </span>
          </div>
          <div className="flex flex-col gap-1 p-3 rounded bg-background/60 border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Info className="size-3.5 text-amber-500" />
              Sem Come-Cotas
            </span>
            <span>
              Diferente dos fundos de investimento tradicionais de condomínio aberto, ETFs
              negociados em bolsa não sofrem a antecipação semestral de come-cotas.
            </span>
          </div>
        </CardContent>
      </Card>

      {/* Metrics Summary Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase">
              Taxa de Administração
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono">
              {formatPercent(asset.managementFee)} a.a.
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Cobrada proporcionalmente e deduzida do valor da cota.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase">
              Patrimônio Líquido (PL)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono">{formatCurrencyBRL(asset.netAssets)}</div>
            <p className="text-xs text-muted-foreground mt-1">Lâmina diária oficial CVM.</p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase">
              Número de Cotistas
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono">
              {integerFormatterBR.format(asset.shareholders)}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Investidores pessoa física e institucional.
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase">
              Índice de Referência (Benchmark)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-emerald-400">{asset.benchmark}</div>
            <p className="text-xs text-muted-foreground mt-1">Estratégia de replicação passiva.</p>
          </CardContent>
        </Card>
      </div>

      {/* Actions and Next Steps */}
      <div className="flex flex-wrap items-center gap-3">
        <Link href={`/comparador?ticker=${asset.ticker}`}>
          <Button variant="outline" size="sm" className="gap-1.5 text-xs">
            <Layers className="size-3.5" />
            Adicionar ao Comparador
          </Button>
        </Link>
        <Link href={`/ferramentas/backtest?ticker=${asset.ticker}`}>
          <Button variant="outline" size="sm" className="gap-1.5 text-xs">
            <TrendingUp className="size-3.5" />
            Simular no Backtest
          </Button>
        </Link>
      </div>
    </div>
  );
}
