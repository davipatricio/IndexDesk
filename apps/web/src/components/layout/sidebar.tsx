'use client';

import * as React from 'react';
import { useSyncExternalStore } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { LayoutDashboard, Plus, TrendingUp, Wrench } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useSession } from '@/hooks/use-session';
import {
  portfolioInitial,
  portfolioAvatarStyle,
  usePortfoliosList,
} from '@/hooks/use-portfolios-list';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { ScrollArea } from '@/components/ui/scroll-area';
import { Skeleton } from '@/components/ui/skeleton';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

interface NavItem {
  href: string;
  label: string;
  icon: typeof LayoutDashboard;
}

const NAV_ITEMS: readonly NavItem[] = [
  { href: '/dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { href: '/rankings', label: 'Rankings', icon: TrendingUp },
  { href: '/ferramentas/backtest', label: 'Ferramentas', icon: Wrench },
];

const COLLAPSED_KEY = 'indexdesk.sidebar.collapsed';

/** Persistência SSR-safe do estado colapsado (localStorage, apenas no cliente). */
function readCollapsed(): boolean {
  if (typeof window === 'undefined') return false;
  return window.localStorage.getItem(COLLAPSED_KEY) === '1';
}

/**
 * Collapsed com persistência no localStorage (`indexdesk.sidebar.collapsed`).
 * SSR-safe: leitura/escrita só no cliente; usando useSyncExternalStore para
 * evitar set-state-in-effect e hidratação dividida.
 */
const collapsedListeners = new Set<() => void>();
function emitCollapsed() {
  for (const listener of collapsedListeners) listener();
}
function subscribeCollapsed(listener: () => void): () => void {
  collapsedListeners.add(listener);
  const onStorage = (event: StorageEvent) => {
    if (event.key === COLLAPSED_KEY) listener();
  };
  window.addEventListener('storage', onStorage);
  return () => {
    collapsedListeners.delete(listener);
    window.removeEventListener('storage', onStorage);
  };
}
function snapshotCollapsed(): boolean {
  return readCollapsed();
}

export function useSidebarCollapsed(): [boolean, (next: boolean) => void] {
  const collapsed = useSyncExternalStore(subscribeCollapsed, snapshotCollapsed, () => false);
  const toggle = React.useCallback((next: boolean) => {
    try {
      window.localStorage.setItem(COLLAPSED_KEY, next ? '1' : '0');
    } catch {
      // storage indisponível (privacidade/SSR) — mantém só no listener
    }
    emitCollapsed();
  }, []);
  return [collapsed, toggle];
}

interface SidebarContentProps {
  /** Modo compacto: só ícones com tooltip (sidebar fixa desktop). */
  collapsed: boolean;
  /** Chamado ao navegar — fecha o drawer mobile. */
  onNavigate?: () => void;
}

/**
 * Conteúdo único da navegação — usado pela sidebar fixa (desktop ≥ lg) e pelo
 * drawer mobile (Sheet). Nunca renderiza valores monetários: só nomes e links.
 */
export function SidebarContent({ collapsed, onNavigate }: SidebarContentProps) {
  const pathname = usePathname();
  const { account, isAuthenticated, isReady } = useSession();
  const { portfolios, isLoading } = usePortfoliosList();

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === '/dashboard' : pathname.startsWith(href);

  const navItem = (item: (typeof NAV_ITEMS)[number]): React.ReactNode => {
    const active = isActive(item.href);
    const content = (
      <Link
        href={item.href}
        onClick={onNavigate}
        aria-current={active ? 'page' : undefined}
        className={cn(
          'flex items-center rounded-md text-sm font-medium transition-colors',
          collapsed ? 'size-9 justify-center' : 'gap-2.5 px-3 py-2',
          active
            ? 'bg-sidebar-accent text-sidebar-accent-foreground'
            : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
        )}
      >
        <item.icon className="size-4 shrink-0" />
        {!collapsed && <span className="truncate">{item.label}</span>}
      </Link>
    );
    if (!collapsed) return content;
    return (
      <Tooltip>
        <TooltipTrigger render={content} />
        <TooltipContent side="right">{item.label}</TooltipContent>
      </Tooltip>
    );
  };

  const portfolioRow = (id: string, title: string): React.ReactNode => {
    const href = `/dashboard/c/${id}`;
    const active = pathname === href;
    const content = (
      <Link
        href={href}
        onClick={onNavigate}
        aria-current={active ? 'page' : undefined}
        className={cn(
          'flex min-w-0 items-center rounded-md text-sm transition-colors',
          collapsed ? 'size-9 justify-center' : 'gap-2.5 px-3 py-1.5',
          active
            ? 'bg-sidebar-accent text-sidebar-accent-foreground font-medium'
            : 'text-muted-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground',
        )}
      >
        <Avatar className="size-5 shrink-0" style={portfolioAvatarStyle(id)}>
          <AvatarFallback className="text-[10px] font-semibold">
            {portfolioInitial(title)}
          </AvatarFallback>
        </Avatar>
        {!collapsed && <span className="truncate">{title}</span>}
      </Link>
    );
    if (!collapsed) return content;
    return (
      <Tooltip>
        <TooltipTrigger render={content} />
        <TooltipContent side="right">{title}</TooltipContent>
      </Tooltip>
    );
  };

  return (
    <div className="flex h-full min-h-0 flex-col gap-4 p-3">
      <nav aria-label="Navegação principal" className="flex flex-col gap-1">
        {NAV_ITEMS.map((item) => navItem(item))}
      </nav>

      <div className="flex min-h-0 flex-1 flex-col gap-2">
        <div className={cn('flex items-center gap-2 pt-1', collapsed ? 'justify-center' : 'px-3')}>
          {!collapsed && (
            <span className="flex-1 text-[10px] font-semibold tracking-wider text-muted-foreground uppercase">
              Minhas carteiras
            </span>
          )}
          <Tooltip>
            <TooltipTrigger
              render={
                <Button
                  render={<Link href="/dashboard/carteiras/nova" />}
                  variant="ghost"
                  size="icon-sm"
                  aria-label="Nova carteira"
                />
              }
            >
              <Plus className="size-4" />
            </TooltipTrigger>
            {!collapsed && <TooltipContent>Nova carteira</TooltipContent>}
          </Tooltip>
        </div>

        {!collapsed && (
          <div className="px-1 text-xs">
            {isLoading ? (
              <div className="flex flex-col gap-2 px-2 py-1">
                <Skeleton className="h-5 w-3/4" />
                <Skeleton className="h-5 w-2/3" />
              </div>
            ) : !portfolios || portfolios.length === 0 ? (
              <p className="px-2 py-1 text-muted-foreground">
                {isReady && !isAuthenticated
                  ? 'Entre para ver suas carteiras.'
                  : 'Nenhuma carteira ainda.'}
              </p>
            ) : (
              <div className="flex flex-col gap-0.5">
                {portfolios.map((p) => portfolioRow(p.id, p.title))}
              </div>
            )}
          </div>
        )}

        {collapsed && (
          <ScrollArea className="min-h-0 flex-1">
            <div className="flex flex-col items-center gap-1">
              {(portfolios ?? []).map((p) => portfolioRow(p.id, p.title))}
            </div>
          </ScrollArea>
        )}
      </div>

      {!collapsed && isAuthenticated && account && (
        <div className="flex items-center gap-2 rounded-md bg-sidebar-accent/40 px-3 py-2">
          <Avatar size="sm">
            <AvatarFallback className="bg-primary/10 text-primary text-[10px] font-semibold">
              {account.initials}
            </AvatarFallback>
          </Avatar>
          <div className="min-w-0 flex-1">
            <p className="truncate text-xs font-medium text-foreground">{account.name}</p>
            <p className="truncate text-[11px] text-muted-foreground">{account.email}</p>
          </div>
        </div>
      )}
    </div>
  );
}
