import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { findCatalogAsset, MOCK_BDRS, type BdrAsset } from '@/lib/mock-catalog';
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
  TrendingDown,
  Building2,
  Receipt,
  Layers,
  Globe,
  Coins,
} from 'lucide-react';

const integerFormatterBR = new Intl.NumberFormat('pt-BR');

interface PageProps {
  params: Promise<{ ticker: string }>;
}

async function getBdrOrFallback(ticker: string): Promise<BdrAsset> {
  const found = findCatalogAsset(ticker);
  if (found && found.category === 'BDR') {
    return found;
  }
  const fallback = MOCK_BDRS.find((b) => b.ticker.toLowerCase() === ticker.toLowerCase());
  if (fallback) return fallback;

  // Generic fallback if not in mock list
  return {
    ticker: ticker.toUpperCase(),
    name: `${ticker.toUpperCase()} Brazilian Depositary Receipt`,
    manager: 'Instituição Depositária B3',
    category: 'BDR',
    subCategory: 'BDR Global',
    underlyingAsset: `${ticker.slice(0, 4)} (Global)`,
    country: 'Internacional',
    managementFee: 0.15,
    netAssets: 1000000000,
    shareholders: 25000,
    lastPrice: 50.0,
    changeDayPercent: 0.2,
    changeYtdPercent: 12.0,
  };
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { ticker } = await params;
  const bdr = await getBdrOrFallback(ticker);

  return {
    title: `${bdr.ticker} — Lâmina CVM, Cotação e Tributação do BDR`,
    description: `Análise completa do BDR ${bdr.ticker} (${bdr.name}): lastro no ativo ${bdr.underlyingAsset} (${bdr.country}), taxa base de ${formatPercent(bdr.managementFee)} a.a., patrimônio de ${formatCurrencyBRL(bdr.netAssets)} e regras de tributação sem isenção de 20k.`,
    openGraph: {
      title: `${bdr.ticker} | IndexDesk BDRs`,
      description: `Lâmina e inteligência para o BDR ${bdr.ticker} (${bdr.name})`,
    },
  };
}

export default async function BdrDetailPage({ params }: PageProps) {
  const { ticker } = await params;
  if (!ticker) notFound();

  const bdr = await getBdrOrFallback(ticker);
  const isPositiveDay = bdr.changeDayPercent >= 0;

  // FinancialProduct Structured Data for Google SEO
  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'FinancialProduct',
    name: bdr.name,
    identifier: bdr.ticker,
    category: 'Brazilian Depositary Receipt (BDR)',
    provider: {
      '@type': 'Organization',
      name: bdr.manager,
    },
    description: `Certificado de Depósito de Valores Mobiliários (BDR) lastreado em ${bdr.underlyingAsset}, negociado na B3 sob o ticker ${bdr.ticker}.`,
  };

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, '\\u003c') }}
      />

      {/* Navigation breadcrumb */}
      <div>
        <Link href="/ativos?tab=BDR">
          <Button variant="ghost" size="sm" className="gap-1.5 text-muted-foreground -ml-2">
            <ArrowLeft className="size-3.5" />
            Voltar ao Catálogo de Ativos
          </Button>
        </Link>
      </div>

      {/* Header Info */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-6">
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center gap-2.5">
            <h1 className="text-3xl font-bold tracking-tight font-mono text-foreground">
              {bdr.ticker}
            </h1>
            <Badge variant="secondary" className="text-xs font-semibold">
              BDR
            </Badge>
            <Badge variant="outline" className="text-xs">
              Origem: {bdr.country}
            </Badge>
          </div>
          <p className="text-muted-foreground text-sm font-medium">{bdr.name}</p>
        </div>

        <div className="flex items-center gap-4">
          <div className="flex flex-col items-end">
            <span className="text-2xl font-bold font-mono text-foreground">
              {formatCurrencyBRL(bdr.lastPrice)}
            </span>
            <span
              className={`text-xs font-mono font-semibold inline-flex items-center ${
                isPositiveDay ? 'text-positive' : 'text-negative'
              }`}
            >
              {isPositiveDay ? (
                <TrendingUp className="size-3 mr-1" />
              ) : (
                <TrendingDown className="size-3 mr-1" />
              )}
              {formatPercent(bdr.changeDayPercent)} (Hoje)
            </span>
          </div>
          <a
            href={`https://br.tradingview.com/chart/?symbol=BMFBOVESPA%3A${bdr.ticker}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button size="sm" className="gap-1.5">
              Gráfico TradingView
              <ExternalLink className="size-3.5" />
            </Button>
          </a>
        </div>
      </div>

      {/* Fiscal & Tax Breakdown Warning for BDRs */}
      <Card variant="warning">
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-bold text-warning flex items-center gap-2">
            <ShieldAlert className="size-4" />
            Atenção Tributária: Regra Fiscal e DARF para BDRs na B3
          </CardTitle>
          <CardDescription className="text-xs text-muted-foreground">
            BDRs de empresas e ETFs globais possuem regras tributárias específicas e não usufruem da
            isenção de R$ 20.000 para pessoas físicas.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs text-muted-foreground">
          <div className="flex flex-col gap-1 p-3 rounded-lg bg-background/80 border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Receipt className="size-3.5 text-warning" />
              Sem Isenção de R$ 20.000
            </span>
            <span>
              A isenção mensal de R$ 20k é restrita a ações brasileiras. Ganhos líquidos apurados em
              qualquer venda de BDRs são tributados a <strong>15% (Swing Trade)</strong> e{' '}
              <strong>20% (Day Trade)</strong> via DARF (código 6015).
            </span>
          </div>

          <div className="flex flex-col gap-1 p-3 rounded-lg bg-background/80 border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Coins className="size-3.5 text-warning" />
              Tributação de Proventos Internacionais
            </span>
            <span>
              Dividendos pagos pelo ativo no exterior sofrem retenção na fonte no país emissor (ex:
              30% nos EUA). O saldo líquido recebido no Brasil é apurado via Carnê-Leão / IRPF
              conforme acordos de bitributação.
            </span>
          </div>

          <div className="flex flex-col gap-1 p-3 rounded-lg bg-background/80 border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Info className="size-3.5 text-warning" />
              Sem Come-Cotas
            </span>
            <span>
              BDRs são certificados de depósito de valores mobiliários e não fundos abertos.
              Portanto, não há cobrança semestral antecipada de come-cotas.
            </span>
          </div>
        </CardContent>
      </Card>

      {/* Metrics Summary Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* 1. Ativo Subjacente */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Globe className="size-3.5 text-primary" />
              Ativo Subjacente (Lastro)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-primary truncate">
              {bdr.underlyingAsset}
            </div>
            <p className="text-xs text-muted-foreground mt-1">Origem geográfica: {bdr.country}.</p>
          </CardContent>
        </Card>

        {/* 2. Taxa de Administração Base */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Receipt className="size-3.5 text-muted-foreground" />
              Taxa ETF Base / Gestão
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-foreground">
              {bdr.managementFee > 0 ? `${formatPercent(bdr.managementFee)} a.a.` : '—'}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Taxa de administração do ETF base no exterior.
            </p>
          </CardContent>
        </Card>

        {/* 3. Patrimônio Líquido */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Building2 className="size-3.5 text-muted-foreground" />
              Patrimônio em BDRs (B3)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-foreground">
              {formatCurrencyBRL(bdr.netAssets)}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Emitido sob custódia de {bdr.manager}.
            </p>
          </CardContent>
        </Card>

        {/* 4. Número de Cotistas */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Building2 className="size-3.5 text-muted-foreground" />
              Investidores na B3
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-foreground">
              {integerFormatterBR.format(bdr.shareholders)}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Detentores do certificado no Brasil.
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Actions and Navigation */}
      <div className="flex flex-wrap items-center gap-3">
        <Link href={`/comparador?ticker=${bdr.ticker}`}>
          <Button variant="outline" size="sm" className="gap-1.5">
            <Layers className="size-3.5" />
            Comparar com outros Ativos
          </Button>
        </Link>
        <Link href="/ativos?tab=BDR">
          <Button variant="secondary" size="sm" className="gap-1.5">
            <Globe className="size-3.5" />
            Explorar todos os BDRs
          </Button>
        </Link>
      </div>
    </div>
  );
}
