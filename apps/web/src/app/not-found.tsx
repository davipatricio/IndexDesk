import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ArrowLeft, SearchX } from 'lucide-react';

export default function NotFound() {
  return (
    <div className="container mx-auto px-4 py-24 flex flex-col items-center justify-center text-center gap-4">
      <div className="p-4 rounded-full bg-muted/60 text-muted-foreground">
        <SearchX className="size-10" />
      </div>
      <h1 className="text-2xl font-bold tracking-tight">Ativo ou Página Não Encontrada</h1>
      <p className="text-sm text-muted-foreground max-w-md">
        O ticker ou página que você buscou não foi localizado na base de dados da B3 ou nos
        relatórios CVM.
      </p>
      <Link href="/">
        <Button size="sm" className="gap-1.5 text-xs">
          <ArrowLeft className="size-3.5" />
          Voltar ao Catálogo de ETFs
        </Button>
      </Link>
    </div>
  );
}
