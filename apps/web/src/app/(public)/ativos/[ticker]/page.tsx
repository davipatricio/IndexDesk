import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { ArrowLeft, ExternalLink, Layers, LineChart, Trophy } from 'lucide-react';

import { DividendSummary } from '@/components/assets/dividend-summary';
import { FiiTaxCard } from '@/components/assets/fii-tax-card';
import { FiscalTaxCard } from '@/components/assets/fiscal-tax-card';
import { InvestmentSimulation } from '@/components/assets/investment-simulation';
import { PriceHeroChart } from '@/components/charts/price-hero-chart';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  fetchAssetDetail,
  fetchAssetDividends,
  fetchAssetQuotes,
  type AssetDetailDto,
  type AssetDividendsDto,
  type QuoteItem,
} from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { Info } from 'lucide-react';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

// Blocking route: params and revalidated fetches resolve outside <Suspense>.
// Still renders full HTML per request, so SEO output is unchanged.
export const instant = false;

type PageProps = { params: Promise<{ ticker: string }> };

const numberFormatter = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 });
const compactBRLFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  notation: 'compact',
  maximumFractionDigits: 1,
});

function assetClassLabel(assetType: string): string {
  const normalized = assetType.toUpperCase();
  if (normalized === 'FII') return 'FII';
  if (normalized.startsWith('BDR')) return 'BDR de ETF';
  if (normalized === 'INDEX') return 'Índice';
  return 'ETF';
}

async function getAssetData(ticker: string): Promise<{
  detail: AssetDetailDto;
  quotes: QuoteItem[];
  dividends: AssetDividendsDto | null;
}> {
  try {
    const detail = await fetchAssetDetail(ticker);
    let quotes: QuoteItem[] = [];
    let dividends: AssetDividendsDto | null = null;

    const quotesTask = fetchAssetQuotes(ticker, { days: 7300 }).catch(() => []);
    const dividendsTask = fetchAssetDividends(ticker).catch(() => null);
    [quotes, dividends] = await Promise.all([quotesTask, dividendsTask]);

    return { detail, quotes, dividends };
  } catch {
    notFound();
  }
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { ticker } = await params;
  if (!ticker?.trim()) notFound();

  try {
    const detail = await fetchAssetDetail(ticker);
    return {
      title: `${detail.ticker} — Cotação, simulação e tributação | IndexDesk`,
      description: `Análise de ${detail.ticker} (${detail.name}) com histórico de preços, simulação de investimento e regras fiscais na B3.`,
      openGraph: {
        title: `${detail.ticker} | IndexDesk B3`,
        description: `Cotação, indexadores e inteligência fiscal para ${detail.name}.`,
      },
    };
  } catch {
    return {
      title: `${ticker.toUpperCase()} | IndexDesk`,
      description: `Análise de ${ticker.toUpperCase()} na B3.`,
    };
  }
}

interface InlineStat {
  label: string;
  value: string;
  tone?: 'positive' | 'negative' | 'neutral';
  /** Educational explanation surfaced through a help tooltip. */
  help?: string;
}

export default async function AssetDetailPage({ params }: PageProps) {
  const { ticker } = await params;
  if (!ticker?.trim()) notFound();

  const { detail, quotes, dividends } = await getAssetData(ticker);
  const stats = detail.stats;
  const positiveDay = (stats.changeDayPercent ?? 0) >= 0;
  const classLabel = assetClassLabel(detail.assetType);
  const isFii = detail.fiscal?.isFii || detail.assetType.toUpperCase() === 'FII';
  const tradingViewSymbol = detail.tradingViewSymbol || `BMFBOVESPA:${detail.ticker}`;

  const inlineStats: InlineStat[] = [
    {
      label: 'Retorno 12 meses',
      value:
        stats.return12mPercent != null
          ? formatPercent(stats.return12mPercent)
          : stats.returnYtdPercent != null
            ? `${formatPercent(stats.returnYtdPercent)} (ano)`
            : '—',
      tone: (stats.return12mPercent ?? stats.returnYtdPercent ?? 0) >= 0 ? 'positive' : 'negative',
      help: 'Quanto o preço valorizou ou caiu nos últimos 12 meses. Considera só a variação de preço, sem proventos.',
    },
    {
      label: 'Volatilidade anual',
      value:
        stats.annualizedVolatilityPercent != null
          ? formatPercent(stats.annualizedVolatilityPercent)
          : '—',
      tone: 'neutral',
      help: 'O quanto o preço costuma oscilar em um ano (desvio-padrão dos retornos diários). Quanto maior, mais imprevisível o ativo no curto prazo.',
    },
    {
      label: 'Sharpe',
      value: stats.sharpeRatio != null ? numberFormatter.format(stats.sharpeRatio) : '—',
      tone: 'neutral',
      help: 'Retorno acima do CDI para cada unidade de risco assumida. Acima de 1 é considerado bom; negativo significa que rendeu menos que o CDI.',
    },
    {
      label: 'Drawdown máximo',
      value: stats.maxDrawdownPercent != null ? formatPercent(stats.maxDrawdownPercent) : '—',
      tone: stats.maxDrawdownPercent != null ? 'negative' : 'neutral',
      help: 'A maior queda do histórico, medida do topo até o fundo antes de recuperar. Mostra a pior perda de quem comprou no pico.',
    },
    {
      label: 'Volume médio/dia',
      value: stats.avgVolume30D != null ? compactBRLFormatter.format(stats.avgVolume30D) : '—',
      tone: 'neutral',
      help: 'Valor financeiro médio negociado por dia nas últimas ~30 sessões. Proxy de liquidez: quanto maior, mais fácil comprar e vender sem afetar o preço.',
    },
  ];

  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'FinancialProduct',
    name: detail.name,
    identifier: detail.ticker,
    category: isFii ? 'Real Estate Fund' : 'Exchange Traded Fund',
  };

  const closes = quotes.map((quote) => ({ date: quote.date, value: quote.close }));

  return (
    <main className="container mx-auto flex flex-col gap-6 px-4 py-8">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, '\\u003c') }}
      />

      {/* C1 — dense quote header */}
      <header className="flex flex-col gap-4 border-b pb-5">
        <Link href="/ativos" className="w-fit">
          <Button variant="ghost" size="sm" className="-ml-2 gap-1.5 text-muted-foreground">
            <ArrowLeft className="size-3.5" />
            Voltar ao catálogo
          </Button>
        </Link>
        <div className="flex flex-col justify-between gap-4 lg:flex-row lg:items-end">
          <div className="flex flex-col gap-1.5">
            <div className="flex flex-wrap items-center gap-2.5">
              <h1 className="font-mono text-3xl font-bold tracking-tight">{detail.ticker}</h1>
              <Badge variant="secondary">{classLabel}</Badge>
              <Badge variant="outline">{detail.currency}</Badge>
              {detail.cnpj ? (
                <span className="font-mono text-xs text-muted-foreground">CNPJ {detail.cnpj}</span>
              ) : null}
            </div>
            <p className="text-sm font-medium text-muted-foreground">{detail.name}</p>
          </div>

          <div className="flex items-end justify-between gap-6 lg:justify-end">
            <div className="text-right">
              <p className="font-mono text-3xl font-bold tabular-nums tracking-tight">
                {stats.lastPrice != null ? formatCurrencyBRL(stats.lastPrice) : '—'}
              </p>
              {stats.changeDayPercent != null ? (
                <p
                  className={`text-xs font-semibold tabular-nums ${
                    positiveDay ? 'text-positive' : 'text-negative'
                  }`}
                >
                  {formatPercent(stats.changeDayPercent)} hoje
                </p>
              ) : null}
            </div>
            <a
              href={`https://br.tradingview.com/chart/?symbol=${encodeURIComponent(tradingViewSymbol)}`}
              target="_blank"
              rel="noopener noreferrer"
            >
              <Button size="sm" variant="outline" className="gap-1.5">
                TradingView <ExternalLink className="size-3.5" />
              </Button>
            </a>
          </div>
        </div>

        <TooltipProvider>
          <dl className="grid grid-cols-2 divide-border overflow-hidden rounded-lg border bg-background/40 sm:grid-cols-3 sm:divide-x lg:grid-cols-5">
            {inlineStats.map((stat) => (
              <div key={stat.label} className="flex flex-col gap-0.5 px-3 py-2">
                <dt className="flex items-center gap-1 text-[11px] font-medium text-muted-foreground">
                  {stat.label}
                  {stat.help ? (
                    <Tooltip>
                      <TooltipTrigger
                        type="button"
                        aria-label={`O que significa ${stat.label}?`}
                        className="inline-flex text-muted-foreground/60 transition-colors hover:text-foreground"
                      >
                        <Info className="size-3" aria-hidden="true" />
                      </TooltipTrigger>
                      <TooltipContent className="max-w-56 text-xs leading-relaxed" sideOffset={6}>
                        {stat.help}
                      </TooltipContent>
                    </Tooltip>
                  ) : null}
                </dt>
                <dd
                  className={`font-mono text-sm font-semibold tabular-nums ${
                    stat.tone === 'positive'
                      ? 'text-positive'
                      : stat.tone === 'negative'
                        ? 'text-negative'
                        : 'text-foreground'
                  }`}
                >
                  {stat.value}
                </dd>
              </div>
            ))}
          </dl>
        </TooltipProvider>
      </header>

      {/* C2 — chart hero with period in the URL */}
      <PriceHeroChart
        ticker={detail.ticker}
        quotes={quotes.map((quote) => ({
          date: quote.date,
          close: quote.close,
          volume: quote.volume,
        }))}
      />

      {/* Quick "what if" simulation against benchmarks */}
      <InvestmentSimulation ticker={detail.ticker} closes={closes} assetClass={detail.assetType} />

      {/* Cash payouts — hidden entirely when the asset never paid locally */}
      {dividends && dividends.events.length > 0 ? <DividendSummary data={dividends} /> : null}

      {/* C3 — fiscal panel (class-specific) */}
      {isFii ? (
        <FiiTaxCard />
      ) : detail.fiscal ? (
        <FiscalTaxCard
          assetType={detail.assetType.toUpperCase().includes('BDR') ? 'BDR' : 'ETF'}
          isIrelandUcits={detail.fiscal.taxDomicile === 'IRELAND_UCITS'}
        />
      ) : null}

      {/* C4 — cross-links + registry data */}
      <footer className="flex flex-col gap-4 border-t pt-5">
        <div className="flex flex-wrap gap-2">
          <Link href={`/comparador?ticker=${detail.ticker}`}>
            <Button variant="outline" size="sm" className="gap-1.5">
              <Layers className="size-3.5" />
              Comparar com outros ativos
            </Button>
          </Link>
          <Link href={`/ferramentas/backtest?ticker=${detail.ticker}`}>
            <Button variant="outline" size="sm" className="gap-1.5">
              <LineChart className="size-3.5" />
              Abrir no simulador de backtest
            </Button>
          </Link>
          <Link href="/rankings">
            <Button variant="outline" size="sm" className="gap-1.5">
              <Trophy className="size-3.5" />
              Ver rankings
            </Button>
          </Link>
        </div>

        <dl className="flex flex-wrap gap-x-8 gap-y-2 text-xs text-muted-foreground">
          <div className="flex gap-1.5">
            <dt>Primeira cotação:</dt>
            <dd className="font-mono tabular-nums text-foreground">
              {stats.firstQuoteDate
                ? new Date(`${stats.firstQuoteDate}T00:00:00Z`).toLocaleDateString('pt-BR')
                : '—'}
            </dd>
          </div>
          <div className="flex gap-1.5">
            <dt>Última cotação:</dt>
            <dd className="font-mono tabular-nums text-foreground">
              {stats.lastQuoteDate
                ? new Date(`${stats.lastQuoteDate}T00:00:00Z`).toLocaleDateString('pt-BR')
                : '—'}
            </dd>
          </div>
          <div className="flex gap-1.5">
            <dt>Código ISIN:</dt>
            <dd className="font-mono tabular-nums text-foreground">{detail.isin || '—'}</dd>
          </div>
        </dl>
      </footer>
    </main>
  );
}
