import type { Metadata } from 'next';
import { Trophy } from 'lucide-react';
import { RankingsExplorer } from '@/components/rankings/rankings-explorer';

export const metadata: Metadata = {
  title: 'Rankings de ETFs, BDRs e FIIs da B3 | IndexDesk',
  description:
    'Classifique ETFs, BDRs e FIIs da B3 por retorno, Sharpe, volatilidade, drawdown e volume médio. Dados locais atualizados diariamente.',
};

export default function RankingsPage() {
  return (
    <div className="container mx-auto flex flex-col gap-6 px-4 py-8">
      <div className="flex flex-col gap-2">
        <span className="text-xs font-semibold uppercase tracking-wider text-primary">
          Desempenho do mercado
        </span>
        <h1 className="flex items-center gap-2.5 text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
          <Trophy className="size-6 text-primary" />
          Rankings de ativos
        </h1>
        <p className="max-w-3xl text-sm leading-relaxed text-muted-foreground">
          Consulte os destaques por retorno, risco, eficiência e liquidez. Escolha o tipo de ativo e
          a métrica para reorganizar a tabela — os melhores ficam no topo.
        </p>
      </div>
      <RankingsExplorer />
    </div>
  );
}
