'use client';

import * as React from 'react';
import { usePathname } from 'next/navigation';
import { parseAsString, useQueryState } from 'nuqs';
import { Search, Menu, Eye, EyeOff } from 'lucide-react';
import { usePrivacyStore } from '@/stores/privacy-store';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { ThemeToggle } from '@/components/layout/theme-toggle';
import { AccountMenu } from '@/components/layout/account-menu';
import { SidebarContent } from '@/components/layout/sidebar';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';

const Q_PARSER = parseAsString.withDefault('');

/**
 * Barra superior do dashboard: hamburger mobile (< lg), busca ligada a `q`
 * (consumida pela sidebar + tabela de carteiras do outro agente), toggle de
 * privacidade, theme-toggle e account-menu.
 */
export function DashboardHeader() {
  const pathname = usePathname();
  const [q, setQ] = useQueryState('q', Q_PARSER);
  const hideValues = usePrivacyStore((s) => s.hideValues);
  const hydrated = usePrivacyStore((s) => s.hydrated);
  const setHideValues = usePrivacyStore((s) => s.setHideValues);
  const [open, setOpen] = React.useState(false);

  const toggle = React.useCallback(() => {
    setHideValues(hideValues !== true);
  }, [hideValues, setHideValues]);

  const masked = hideValues === true;

  return (
    <header className="sticky top-0 z-40 flex h-14 items-center gap-2 border-b bg-background/95 px-4 backdrop-blur supports-[backdrop-filter]:bg-background/60">
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetTrigger
          render={
            <Button
              variant="ghost"
              size="icon-sm"
              className="lg:hidden shrink-0"
              aria-label="Abrir navegação"
            />
          }
          className="lg:hidden"
        >
          <Menu className="size-4" />
        </SheetTrigger>
        <SheetContent side="left" className="w-72 p-0">
          <SheetHeader className="p-4 pb-2">
            <SheetTitle>IndexDesk</SheetTitle>
          </SheetHeader>
          <SidebarContent collapsed={false} onNavigate={() => setOpen(false)} />
        </SheetContent>
      </Sheet>

      {pathname === '/dashboard' && (
        <div className="relative min-w-0 flex-1 sm:max-w-64">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={q}
            onChange={(e) => void setQ(e.target.value)}
            placeholder="Buscar carteira…"
            aria-label="Buscar carteira por nome"
            className="h-8 pl-8 text-sm"
          />
        </div>
      )}

      <div className="ml-auto flex items-center gap-1">
        <Tooltip>
          <TooltipTrigger
            render={
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={toggle}
                disabled={!hydrated}
                className="shrink-0 text-muted-foreground disabled:opacity-60"
                aria-label={masked ? 'Mostrar valores (Ctrl+.)' : 'Esconder valores (Ctrl+.)'}
                aria-pressed={masked}
                title={masked ? 'Mostrar valores (Ctrl+.)' : 'Esconder valores (Ctrl+.)'}
              />
            }
          >
            {masked ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </TooltipTrigger>
          <TooltipContent>
            {masked ? 'Mostrar valores' : 'Esconder valores'} · Ctrl+.
          </TooltipContent>
        </Tooltip>
        <ThemeToggle />
        <AccountMenu />
      </div>
    </header>
  );
}

export { Q_PARSER as dashboardQParser };
