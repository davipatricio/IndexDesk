import Link from 'next/link';
import { ArrowRight } from 'lucide-react';
import { dehydrate, HydrationBoundary } from '@tanstack/react-query';
import { getQueryClient } from '@/lib/query-client';
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

export default async function HomePage() {
  const queryClient = getQueryClient();

  const [indicators, gainers, losers] = await Promise.all([
    safeFetch(() =>
      queryClient.fetchQuery({
        queryKey: ['market-indicators'],
        queryFn: fetchMarketIndicators,
        staleTime: 3_600_000,
      }),
    ),
    safeFetch(() =>
      queryClient.fetchQuery({
        queryKey: ['rankings', 'gainers-home'],
        queryFn: () => fetchAssetRankings({ metric: 'retorno12m', orderDirection: 'desc' }),
        staleTime: 300_000,
      }),
    ),
    safeFetch(() =>
      queryClient.fetchQuery({
        queryKey: ['rankings', 'losers-home'],
        queryFn: () => fetchAssetRankings({ metric: 'retorno12m', orderDirection: 'asc' }),
        staleTime: 300_000,
      }),
    ),
  ]);

  const topGainers = (gainers ?? [])
    .filter((row) => row.metricValue !== null)
    .slice(0, MOVERS_COUNT);
  const topLosers = (losers ?? []).filter((row) => row.metricValue !== null).slice(0, MOVERS_COUNT);
  const hasMovers = topGainers.length > 0 && topLosers.length > 0;

  const moverTickers = [...topGainers, ...topLosers].map((row) => row.ticker.toUpperCase());
  const sparks =
    moverTickers.length > 0
      ? await safeFetch(() =>
          queryClient.fetchQuery({
            queryKey: ['quotes-batch', 'home-movers', moverTickers.join(',')],
            queryFn: () => fetchQuoteSparks(moverTickers),
            staleTime: 300_000,
          }),
        )
      : undefined;

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <div className="container mx-auto flex flex-col gap-10 px-4 py-8">
        <section className="flex flex-col gap-1">
          <h1 className="text-xl font-bold tracking-tight text-foreground sm:text-2xl">
            Visão geral do mercado
          </h1>
          <p className="text-sm text-muted-foreground">
            Indicadores e destaques da B3, atualizados com os dados locais mais recentes.
          </p>
        </section>

        <section aria-label="Indicadores de mercado" className="overflow-x-auto">
          {indicators && indicators.length > 0 ? (
            <div className="grid min-w-md grid-cols-1 divide-y rounded-lg border sm:min-w-0 sm:grid-cols-3 sm:divide-x sm:divide-y-0">
              {indicators.map((indicator) => (
                <IndicatorCell key={indicator.code} indicator={indicator} />
              ))}
            </div>
          ) : (
            <p className="rounded-lg border p-4 text-sm text-muted-foreground">
              Indicadores de mercado ainda não disponíveis. Eles aparecem aqui assim que a série do
              Banco Central for sincronizada.
            </p>
          )}
        </section>

        {hasMovers ? (
          <section
            aria-label="Destaques de 12 meses"
            className="grid grid-cols-1 gap-x-8 gap-y-6 lg:grid-cols-2"
          >
            <MoverColumn title="Maiores altas em 12 meses" rows={topGainers} sparks={sparks} />
            <MoverColumn title="Maiores quedas em 12 meses" rows={topLosers} sparks={sparks} />
            <p className="text-xs leading-relaxed text-muted-foreground lg:col-span-2">
              Retornos calculados sobre o histórico local de cotações; ativos sem série suficiente
              ficam fora da lista. Conteúdo educacional — não é recomendação de investimento.
            </p>
          </section>
        ) : (
          <section aria-label="Destaques de 12 meses">
            <p className="rounded-lg border p-4 text-sm text-muted-foreground">
              Os destaques de retorno aparecem quando houver histórico suficiente de cotações.
              Explore o catálogo completo enquanto isso.
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
                    <span className="truncate text-xs text-muted-foreground">
                      {tool.description}
                    </span>
                  </span>
                  <ArrowRight className="size-4 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
                </Link>
              </li>
            ))}
          </ul>
        </section>
      </div>
    </HydrationBoundary>
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
        acumulado em 12 meses · dado de{' '}
        {new Date(`${indicator.latestDate}T12:00:00`).toLocaleDateString('pt-BR')}
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
