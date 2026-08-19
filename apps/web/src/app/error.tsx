'use client';

import { Button } from '@/components/ui/button';
import { AlertTriangle } from 'lucide-react';

export default function ErrorPage({
  error: _error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="container mx-auto flex flex-col items-center justify-center gap-4 px-4 py-24 text-center">
      <div className="rounded-full bg-destructive/10 p-4 text-destructive">
        <AlertTriangle className="size-10" />
      </div>
      <h1 className="text-2xl font-bold tracking-tight">Ocorreu um Erro Inesperado</h1>
      <p className="max-w-md text-sm text-muted-foreground">
        Não foi possível carregar os dados solicitados. Tente novamente ou retorne à página inicial.
      </p>
      <Button onClick={() => reset()} size="sm" className="text-xs">
        Tentar Novamente
      </Button>
    </div>
  );
}
