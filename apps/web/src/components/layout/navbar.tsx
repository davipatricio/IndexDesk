import Link from 'next/link';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { BarChart3, Calculator, Layers, Shield } from 'lucide-react';

export function Navbar() {
  return (
    <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="container mx-auto flex h-14 items-center justify-between px-4">
        <div className="flex items-center gap-6">
          <Link href="/" className="flex items-center gap-2 font-bold tracking-tight text-lg">
            <span className="bg-gradient-to-r from-emerald-500 to-teal-400 bg-clip-text text-transparent">
              IndexDesk
            </span>
            <Badge variant="outline" className="text-[10px] uppercase tracking-wider py-0 px-1.5">
              B3 Intelligence
            </Badge>
          </Link>
          <nav className="hidden md:flex items-center gap-5 text-sm font-medium text-muted-foreground">
            <Link href="/" className="transition-colors hover:text-foreground">
              Catálogo de ETFs
            </Link>
            <Link
              href="/comparador"
              className="transition-colors hover:text-foreground flex items-center gap-1.5"
            >
              <Layers className="size-3.5" />
              Comparador
            </Link>
            <Link
              href="/ferramentas/backtest"
              className="transition-colors hover:text-foreground flex items-center gap-1.5"
            >
              <BarChart3 className="size-3.5" />
              Simulador Backtest
            </Link>
            <Link
              href="/ferramentas/rendimento-real"
              className="transition-colors hover:text-foreground flex items-center gap-1.5"
            >
              <Calculator className="size-3.5" />
              Rendimento Real
            </Link>
          </nav>
        </div>
        <div className="flex items-center gap-3">
          <Link href="/admin">
            <Button variant="ghost" size="sm" className="gap-1.5 text-xs text-muted-foreground">
              <Shield className="size-3.5" />
              Backoffice Admin
            </Button>
          </Link>
        </div>
      </div>
    </header>
  );
}
