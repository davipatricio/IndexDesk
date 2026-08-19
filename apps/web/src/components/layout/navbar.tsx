'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { TickerSearch } from '@/components/layout/ticker-search';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { AccountMenu } from '@/components/layout/account-menu';
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import { Menu } from 'lucide-react';

const NAV_ITEMS = [
  { href: '/ativos', label: 'Explorar Ativos' },
  { href: '/comparador', label: 'Comparador' },
  { href: '/ferramentas/backtest', label: 'Simulador Backtest' },
  { href: '/ferramentas/rendimento-real', label: 'Rendimento Real' },
];

export function Navbar() {
  return (
    <header className="sticky top-0 z-50 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <div className="container mx-auto flex h-14 items-center gap-2 px-4">
        <div className="flex items-center gap-1">
          <Sheet>
            <SheetTrigger
              render={
                <Button
                  variant="ghost"
                  size="icon-sm"
                  className="md:hidden"
                  aria-label="Abrir menu de navegação"
                />
              }
            >
              <Menu className="size-4" />
            </SheetTrigger>
            <SheetContent side="left" className="w-72">
              <SheetHeader>
                <SheetTitle>IndexDesk</SheetTitle>
              </SheetHeader>
              <nav className="flex flex-col gap-1 px-2">
                {NAV_ITEMS.map((item) => (
                  <SheetClose
                    key={item.href}
                    render={
                      <Link
                        href={item.href}
                        className="flex items-center rounded-md px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                      />
                    }
                  >
                    {item.label}
                  </SheetClose>
                ))}
              </nav>
            </SheetContent>
          </Sheet>

          <Link
            href="/"
            className="flex items-center rounded-md px-2 text-base font-semibold tracking-tight text-foreground"
          >
            IndexDesk
          </Link>
        </div>

        <nav className="ml-4 hidden items-center gap-5 text-sm font-medium text-muted-foreground md:flex">
          {NAV_ITEMS.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="transition-colors hover:text-foreground"
            >
              {item.label}
            </Link>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-1">
          <TickerSearch />
          <ThemeToggle />
          <AccountMenu />
        </div>
      </div>
    </header>
  );
}
