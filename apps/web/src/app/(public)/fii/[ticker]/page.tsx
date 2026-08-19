import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { findCatalogAsset, MOCK_FIIS, type FiiAsset } from '@/lib/mock-catalog';
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
  Coins,
  Scale,
  Sparkles,
} from 'lucide-react';

const integerFormatterBR = new Intl.NumberFormat('pt-BR');

interface PageProps {
  params: Promise<{ ticker: string }>;
}

async function getFiiOrFallback(ticker: string): Promise<FiiAsset> {
  const found = findCatalogAsset(ticker);
  if (found && found.category === 'FII') {
    return found;
  }
  const fallback = MOCK_FIIS.find((f) => f.ticker.toLowerCase() === ticker.toLowerCase());
  if (fallback) return fallback;

  // Generic fallback if not in mock list
  return {
    ticker: ticker.toUpperCase(),
    name: `${ticker.toUpperCase()} Fundo de Investimento Imobiliário`,
    manager: 'Gestora Especializada B3',
    category: 'FII',
    subCategory: 'Tijolo / Renda',
    segment: 'Imóveis Comerciais',
    dividendYield12m: 9.5,
    pvp: 0.99,
    lastDividend: 0.85,
    netAssets: 2500000000,
    shareholders: 150000,
    lastPrice: 100.0,
    changeDayPercent: 0.1,
    changeYtdPercent: 6.5,
  };
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { ticker } = await params;
  const fii = await getFiiOrFallback(ticker);

  return {
    title: `${fii.ticker} — Lâmina CVM, Dividend Yield e Tributação do FII`,
    description: `Análise completa do FII ${fii.ticker} (${fii.name}): Dividend Yield 12M de ${formatPercent(fii.dividendYield12m)}, P/VP de ${fii.pvp.toFixed(2)}x, último provento de ${formatCurrencyBRL(fii.lastDividend)} e regras de isenção de proventos PF.`,
    openGraph: {
      title: `${fii.ticker} | IndexDesk FIIs`,
      description: `Lâmina e inteligência para o Fundo Imobiliário ${fii.ticker} (${fii.name})`,
    },
  };
}

export default async function FiiDetailPage({ params }: PageProps) {
  const { ticker } = await params;
  if (!ticker) notFound();

  const fii = await getFiiOrFallback(ticker);
  const isPositiveDay = fii.changeDayPercent >= 0;

  // FinancialProduct Structured Data for Google SEO
  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'FinancialProduct',
    name: fii.name,
    identifier: fii.ticker,
    category: 'Fundo de Investimento Imobiliário (FII)',
    provider: {
      '@type': 'Organization',
      name: fii.manager,
    },
    description: `Fundo Imobiliário ${fii.name} do segmento ${fii.segment} negociado na B3 sob o ticker ${fii.ticker}.`,
  };

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, '\\u003c') }}
      />

      {/* Navigation breadcrumb */}
      <div>
        <Link href="/ativos?tab=FII">
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
              {fii.ticker}
            </h1>
            <Badge variant="secondary" className="text-xs font-semibold">
              FII
            </Badge>
            <Badge variant="outline" className="text-xs">
              Segmento: {fii.segment}
            </Badge>
          </div>
          <p className="text-muted-foreground text-sm font-medium">{fii.name}</p>
        </div>

        <div className="flex items-center gap-4">
          <div className="flex flex-col items-end">
            <span className="text-2xl font-bold font-mono text-foreground">
              {formatCurrencyBRL(fii.lastPrice)}
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
              {formatPercent(fii.changeDayPercent)} (Hoje)
            </span>
          </div>
          <a
            href={`https://br.tradingview.com/chart/?symbol=BMFBOVESPA%3A${fii.ticker}`}
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

      {/* Fiscal & Tax Breakdown for FIIs (Crucial B3 Rule) */}
      <Card variant="default" className="border-primary/30 bg-primary/5">
        <CardHeader className="pb-3">
          <CardTitle className="text-sm font-bold text-foreground flex items-center gap-2">
            <ShieldAlert className="size-4 text-primary" />
            Guia Fiscal do Investidor: Regras de Tributação e Proventos de FIIs
          </CardTitle>
          <CardDescription className="text-xs text-muted-foreground">
            Entenda como funciona a isenção dos rendimentos mensais e a tributação sobre o ganho de
            capital em alienação de cotas na B3.
          </CardDescription>
        </CardHeader>
        <CardContent className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs text-muted-foreground">
          <div className="flex flex-col gap-1 p-3 rounded-lg bg-card border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Sparkles className="size-3.5 text-positive" />
              Proventos Mensais Isentos (PF)
            </span>
            <span>
              Os dividendos distribuídos por FIIs são{' '}
              <strong>100% isentos de Imposto de Renda</strong> para pessoas físicas, desde que o
              fundo possua mais de 100 cotistas e o investidor detenha menos de 10% das cotas (Lei
              11.033/04).
            </span>
          </div>

          <div className="flex flex-col gap-1 p-3 rounded-lg bg-card border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Receipt className="size-3.5 text-warning" />
              20% de IR no Ganho de Capital
            </span>
            <span>
              Ao vender cotas com lucro, a alíquota é de <strong>20% sobre o ganho líquido</strong>{' '}
              (tanto em Swing Trade quanto Day Trade). Não há isenção de R$ 20 mil/mês. Recolhimento
              via DARF (código 6015).
            </span>
          </div>

          <div className="flex flex-col gap-1 p-3 rounded-lg bg-card border">
            <span className="font-semibold text-foreground flex items-center gap-1.5">
              <Info className="size-3.5 text-primary" />
              Sem Come-Cotas
            </span>
            <span>
              Como os FIIs são estruturados como condomínios fechados negociados em bolsa, não há
              cobrança periódica semestral de come-cotas (ao contrário dos fundos abertos de banco).
            </span>
          </div>
        </CardContent>
      </Card>

      {/* Metrics Summary Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* 1. Dividend Yield 12M */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Coins className="size-3.5 text-primary" />
              Dividend Yield (12M)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-primary">
              {formatPercent(fii.dividendYield12m)}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Rendimentos acumulados nos últimos 12 meses.
            </p>
          </CardContent>
        </Card>

        {/* 2. P/VP */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Scale className="size-3.5 text-muted-foreground" />
              Preço / Valor Patrimonial (P/VP)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-baseline gap-2">
              <span className="text-2xl font-bold font-mono text-foreground">
                {fii.pvp.toFixed(2)}x
              </span>
              <Badge
                variant={fii.pvp < 1 ? 'default' : fii.pvp > 1.05 ? 'destructive' : 'secondary'}
                className="text-[10px] py-0 px-1 font-sans"
              >
                {fii.pvp < 1 ? 'Com Desconto' : fii.pvp > 1.05 ? 'Com Ágio' : 'Preço Justo'}
              </Badge>
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              {fii.pvp < 1
                ? `Cota negociada com ${((1 - fii.pvp) * 100).toFixed(1)}% de desconto sobre o VP.`
                : fii.pvp > 1
                  ? `Cota negociada com ${((fii.pvp - 1) * 100).toFixed(1)}% de ágio patrimonial.`
                  : 'Cota em linha com o valor patrimonial contábil.'}
            </p>
          </CardContent>
        </Card>

        {/* 3. Último Rendimento */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Receipt className="size-3.5 text-muted-foreground" />
              Último Rendimento
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-foreground">
              {formatCurrencyBRL(fii.lastDividend)} / cota
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Distribuído mensalmente diretamente em conta.
            </p>
          </CardContent>
        </Card>

        {/* 4. Patrimônio Líquido */}
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-xs text-muted-foreground font-semibold uppercase flex items-center gap-1.5">
              <Building2 className="size-3.5 text-muted-foreground" />
              Patrimônio Líquido (PL)
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold font-mono text-foreground">
              {formatCurrencyBRL(fii.netAssets)}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              {integerFormatterBR.format(fii.shareholders)} cotistas ativos.
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Actions and Navigation */}
      <div className="flex flex-wrap items-center gap-3">
        <Link href={`/comparador?ticker=${fii.ticker}`}>
          <Button variant="outline" size="sm" className="gap-1.5">
            <Layers className="size-3.5" />
            Comparar com outros Ativos
          </Button>
        </Link>
        <Link href="/ativos?tab=FII">
          <Button variant="secondary" size="sm" className="gap-1.5">
            <Building2 className="size-3.5" />
            Explorar todos os FIIs
          </Button>
        </Link>
      </div>
    </div>
  );
}
