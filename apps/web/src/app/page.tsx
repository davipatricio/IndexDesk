import Link from 'next/link';
import { dehydrate, HydrationBoundary } from '@tanstack/react-query';
import { getQueryClient } from '@/lib/query-client';
import { fetchAssets } from '@/lib/api-client';
import { computeCatalogStats } from '@/lib/catalog-stats';
import { formatPercent } from '@/lib/utils';
import { AssetCatalogTable } from '@/components/home/asset-catalog-table';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Database, Percent, ArrowRight, Sparkles } from 'lucide-react';

export default async function HomePage() {
  const queryClient = getQueryClient();
  const assets = await queryClient.fetchQuery({ queryKey: ['assets'], queryFn: () => fetchAssets() });
  const stats = computeCatalogStats(assets);

  return <HydrationBoundary state={dehydrate(queryClient)}><div className="container mx-auto flex flex-col gap-8 px-4 py-8">
    <section className="flex max-w-3xl flex-col gap-3"><Badge variant="secondary" className="w-fit gap-1 text-xs"><Sparkles className="size-3 text-primary" />Plataforma de inteligência B3</Badge><h1 className="text-3xl font-bold tracking-tight text-foreground sm:text-4xl">Inteligência e análise de ativos na B3</h1><p className="text-sm leading-relaxed text-muted-foreground sm:text-base">Consulte o catálogo servido pela API local do IndexDesk, com dados de mercado e indicadores somente quando estiverem disponíveis no backend.</p></section>
    <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"><Card><CardHeader className="pb-2"><CardTitle className="flex items-center gap-1.5 text-xs font-semibold text-muted-foreground"><Database className="size-3.5 text-primary" />Ativos retornados</CardTitle></CardHeader><CardContent><div className="font-mono text-2xl font-bold">{stats.assetCount}</div><p className="mt-1 text-xs text-muted-foreground">Catálogo local</p></CardContent></Card><Card><CardHeader className="pb-2"><CardTitle className="flex items-center gap-1.5 text-xs font-semibold text-muted-foreground"><Percent className="size-3.5 text-primary" />Retorno médio 12M</CardTitle></CardHeader><CardContent><div className="font-mono text-2xl font-bold">{stats.primaryMetric.value === '—' ? '—' : formatPercent(Number.parseFloat(stats.primaryMetric.value))}</div><p className="mt-1 text-xs text-muted-foreground">Somente registros com métrica</p></CardContent></Card><Card><CardHeader className="pb-2"><CardTitle className="text-xs font-semibold text-muted-foreground">Status do catálogo</CardTitle></CardHeader><CardContent><div className="font-mono text-2xl font-bold">{assets.length ? 'Online' : 'Vazio'}</div><p className="mt-1 text-xs text-muted-foreground">Resposta da API local</p></CardContent></Card><Card><CardHeader className="pb-2"><CardTitle className="text-xs font-semibold text-muted-foreground">Dados ausentes</CardTitle></CardHeader><CardContent><div className="font-mono text-2xl font-bold">Preservados</div><p className="mt-1 text-xs text-muted-foreground">Sem valores sintéticos</p></CardContent></Card></section>
    <Card className="border-primary/30"><CardHeader className="flex flex-row items-center justify-between gap-4"><div><CardTitle className="text-base">Explorador de ativos</CardTitle><p className="mt-1 text-xs text-muted-foreground">Filtre o catálogo completo por tipo e consulte a ficha de cada ativo.</p></div><Link href="/ativos"><Button size="sm" className="gap-1.5">Abrir catálogo <ArrowRight className="size-3.5" /></Button></Link></CardHeader></Card>
    <section className="flex flex-col gap-4"><div><h2 className="text-xl font-bold tracking-tight">ETFs retornados pela API</h2><p className="text-xs text-muted-foreground">Nenhuma cotação ou métrica é inventada quando o backend não informa o valor.</p></div><AssetCatalogTable /></section>
  </div></HydrationBoundary>;
}
