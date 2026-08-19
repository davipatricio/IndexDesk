import Link from 'next/link';
import { dehydrate, HydrationBoundary } from '@tanstack/react-query';
import { getQueryClient } from '@/lib/query-client';
import { fetchAssets } from '@/lib/api-client';
import { computeCatalogStats } from '@/lib/catalog-stats';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { AssetCatalogTable } from '@/components/home/asset-catalog-table';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Database,
  Percent,
  Building2,
  Users,
  ArrowRight,
  Layers,
  Landmark,
  Globe,
  Sparkles,
} from 'lucide-react';

const integerFormatterBR = new Intl.NumberFormat('pt-BR');

export default async function HomePage() {
  const queryClient = getQueryClient();

  const assets = await queryClient.fetchQuery({
    queryKey: ['assets'],
    queryFn: () => fetchAssets(),
  });

  const stats = computeCatalogStats(assets);

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <div className="container mx-auto px-4 py-8 flex flex-col gap-8">
        {/* Hero Section */}
        <section className="flex flex-col gap-3 max-w-3xl">
          <div className="flex items-center gap-2">
            <Badge variant="secondary" className="gap-1 text-xs">
              <Sparkles className="size-3 text-primary" />
              Plataforma de Inteligência B3
            </Badge>
          </div>
          <h1 className="text-3xl sm:text-4xl font-bold tracking-tight text-foreground">
            Inteligência e Análise de ETFs, FIIs e BDRs na B3
          </h1>
          <p className="text-muted-foreground text-sm sm:text-base leading-relaxed">
            Consulte taxas de administração reais, lâminas CVM diárias, regras tributárias (sem
            isenção de R$ 20k para ETFs/BDRs, DARF e isenção de proventos para FIIs), comparadores e
            simuladores de rendimento.
          </p>
        </section>

        {/* Real Computed Catalog Highlights */}
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5">
                <Database className="size-3.5 text-primary" />
                Ativos Acompanhados
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-foreground font-mono">{stats.assetCount}</div>
              <p className="text-xs text-muted-foreground mt-1">
                Patrimônio combinado de {formatCurrencyBRL(stats.totalNetAssets)}
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5">
                <Percent className="size-3.5 text-primary" />
                Menor Taxa de Adm.
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-foreground font-mono">
                {formatPercent(stats.lowestManagementFee)} a.a.
              </div>
              <p className="text-xs text-muted-foreground mt-1">
                {stats.lowestFeeTicker} • Média da amostra em{' '}
                {formatPercent(stats.averageManagementFee)} a.a.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5">
                <Building2 className="size-3.5 text-primary" />
                Gestoras no Catálogo
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-foreground font-mono">
                {stats.managerCount}
              </div>
              <p className="text-xs text-muted-foreground mt-1">
                iShares, Itaú Asset, Investo, Hashdex e outras
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5">
                <Users className="size-3.5 text-primary" />
                Cotistas Mapeados
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-foreground font-mono">
                {integerFormatterBR.format(stats.totalShareholders)}
              </div>
              <p className="text-xs text-muted-foreground mt-1">
                Posições registradas em informes diários CVM
              </p>
            </CardContent>
          </Card>
        </section>

        {/* Feature Explorer Banner / Call to Action */}
        <Card className="border-primary/30 bg-gradient-to-r from-card via-card to-primary/5">
          <CardHeader className="pb-3">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
              <div className="flex flex-col gap-1">
                <CardTitle className="text-base font-bold text-foreground flex items-center gap-2">
                  <Database className="size-4 text-primary" />
                  Explorador Multi-Classes: ETFs, FIIs e BDRs
                </CardTitle>
                <CardDescription className="text-xs text-muted-foreground max-w-xl">
                  Acesse o painel completo com filtros de gestora/segmento, ordenação dinâmica em
                  todas as colunas, Dividend Yield e regras fiscais detalhadas.
                </CardDescription>
              </div>

              <div className="flex items-center gap-2">
                <Link href="/ativos">
                  <Button size="sm" className="gap-1.5">
                    Explorar Todos os Ativos
                    <ArrowRight className="size-3.5" />
                  </Button>
                </Link>
              </div>
            </div>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-2 pt-0">
            <Link href="/ativos?tab=ETF">
              <Badge variant="outline" className="gap-1 hover:bg-accent cursor-pointer py-1 px-2.5">
                <Layers className="size-3 text-primary" />
                ETFs de Índice (12)
              </Badge>
            </Link>
            <Link href="/ativos?tab=FII">
              <Badge variant="outline" className="gap-1 hover:bg-accent cursor-pointer py-1 px-2.5">
                <Landmark className="size-3 text-primary" />
                Fundos Imobiliários / FIIs (11)
              </Badge>
            </Link>
            <Link href="/ativos?tab=BDR">
              <Badge variant="outline" className="gap-1 hover:bg-accent cursor-pointer py-1 px-2.5">
                <Globe className="size-3 text-primary" />
                BDRs &amp; ETFs Globais (10)
              </Badge>
            </Link>
          </CardContent>
        </Card>

        {/* Interactive ETF Catalog Section */}
        <section className="flex flex-col gap-4">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <div className="flex flex-col gap-1">
              <h2 className="text-xl font-bold tracking-tight text-foreground">
                Destaques do Catálogo de ETFs
              </h2>
              <p className="text-xs text-muted-foreground">
                Ordene por taxa de administração, patrimônio líquido, retorno acumulado ou cotação.
              </p>
            </div>
            <Link
              href="/ativos"
              className="text-xs text-primary hover:underline font-medium inline-flex items-center gap-1"
            >
              Ver catálogo completo com FIIs e BDRs
              <ArrowRight className="size-3" />
            </Link>
          </div>
          <AssetCatalogTable />
        </section>
      </div>
    </HydrationBoundary>
  );
}
