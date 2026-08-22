import type { Metadata } from 'next';
import { AssetExplorer } from '@/components/catalog/asset-explorer';
import { Database } from 'lucide-react';

export const metadata: Metadata = {
  title: 'Explorador de Ativos B3 — ETFs, FIIs e BDRs | IndexDesk',
  description: 'Explore ETFs, FIIs e BDRs da B3 com busca e filtros por categoria.',
};

export default function AtivosPage() {
  return (
    <div className="container mx-auto flex flex-col gap-6 px-4 py-8">
      <div className="flex flex-col gap-2">
        <span className="text-xs font-semibold uppercase tracking-wider text-primary">
          Mercado B3 consolidado
        </span>
        <h1 className="flex items-center gap-2.5 text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
          <Database className="size-6 text-primary" />
          Explorador de ativos
        </h1>
        <p className="max-w-3xl text-sm leading-relaxed text-muted-foreground">
          Consulte os ativos disponíveis, filtre por tipo e pesquise por ticker, nome ou benchmark.
          Se não houver resultados, ajuste os filtros ou tente novamente mais tarde.
        </p>
      </div>
      <AssetExplorer />
    </div>
  );
}
