import type { Metadata } from 'next';
import { AssetExplorer } from '@/components/catalog/asset-explorer';
import { ALL_CATALOG_ASSETS } from '@/lib/mock-catalog';
import { Database } from 'lucide-react';

export const metadata: Metadata = {
  title: 'Explorador de Ativos B3 — ETFs, FIIs e BDRs | IndexDesk',
  description:
    'Catálogo completo e comparável de ETFs, Fundos Imobiliários (FIIs) e BDRs listados na B3. Filtre por gestora, segmento, ordene por dividend yield, taxas, cotação e consulte regras fiscais.',
  openGraph: {
    title: 'Explorador de Ativos B3 | IndexDesk',
    description:
      'Catálogo completo de ETFs, FIIs e BDRs com filtros por gestora/segmento e ordenação de todas as colunas.',
  },
};

export default function AtivosPage() {
  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'DataCatalog',
    name: 'Catálogo de Ativos B3 — IndexDesk',
    description:
      'Catálogo interativo com inteligência de mercado para ETFs, Fundos Imobiliários e BDRs na B3.',
    dataset: ALL_CATALOG_ASSETS.slice(0, 15).map((asset) => ({
      '@type': 'FinancialProduct',
      name: asset.name,
      identifier: asset.ticker,
      category: asset.category,
      provider: {
        '@type': 'Organization',
        name: asset.manager,
      },
    })),
  };

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd).replace(/</g, '\\u003c') }}
      />

      {/* Page Header */}
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold text-primary uppercase tracking-wider">
            Mercado B3 Consolidado
          </span>
        </div>
        <h1 className="text-2xl sm:text-3xl font-bold tracking-tight flex items-center gap-2.5 text-foreground">
          <Database className="size-6 text-primary" />
          Explorador de Ativos
        </h1>
        <p className="text-muted-foreground text-sm max-w-3xl leading-relaxed">
          Navegue pelo catálogo completo de Fundos de Índice (ETFs), Fundos Imobiliários (FIIs) e
          BDRs. Aplique filtros por gestora ou segmento, pesquise por ticker e ordene todas as
          métricas fundamentais.
        </p>
      </div>

      {/* Multi-category Interactive Explorer */}
      <AssetExplorer />
    </div>
  );
}
