import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import {
  ArrowLeft,
  ExternalLink,
  Layers,
  ReceiptText,
  TrendingDown,
  TrendingUp,
} from 'lucide-react';

import { FiscalTaxCard } from '@/components/assets/fiscal-tax-card';
import { PriceHistoryChart } from '@/components/charts/price-history-chart';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  fetchAssetDetail,
  fetchAssetQuotes,
  type AssetDetailDto,
  type QuoteItem,
} from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';

type PageProps = { params: Promise<{ ticker: string }> };

const numberFormatter = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 });

function isBdr(assetType: string): boolean {
  return assetType.toUpperCase().includes('BDR');
}

async function getAssetData(
  ticker: string,
): Promise<{ detail: AssetDetailDto; quotes: QuoteItem[] }> {
  try {
    const detail = await fetchAssetDetail(ticker);
    let quotes: QuoteItem[] = [];
    try {
      quotes = await fetchAssetQuotes(ticker, { days: 365 });
    } catch {
      quotes = [];
    }
    return { detail, quotes };
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
      title: `${detail.ticker} — Lâmina, cotação e tributação | IndexDesk`,
      description: `Análise de ${detail.ticker} (${detail.name}) com histórico de preços, indicadores e regras fiscais na B3.`,
      openGraph: {
        title: `${detail.ticker} | IndexDesk B3`,
        description: `Lâmina e inteligência de mercado para ${detail.name}.`,
      },
    };
  } catch {
    return {
      title: `${ticker.toUpperCase()} | IndexDesk`,
      description: `Análise de ${ticker.toUpperCase()} na B3.`,
    };
  }
}

function StatCard({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-xs font-semibold uppercase text-muted-foreground">
          {label}
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p className="font-mono text-2xl font-bold text-foreground">{value}</p>
        {hint ? <p className="mt-1 text-xs text-muted-foreground">{hint}</p> : null}
      </CardContent>
    </Card>
  );
}

export default async function AssetDetailPage({ params }: PageProps) {
  const { ticker } = await params;
  if (!ticker?.trim()) notFound();

  const { detail, quotes } = await getAssetData(ticker);
  const stats = detail.stats;
  const positive = (stats.changeDayPercent ?? 0) >= 0;
  const typeIsBdr = isBdr(detail.assetType);
  const tradingViewSymbol = detail.tradingViewSymbol || `BMFBOVESPA:${detail.ticker}`;

  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'FinancialProduct',
    name: detail.name,
    identifier: detail.ticker,
    category: typeIsBdr ? 'Brazilian Depositary Receipt' : 'Exchange Traded Fund',
  };

  return (
    <main className="container mx-auto flex flex-col gap-6 px-4 py-8">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, '\\u003c') }}
      />
      <Link href="/ativos" className="w-fit">
        <Button variant="ghost" size="sm" className="-ml-2 gap-1.5 text-muted-foreground">
          <ArrowLeft className="size-3.5" />
          Voltar ao catálogo
        </Button>
      </Link>

      <header className="flex flex-col justify-between gap-4 border-b pb-6 md:flex-row md:items-center">
        <div className="flex flex-col gap-1.5">
          <div className="flex flex-wrap items-center gap-2.5">
            <h1 className="font-mono text-3xl font-bold tracking-tight">{detail.ticker}</h1>
            <Badge variant="secondary">{typeIsBdr ? 'BDR de ETF' : 'ETF'}</Badge>
            <Badge variant="outline">{detail.currency}</Badge>
            {detail.cnpj ? (
              <Badge variant="outline" className="text-xs">
                CNPJ: {detail.cnpj}
              </Badge>
            ) : null}
          </div>
          <p className="text-sm font-medium text-muted-foreground">{detail.name}</p>
        </div>
        <div className="flex items-center gap-4">
          <div className="text-right">
            <p className="font-mono text-2xl font-bold">
              {stats.lastPrice != null ? formatCurrencyBRL(stats.lastPrice) : '—'}
            </p>
            {stats.changeDayPercent != null ? (
              <p
                className={`inline-flex items-center text-xs font-semibold ${
                  positive ? 'text-positive' : 'text-negative'
                }`}
              >
                {positive ? (
                  <TrendingUp className="mr-1 size-3" />
                ) : (
                  <TrendingDown className="mr-1 size-3" />
                )}
                {formatPercent(stats.changeDayPercent)} hoje
              </p>
            ) : null}
          </div>
          <a
            href={`https://br.tradingview.com/chart/?symbol=${encodeURIComponent(tradingViewSymbol)}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            <Button size="sm" className="gap-1.5">
              TradingView <ExternalLink className="size-3.5" />
            </Button>
          </a>
        </div>
      </header>

      {detail.fiscal ? (
        <FiscalTaxCard
          assetType={typeIsBdr ? 'BDR' : 'ETF'}
          isIrelandUcits={detail.fiscal.taxDomicile === 'IRELAND_UCITS'}
        />
      ) : null}

      <section
        aria-label="Indicadores do ativo"
        className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"
      >
        <StatCard
          label="Retorno 12 meses"
          value={
            stats.return12mPercent != null
              ? formatPercent(stats.return12mPercent)
              : stats.returnYtdPercent != null
                ? formatPercent(stats.returnYtdPercent)
                : '—'
          }
          hint={
            stats.return12mPercent != null
              ? 'Últimos 12 meses'
              : stats.returnYtdPercent != null
                ? 'Acumulado no ano (YTD)'
                : undefined
          }
        />
        <StatCard
          label="Volatilidade anual"
          value={
            stats.annualizedVolatilityPercent == null
              ? '—'
              : formatPercent(stats.annualizedVolatilityPercent)
          }
          hint="Desvio-padrão anualizado"
        />
        <StatCard
          label="Máximo drawdown"
          value={stats.maxDrawdownPercent == null ? '—' : formatPercent(stats.maxDrawdownPercent)}
          hint="Maior queda no histórico"
        />
        <StatCard
          label="Sharpe"
          value={stats.sharpeRatio == null ? '—' : numberFormatter.format(stats.sharpeRatio)}
          hint="Retorno ajustado ao risco"
        />
      </section>

      <PriceHistoryChart quotes={quotes} ticker={detail.ticker} />

      <section
        className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
        aria-label="Dados cadastrais"
      >
        <StatCard
          label="Primeira cotação"
          value={
            stats.firstQuoteDate ? new Date(stats.firstQuoteDate).toLocaleDateString('pt-BR') : '—'
          }
        />
        <StatCard
          label="Última cotação"
          value={
            stats.lastQuoteDate ? new Date(stats.lastQuoteDate).toLocaleDateString('pt-BR') : '—'
          }
        />
        <StatCard label="Código ISIN" value={detail.isin || '—'} />
      </section>

      <div className="flex flex-wrap gap-3">
        <Link href={`/comparador?ticker=${detail.ticker}`}>
          <Button variant="outline" size="sm" className="gap-1.5">
            <Layers className="size-3.5" />
            Adicionar ao comparador
          </Button>
        </Link>
        <Link href={`/ferramentas/backtest?ticker=${detail.ticker}`}>
          <Button variant="outline" size="sm" className="gap-1.5">
            <TrendingUp className="size-3.5" />
            Simular no backtest
          </Button>
        </Link>
        {detail.fiscal && detail.fiscal.foreignDividendWithholdingPercent > 0 ? (
          <p className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <ReceiptText className="size-3.5" />
            Retenção estrangeira estimada:{' '}
            {formatPercent(detail.fiscal.foreignDividendWithholdingPercent)}
          </p>
        ) : null}
      </div>
    </main>
  );
}
