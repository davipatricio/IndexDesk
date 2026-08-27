'use client';

import * as React from 'react';
import Link from 'next/link';
import { useQueryState, parseAsString } from 'nuqs';
import { ChevronsLeft, ChevronsRight } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useSession } from '@/hooks/use-session';
import { usePrivacyStore } from '@/stores/privacy-store';
import { useCurrentUserQuery } from '@/hooks/use-auth-queries';
import { useSessionStore } from '@/stores/session-store';
import {
  portfolioInitial,
  portfolioAvatarStyle,
  usePortfoliosList,
} from '@/hooks/use-portfolios-list';
import { useSidebarCollapsed, SidebarContent } from '@/components/layout/sidebar';
import { DashboardHeader } from '@/components/layout/dashboard-header';
import { PrivacyHotkey } from '@/components/layout/privacy-hotkey';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Skeleton } from '@/components/ui/skeleton';

const Q_PARSER = parseAsString.withDefault('');

/**
 * Espelha os dados do /me para a store de privacidade (preferências + papel de
 * autenticação) e mantém o cache de sessão alinhado com o perfil fresco.
 */
function DashboardPrivacyProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isReady } = useSession();
  const initFromPreferences = usePrivacyStore((s) => s.initFromPreferences);
  const setAuthenticated = usePrivacyStore((s) => s.setAuthenticated);
  const meQuery = useCurrentUserQuery({ enabled: isReady && isAuthenticated });
  const setUser = useSessionStore((s) => s.setUser);

  React.useEffect(() => {
    if (!isReady) return;
    setAuthenticated(isAuthenticated);
  }, [isReady, isAuthenticated, setAuthenticated]);

  React.useEffect(() => {
    const fresh = meQuery.data;
    if (!fresh) return;
    setUser(fresh);
    initFromPreferences(fresh.preferences);
  }, [meQuery.data, initFromPreferences, setUser]);

  return <>{children}</>;
}

/** Link de carteira com avatar-inicial de cor determinística (hash do id). */
function PortfolioLink({ id, title }: { id: string; title: string }) {
  return (
    <Link
      href={`/dashboard/c/${id}`}
      className="flex min-w-0 items-center gap-2.5 rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground"
    >
      <Avatar className="size-5 shrink-0" style={portfolioAvatarStyle(id)}>
        <AvatarFallback className="text-[10px] font-semibold">
          {portfolioInitial(title)}
        </AvatarFallback>
      </Avatar>
      <span className="truncate">{title}</span>
    </Link>
  );
}

interface SidebarSectionProps {
  collapsed: boolean;
  onToggleCollapsed: (next: boolean) => void;
  q: string;
}

/**
 * Coluna fixa desktop (≥ lg). Com `q` preenchido vira lista de resultados da
 * busca (mesmo estado `q` lido pela tabela de carteiras); sem `q`, navegação
 * completa (SidebarContent).
 */
function SidebarSection({ collapsed, onToggleCollapsed, q }: SidebarSectionProps) {
  const { portfolios, isLoading } = usePortfoliosList();
  const term = q.trim().toLowerCase();
  const filtered = React.useMemo(() => {
    const list = portfolios ?? [];
    if (!term) return list;
    return list.filter((p) => p.title.toLowerCase().includes(term));
  }, [portfolios, term]);

  return (
    <aside
      data-collapsed={collapsed}
      className={cn(
        'sticky top-0 z-30 hidden h-svh shrink-0 flex-col border-r bg-sidebar text-sidebar-foreground transition-[width] duration-150 lg:flex',
        collapsed ? 'w-14' : 'w-60',
      )}
    >
      <div className="flex h-14 shrink-0 items-center border-b px-3">
        <Link
          href="/dashboard"
          aria-label="Ir para o dashboard"
          className={cn(
            'flex items-center gap-2 text-sm font-semibold tracking-tight text-foreground',
            collapsed && 'w-full justify-center',
          )}
        >
          <span className="flex size-6 shrink-0 items-center justify-center rounded-md bg-primary text-[11px] font-bold text-primary-foreground">
            ID
          </span>
          {!collapsed && <span>IndexDesk</span>}
        </Link>
      </div>

      {term ? (
        <div className="min-h-0 flex-1 overflow-y-auto p-3 text-sm">
          <p className="mb-2 px-1 text-[10px] font-semibold tracking-wider text-muted-foreground uppercase">
            Carteiras com “{q.trim()}”
          </p>
          {isLoading ? (
            <div className="flex flex-col gap-2 px-1">
              <Skeleton className="h-5 w-3/4" />
              <Skeleton className="h-5 w-2/3" />
            </div>
          ) : filtered.length === 0 ? (
            <p className="px-1 text-muted-foreground">Nenhuma carteira encontrada.</p>
          ) : (
            <div className="flex flex-col gap-0.5">
              {filtered.map((p) => (
                <PortfolioLink key={p.id} id={p.id} title={p.title} />
              ))}
            </div>
          )}
        </div>
      ) : (
        <SidebarContent collapsed={collapsed} />
      )}

      <div className={cn('flex items-center border-t p-2', collapsed && 'justify-center')}>
        <Tooltip>
          <TooltipTrigger
            render={
              <Button
                variant="ghost"
                size="icon-sm"
                onClick={() => onToggleCollapsed(!collapsed)}
                aria-label={collapsed ? 'Expandir menu lateral' : 'Recolher menu lateral'}
              />
            }
          >
            {collapsed ? <ChevronsRight className="size-4" /> : <ChevronsLeft className="size-4" />}
          </TooltipTrigger>
          {!collapsed && <TooltipContent side="right">Recolher menu</TooltipContent>}
        </Tooltip>
      </div>
    </aside>
  );
}

/**
 * Shell do dashboard: sidebar fixa + header (busca `q`, toggle de privacidade,
 * tema, conta) + conteúdo. Navbar/Footer públicos vivem fora deste grupo.
 */
export function DashboardShell({ children }: { children: React.ReactNode }) {
  const [collapsed, toggleCollapsed] = useSidebarCollapsed();
  const [q] = useQueryState('q', Q_PARSER);

  return (
    <PrivacyHotkey>
      <DashboardPrivacyProvider>
        <TooltipProvider delay={200}>
          <div className="flex min-h-svh w-full">
            <SidebarSection collapsed={collapsed} onToggleCollapsed={toggleCollapsed} q={q} />
            <div className="flex min-w-0 flex-1 flex-col">
              <DashboardHeader />
              <main className="flex-1">{children}</main>
            </div>
          </div>
        </TooltipProvider>
      </DashboardPrivacyProvider>
    </PrivacyHotkey>
  );
}
