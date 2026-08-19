import { dehydrate, HydrationBoundary } from '@tanstack/react-query';
import { getQueryClient } from '@/lib/query-client';
import { fetchAssets } from '@/lib/api-client';
import { AssetCatalogTable } from '@/components/home/asset-catalog-table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { TrendingUp, ShieldCheck, Database, Layers } from 'lucide-react';

export default async function HomePage() {
  const queryClient = getQueryClient();

  // SSR-First Prefetching on Server Component
  await queryClient.prefetchQuery({
    queryKey: ['assets'],
    queryFn: () => fetchAssets(),
  });

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <div className="container mx-auto px-4 py-8 flex flex-col gap-8">
        {/* Hero Section */}
        <section className="flex flex-col gap-3 max-w-3xl">
          <div className="inline-flex items-center gap-2">
            <Badge className="gap-1 text-xs bg-emerald-500/15 text-emerald-500">
              <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse" />
              Mercado B3 Aberto
            </Badge>
            <span className="text-xs text-muted-foreground">
              Atualização contínua via CVM & BCB
            </span>
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold tracking-tight">
            Inteligência e Análise Completa de{' '}
            <span className="bg-gradient-to-r from-emerald-400 to-teal-300 bg-clip-text text-transparent">
              ETFs e BDRs na B3
            </span>
          </h1>
          <p className="text-muted-foreground text-sm sm:text-base leading-relaxed">
            Consulte taxas de administração reais, lâminas CVM diárias, regras tributárias (sem
            isenção de R$ 20k, DARF e come-cotas), matriz de overlap de carteiras e simuladores
            avançados de backtest.
          </p>
        </section>

        {/* Feature Highlights Grid */}
        <section className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <Card className="bg-card/50 backdrop-blur border-muted">
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5 uppercase">
                <Database className="size-3.5 text-emerald-500" />
                Lâminas Oficiais CVM
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-lg font-bold">100% Streaming</div>
              <p className="text-xs text-muted-foreground mt-1">
                Informes diários e CDA holdings processados em PostgreSQL 18 & TimescaleDB.
              </p>
            </CardContent>
          </Card>

          <Card className="bg-card/50 backdrop-blur border-muted">
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5 uppercase">
                <ShieldCheck className="size-3.5 text-emerald-500" />
                Fiscal & Tributário
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-lg font-bold">Sem Come-Cotas</div>
              <p className="text-xs text-muted-foreground mt-1">
                Alíquotas exatas de 15% Swing / 20% Day Trade e Lei 13.043/14 para Renda Fixa.
              </p>
            </CardContent>
          </Card>

          <Card className="bg-card/50 backdrop-blur border-muted">
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5 uppercase">
                <Layers className="size-3.5 text-emerald-500" />
                Comparador Multi-Ativos
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-lg font-bold">Até 6 Ativos</div>
              <p className="text-xs text-muted-foreground mt-1">
                Correlação de retornos, overlap de ativos subjacentes e tracking error.
              </p>
            </CardContent>
          </Card>

          <Card className="bg-card/50 backdrop-blur border-muted">
            <CardHeader className="pb-2">
              <CardTitle className="text-xs font-semibold text-muted-foreground flex items-center gap-1.5 uppercase">
                <TrendingUp className="size-3.5 text-emerald-500" />
                Backtest & Sharpe
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-lg font-bold">Rebalanceamento Real</div>
              <p className="text-xs text-muted-foreground mt-1">
                Simulação com aportes recorrentes e ajuste pela inflação IPCA oficial.
              </p>
            </CardContent>
          </Card>
        </section>

        {/* Interactive Catalog Section */}
        <section className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <h2 className="text-xl font-bold tracking-tight">Catálogo de Fundos de Índice</h2>
            <p className="text-xs text-muted-foreground">
              Ordene por taxa de administração, patrimônio líquido, retorno acumulado ou cotação.
            </p>
          </div>
          <AssetCatalogTable />
        </section>
      </div>
    </HydrationBoundary>
  );
}
