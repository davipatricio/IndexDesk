import Link from 'next/link';
import { Button } from '@/components/ui/button';

export default function OfflinePage() {
  return (
    <div className="container mx-auto flex min-h-[60vh] flex-col items-center justify-center gap-4 px-4 text-center">
      <h1 className="text-2xl font-bold">Você está offline</h1>
      <p className="max-w-md text-sm text-muted-foreground">
        As páginas e calculadoras salvas continuam disponíveis. Reconecte-se para atualizar dados de
        mercado e relatórios CVM.
      </p>
      <Link href="/">
        <Button size="sm">Abrir catálogo salvo</Button>
      </Link>
    </div>
  );
}
