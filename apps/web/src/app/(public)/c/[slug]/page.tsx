import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { fetchPublicPortfolio, type PublicPortfolioDto } from '@/lib/api-client';
import { MaskedValue } from '@/components/privacy/masked-value';
import { MaskedSection } from '@/components/portfolio/masked-section';

// Cache de dados fica no fetch (next.revalidate em fetchPublicPortfolio) —
// segment config "revalidate" não é compatível com nextConfig.cacheComponents.

interface PageProps {
  params: Promise<{ slug: string }>;
  searchParams: Promise<{ t?: string }>;
}

async function load(slug: string, shareToken?: string): Promise<PublicPortfolioDto | null> {
  try {
    return await fetchPublicPortfolio(slug, shareToken);
  } catch {
    return null;
  }
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const portfolio = await load(slug);
  if (!portfolio)
    return { title: 'Carteira não encontrada', robots: { index: false, follow: false } };
  return {
    title: `Carteira ${portfolio.title} — ${portfolio.identityLabel}`,
    description:
      portfolio.description ??
      `Alocação e rentabilidade da carteira "${portfolio.title}" (${portfolio.riskProfile}) no IndexDesk.`,
    robots: { index: true, follow: true },
  };
}

const riskLabels: Record<string, string> = {
  conservador: 'Conservadora',
  moderado: 'Moderada',
  arrojado: 'Arrojada',
};

/** Página pública e indexável de uma carteira (M-P5). Respeita percent_only/identidade. */
export default async function PublicPortfolioPage({ params, searchParams }: PageProps) {
  const [{ slug }, sp] = await Promise.all([params, searchParams]);
  const portfolio = await load(slug, sp.t);

  if (!portfolio) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-20 text-center">
        <h1 className="text-xl font-semibold">Carteira não encontrada</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          O link pode ter expirado ou a carteira saiu do ar.
        </p>
        <Link href="/" className="mt-4 inline-block text-sm text-primary hover:underline">
          Voltar ao início
        </Link>
      </div>
    );
  }

  const showValues = portfolio.valuesMode === 'full_values';
  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: [
      { '@type': 'ListItem', position: 1, name: 'IndexDesk', item: '/' },
      { '@type': 'ListItem', position: 2, name: 'Carteiras' },
      { '@type': 'ListItem', position: 3, name: portfolio.title },
    ],
  };

  return (
    <MaskedSection>
      <div className="mx-auto w-full max-w-4xl space-y-6 px-4 py-8">
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, '\\u003c') }}
        />

        <header className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="text-2xl font-semibold tracking-tight">{portfolio.title}</h1>
            <Badge variant="secondary">
              {riskLabels[portfolio.riskProfile] ?? portfolio.riskProfile}
            </Badge>
            <Badge variant="outline">{portfolio.identityLabel}</Badge>
          </div>
          {portfolio.description ? (
            <p className="text-sm text-muted-foreground">{portfolio.description}</p>
          ) : null}
          <p className="text-xs text-muted-foreground">
            Criada em {new Date(portfolio.createdAt).toLocaleDateString('pt-BR')} · preços de
            fechamento
          </p>
        </header>

        <section className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <Card>
            <CardHeader className="pb-1">
              <p className="text-xs text-muted-foreground">Retorno total</p>
            </CardHeader>
            <CardContent>
              <p
                className={`text-xl font-semibold tabular-nums ${
                  portfolio.totalReturnPercent >= 0 ? 'text-emerald-600' : 'text-red-600'
                }`}
              >
                <MaskedValue>{fmtPct(portfolio.totalReturnPercent)}</MaskedValue>
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-1">
              <p className="text-xs text-muted-foreground">Posições</p>
            </CardHeader>
            <CardContent>
              <p className="text-xl font-semibold tabular-nums">{portfolio.positions.length}</p>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-1">
              <p className="text-xs text-muted-foreground">Modo</p>
            </CardHeader>
            <CardContent>
              <p className="text-sm font-medium">
                {showValues ? 'Valores abertos' : 'Somente percentuais'}
              </p>
            </CardContent>
          </Card>
        </section>

        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Alocação por classe</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {portfolio.allocationPercent.length === 0 ? (
              <p className="text-sm text-muted-foreground">Sem posições para exibir.</p>
            ) : (
              portfolio.allocationPercent.map((a) => (
                <div key={a.assetClass} className="space-y-1">
                  <div className="flex items-center justify-between text-xs">
                    <span>{a.assetClass}</span>
                    <span className="tabular-nums">{fmtPct(a.percent)}</span>
                  </div>
                  <div className="h-2 overflow-hidden rounded-full bg-muted">
                    <div
                      className="h-full rounded-full bg-primary"
                      style={{ width: `${Math.min(100, Math.max(0, a.percent))}%` }}
                    />
                  </div>
                </div>
              ))
            )}
          </CardContent>
        </Card>

        {portfolio.positions.length > 0 ? (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Principais posições</CardTitle>
            </CardHeader>
            <CardContent>
              <ul className="divide-y">
                {portfolio.positions.map((p) => (
                  <li key={p.ticker} className="flex items-center justify-between py-2 text-sm">
                    <span>
                      <span className="font-medium">{p.ticker}</span>
                      <span className="block text-xs text-muted-foreground">{p.name}</span>
                    </span>
                    <span className="flex items-center gap-4 tabular-nums">
                      <span className="text-xs text-muted-foreground">
                        peso {fmtPct(p.weightPercent)}
                      </span>
                      <span
                        className={
                          p.returnPercent >= 0
                            ? 'font-medium text-emerald-600'
                            : 'font-medium text-red-600'
                        }
                      >
                        <MaskedValue>{fmtPct(p.returnPercent)}</MaskedValue>
                      </span>
                    </span>
                  </li>
                ))}
              </ul>
            </CardContent>
          </Card>
        ) : null}

        {!showValues ? (
          <p className="rounded-lg border bg-muted/30 p-3 text-[11px] text-muted-foreground">
            O dono desta carteira optou por compartilhar apenas percentuais — valores em R$ ficam
            privados.
          </p>
        ) : null}

        <p className="text-[11px] text-muted-foreground">
          Conteúdo educacional. Não constitui recomendação de investimento.
        </p>
      </div>
    </MaskedSection>
  );
}

function fmtPct(v: number): string {
  return `${v >= 0 ? '' : '-'}${Math.abs(v).toLocaleString('pt-BR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}%`;
}
