import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ArrowRight } from 'lucide-react';
import {
  fetchAssetRankings,
  fetchMarketIndicators,
  fetchQuoteSparks,
  type AssetRankingDto,
  type MarketIndicatorDto,
  type QuoteSparkPointDto,
} from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent, cn } from '@/lib/utils';
import { Sparkline } from '@/components/charts/sparkline';

// Dashboard fetches with time-based revalidate need per-request rendering
// under cacheComponents — an instant static shell cannot represent them.
export const instant = false;

const MOVERS_COUNT = 5;

const TOOLS = [
  {
    href: '/ativos',
    name: 'Explorar ativos',
    description: 'Catálogo completo de ETFs, BDRs e FIIs com filtros e busca.',
  },
  {
    href: '/rankings',
    name: 'Rankings',
    description: 'Destaques por retorno, risco, eficiência e liquidez.',
  },
  {
    href: '/comparador',
    name: 'Comparador',
    description: 'Até seis ativos lado a lado, normalizados no mesmo período.',
  },
  {
    href: '/ferramentas/backtest',
    name: 'Simulador de backtest',
    description: 'Carteiras com aportes, rebalanceamento e comparação com o CDI.',
  },
  {
    href: '/ferramentas/rendimento-real',
    name: 'Rendimento real',
    description: 'Desconta a inflação e mostra o ganho de verdade da sua carteira.',
  },
] as const;

async function safeFetch<T>(fetcher: () => Promise<T>): Promise<T | null> {
  try {
    return await fetcher();
  } catch {
    return null;
  }
}

function pctClass(value: number | null | undefined): string | undefined {
  if (value === null || value === undefined) return undefined;
  if (value > 0) return 'text-positive';
  if (value < 0) return 'text-negative';
  return undefined;
}

function signed(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return '—';
  return `${value > 0 ? '+' : ''}${formatPercent(value)}`;
}

// Nota: fetch direto em vez de queryClient.fetchQuery — o cache do React Query chama
// Date.now() internamente e bloqueia o prerender sob cacheComponents. O revalidate
// equivalente já vive nos próprios fetchers.
export default async function HomePage() {
  const [indicators, gainers, losers] = await Promise.all([
    safeFetch(fetchMarketIndicators),
    safeFetch(() => fetchAssetRankings({ metric: 'retorno12m', orderDirection: 'desc' })),
    safeFetch(() => fetchAssetRankings({ metric: 'retorno12m', orderDirection: 'asc' })),
  ]);

  const topGainers = (gainers ?? [])
    .filter((row) => row.metricValue !== null)
    .slice(0, MOVERS_COUNT);
  const topLosers = (losers ?? []).filter((row) => row.metricValue !== null).slice(0, MOVERS_COUNT);
  const hasMovers = topGainers.length > 0 || topLosers.length > 0;

  const moverTickers = [...topGainers, ...topLosers].map((row) => row.ticker.toUpperCase());
  const sparks =
    moverTickers.length > 0 ? await safeFetch(() => fetchQuoteSparks(moverTickers)) : undefined;

  return (
    <div className="container mx-auto flex flex-col gap-10 px-4 py-8">
      <section className="flex flex-col gap-1">
        <h1 className="text-xl font-bold tracking-tight text-foreground sm:text-2xl">
          Entenda seus investimentos.
        </h1>
        <p className="text-sm text-muted-foreground">
          Explore ativos da B3, compare estratégias e acompanhe suas carteiras em um só lugar.
        </p>
        <div className="mt-4 flex flex-wrap gap-2">
          <Button render={<Link href="/ativos" />}>Explorar ativos</Button>
          <Button variant="outline" render={<Link href="/dashboard" />}>
            Minhas carteiras
          </Button>
        </div>
      </section>

      <section aria-label="Indicadores de mercado" className="overflow-x-auto">
        {indicators && indicators.length > 0 ? (
          <div className="grid grid-cols-1 divide-y rounded-lg border sm:min-w-0 sm:grid-cols-3 sm:divide-x sm:divide-y-0">
            {indicators.map((indicator) => (
              <IndicatorCell key={indicator.code} indicator={indicator} />
            ))}
          </div>
        ) : (
          <p className="rounded-lg border p-4 text-sm text-muted-foreground">
            {indicators === null
              ? 'Não foi possível carregar os indicadores agora. Recarregue a página para tentar novamente.'
              : 'Os indicadores ainda não estão disponíveis. Explore os ativos enquanto isso.'}
          </p>
        )}
      </section>

      {hasMovers ? (
        <section
          aria-label="Destaques de 12 meses"
          className="grid grid-cols-1 gap-x-8 gap-y-6 lg:grid-cols-2"
        >
          {topGainers.length > 0 && (
            <MoverColumn title="Maiores retornos em 12 meses" rows={topGainers} sparks={sparks} />
          )}
          {topLosers.length > 0 && (
            <MoverColumn title="Menores retornos em 12 meses" rows={topLosers} sparks={sparks} />
          )}
          {(gainers === null || losers === null) && (
            <p role="alert">
              Parte dos destaques não pôde ser carregada. Recarregue a página para tentar novamente.
            </p>
          )}
          <p className="text-xs leading-relaxed text-muted-foreground lg:col-span-2">
            Retornos calculados com preços de fechamento; ativos sem histórico suficiente ficam fora
            da lista. Conteúdo educacional — não é recomendação de investimento.
          </p>
        </section>
      ) : (
        <section aria-label="Destaques de 12 meses">
          <p className="rounded-lg border p-4 text-sm text-muted-foreground">
            Os destaques de retorno aparecem quando houver histórico suficiente de cotações. Explore
            o catálogo completo enquanto isso.
          </p>
        </section>
      )}

      <section aria-label="Ferramentas" className="flex flex-col gap-2">
        <h2 className="text-sm font-semibold text-foreground">Ferramentas</h2>
        <ul className="divide-y divide-border/60 overflow-hidden rounded-lg border">
          {TOOLS.map((tool) => (
            <li key={tool.href}>
              <Link
                href={tool.href}
                className="group flex items-center justify-between gap-4 px-4 py-3 transition-colors hover:bg-accent/50 focus-visible:bg-accent/50 focus-visible:outline-none"
              >
                <span className="flex min-w-0 flex-col gap-0.5">
                  <span className="text-sm font-medium text-foreground">{tool.name}</span>
                  <span className="text-xs text-muted-foreground">{tool.description}</span>
                </span>
                <ArrowRight className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}

function IndicatorCell({ indicator }: { indicator: MarketIndicatorDto }) {
  return (
    <div className="flex flex-col gap-1 px-5 py-4">
      <span className="text-xs font-medium tracking-wide text-muted-foreground uppercase">
        {indicator.name}
      </span>
      <span className="font-mono text-2xl font-bold tabular-nums">
        {formatPercent(indicator.accum12mPercent ?? indicator.latestValue)}
      </span>
      <span className="text-xs text-muted-foreground">
        {indicator.accum12mPercent == null ? 'último valor disponível' : 'acumulado em 12 meses'} ·
        dado de {new Date(`${indicator.latestDate}T12:00:00`).toLocaleDateString('pt-BR')}
      </span>
    </div>
  );
}

function moverHref(row: Pick<AssetRankingDto, 'ticker' | 'assetType'>): string {
  const type = row.assetType.toUpperCase();
  const segment =
    type.includes('FII') || type.includes('REAL_ESTATE')
      ? 'fii'
      : type.includes('BDR')
        ? 'bdr'
        : 'etf';
  return `/${segment}/${encodeURIComponent(row.ticker.toLowerCase())}`;
}

function MoverSparkline({
  sparks,
  ticker,
}: {
  sparks?: Record<string, QuoteSparkPointDto[]> | null;
  ticker: string;
}) {
  const series = sparks?.[ticker.toUpperCase()];
  if (!series || series.length < 2) return null;
  return (
    <Sparkline values={series.map((point) => point.close)} className="hidden h-7 w-16 md:block" />
  );
}

function MoverColumn({
  title,
  rows,
  sparks,
}: {
  title: string;
  rows: AssetRankingDto[];
  sparks?: Record<string, QuoteSparkPointDto[]> | null;
}) {
  return (
    <div className="flex flex-col gap-2">
      <h2 className="text-sm font-semibold text-foreground">{title}</h2>
      <ul className="divide-y divide-border/60 overflow-hidden rounded-lg border">
        {rows.map((row) => (
          <li key={row.ticker}>
            <Link
              href={moverHref(row)}
              className="flex items-center justify-between gap-3 px-4 py-2.5 transition-colors hover:bg-accent/50 focus-visible:bg-accent/50 focus-visible:outline-none"
            >
              <span className="flex min-w-0 items-baseline gap-2">
                <span className="font-mono text-sm font-semibold text-foreground">
                  {row.ticker}
                </span>
                <span className="truncate text-xs text-muted-foreground">{row.name}</span>
              </span>
              <span className="flex shrink-0 items-baseline gap-3">
                <MoverSparkline sparks={sparks} ticker={row.ticker} />
                <span className="hidden font-mono text-xs tabular-nums text-muted-foreground sm:inline">
                  {row.lastPrice === null ? '—' : formatCurrencyBRL(row.lastPrice)}
                </span>
                <span
                  className={cn(
                    'w-20 text-right font-mono text-sm font-semibold tabular-nums',
                    pctClass(row.metricValue),
                  )}
                >
                  {signed(row.metricValue)}
                </span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
