'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useHotkey } from '@tanstack/react-hotkeys';
import { fetchAssets } from '@/lib/api-client';
import { getAssetCategory, getAssetDetailHref } from '@/components/catalog/catalog-table';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from '@/components/ui/command';
import {
  BarChart3,
  ArrowRightLeft,
  Search,
  TrendingUp,
  Table2,
  Percent,
  Layers,
} from 'lucide-react';

const PAGES = [
  { href: '/ativos', label: 'Explorar ativos', icon: Table2, shortcut: '' },
  { href: '/rankings', label: 'Rankings', icon: TrendingUp, shortcut: '' },
  { href: '/comparador', label: 'Comparador', icon: ArrowRightLeft, shortcut: '' },
  {
    href: '/ferramentas/backtest',
    label: 'Simulador de backtest',
    icon: BarChart3,
    shortcut: '',
  },
  {
    href: '/ferramentas/rendimento-real',
    label: 'Rendimento real',
    icon: Percent,
    shortcut: '',
  },
] as const;

const MAX_ASSETS = 8;

export function TickerSearch() {
  const router = useRouter();
  const [open, setOpen] = React.useState(false);

  useHotkey('Mod+K', () => setOpen((previous) => !previous), { preventDefault: true });

  const { data, isLoading, isError } = useQuery({
    queryKey: ['assets', 'search'],
    queryFn: () => fetchAssets(),
    enabled: open,
    staleTime: 60_000,
  });
  const assets = React.useMemo(() => (data ?? []).slice(0, MAX_ASSETS), [data]);

  const go = React.useCallback(
    (href: string) => {
      setOpen(false);
      router.push(href);
    },
    [router],
  );

  return (
    <>
      <Button
        variant="ghost"
        size="sm"
        onClick={() => setOpen(true)}
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-label="Buscar página ou ativo"
        className="gap-1.5 text-muted-foreground hover:text-foreground"
      >
        <Search className="size-3.5" />
        <span className="hidden sm:inline">Buscar</span>
        <kbd className="hidden items-center rounded border bg-muted px-1.5 py-0.5 font-mono text-[10px] font-medium text-muted-foreground lg:inline-flex">
          Ctrl K
        </kbd>
      </Button>

      <CommandDialog open={open} onOpenChange={(nextOpen) => setOpen(nextOpen)}>
        <CommandInput placeholder="Buscar ferramenta ou ticker…" />
        <CommandList>
          {isLoading ? (
            <p className="px-3 py-6 text-center text-sm text-muted-foreground">
              Carregando catálogo…
            </p>
          ) : null}
          {!isLoading && !isError ? (
            <>
              <CommandEmpty>Nada encontrado para essa busca.</CommandEmpty>
              <CommandGroup heading="Navegação">
                {PAGES.map((page) => (
                  <CommandItem
                    key={page.href}
                    value={`${page.label} ${page.href}`}
                    onSelect={() => go(page.href)}
                  >
                    <page.icon className="text-muted-foreground" />
                    {page.label}
                  </CommandItem>
                ))}
              </CommandGroup>
              {assets.length > 0 ? (
                <>
                  <CommandSeparator />
                  <CommandGroup heading="Ativos">
                    {assets.map((asset) => (
                      <CommandItem
                        key={asset.ticker}
                        value={`${asset.ticker} ${asset.name}`}
                        onSelect={() => go(getAssetDetailHref(asset))}
                      >
                        <Layers className="text-muted-foreground" />
                        <span className="font-mono text-xs font-semibold">{asset.ticker}</span>
                        <Badge variant="secondary" className="px-1 py-0 text-[9px]">
                          {getAssetCategory(asset)}
                        </Badge>
                        <span className="truncate text-xs text-muted-foreground">{asset.name}</span>
                      </CommandItem>
                    ))}
                  </CommandGroup>
                </>
              ) : null}
            </>
          ) : null}
          {!isLoading && isError ? (
            <p className="px-3 py-6 text-center text-sm text-destructive">
              Não foi possível carregar o catálogo agora.
            </p>
          ) : null}
        </CommandList>
        {PAGES[0] ? (
          <div className="border-t px-3 py-2">
            <CommandShortcut>Enter abre · Esc fecha</CommandShortcut>
          </div>
        ) : null}
      </CommandDialog>
    </>
  );
}
