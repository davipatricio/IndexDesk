'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';
import { useHotkey } from '@tanstack/react-hotkeys';
import { ALL_CATALOG_ASSETS, type CatalogAsset } from '@/lib/mock-catalog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Search } from 'lucide-react';

const MAX_RESULTS = 8;

function getAssetDetailHref(asset: CatalogAsset): string {
  const ticker = asset.ticker.toLowerCase();
  switch (asset.category) {
    case 'ETF':
      return `/etf/${ticker}`;
    case 'FII':
      return `/fii/${ticker}`;
    case 'BDR':
      return `/bdr/${ticker}`;
  }
}

/**
 * Header ticker search.
 *
 * Searches the asset catalog (ETFs, FIIs, BDRs) by ticker, name,
 * manager or benchmark/underlying asset, and navigates to the dedicated
 * asset detail page on selection. `Mod+K` (Ctrl/Cmd+K) toggles it.
 */
export function TickerSearch() {
  const router = useRouter();
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const containerRef = React.useRef<HTMLDivElement>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);

  const toggle = React.useCallback(() => {
    setOpen((prev) => {
      if (!prev) setQuery('');
      return !prev;
    });
  }, []);

  // Focus the input whenever the panel opens.
  React.useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  // Close when the user clicks/taps outside the control.
  React.useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [open]);

  useHotkey('Mod+K', toggle, { preventDefault: true });

  const q = query.trim().toLowerCase();
  const results = (
    q
      ? ALL_CATALOG_ASSETS.filter(
          (asset) =>
            asset.ticker.toLowerCase().includes(q) ||
            asset.name.toLowerCase().includes(q) ||
            asset.manager.toLowerCase().includes(q) ||
            ('benchmark' in asset && asset.benchmark.toLowerCase().includes(q)) ||
            ('underlyingAsset' in asset && asset.underlyingAsset.toLowerCase().includes(q)),
        )
      : ALL_CATALOG_ASSETS
  ).slice(0, MAX_RESULTS);

  const select = (asset: CatalogAsset) => {
    setOpen(false);
    setQuery('');
    router.push(getAssetDetailHref(asset));
  };

  return (
    <div ref={containerRef} className="relative">
      <Button
        variant="ghost"
        size="sm"
        onClick={toggle}
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-label="Buscar ativo"
        className="gap-1.5 text-muted-foreground hover:text-foreground"
      >
        <Search className="size-3.5" />
        <span className="hidden sm:inline">Buscar</span>
        <kbd className="hidden lg:inline-flex items-center rounded border bg-muted px-1.5 py-0.5 font-mono text-[10px] font-medium text-muted-foreground">
          Ctrl K
        </kbd>
      </Button>

      {open && (
        <div className="absolute right-0 top-full mt-2 w-80 max-w-[calc(100vw-2rem)] rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10 z-50">
          <div className="px-1.5 py-1">
            <Input
              ref={inputRef}
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Escape') setOpen(false);
                if (event.key === 'Enter' && results[0]) select(results[0]);
              }}
              placeholder="Buscar ticker, ETF, FII ou BDR..."
              aria-label="Buscar por ticker, nome ou gestora"
              className="text-xs h-8"
            />
          </div>
          <ul className="max-h-72 overflow-y-auto p-1 divide-y divide-border/40">
            {results.length ? (
              results.map((asset) => (
                <li key={asset.ticker}>
                  <button
                    type="button"
                    onClick={() => select(asset)}
                    className="flex w-full items-center justify-between gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-accent focus-visible:bg-accent focus-visible:outline-none"
                  >
                    <span className="flex min-w-0 flex-col">
                      <span className="font-semibold text-foreground font-mono text-xs flex items-center gap-1.5">
                        {asset.ticker}
                        <Badge variant="secondary" className="text-[9px] py-0 px-1 font-sans">
                          {asset.category}
                        </Badge>
                      </span>
                      <span className="truncate text-[11px] text-muted-foreground">
                        {asset.name}
                      </span>
                    </span>
                    <span className="shrink-0 text-[11px] text-muted-foreground font-mono">
                      {asset.subCategory}
                    </span>
                  </button>
                </li>
              ))
            ) : (
              <li className="px-2 py-4 text-center text-xs text-muted-foreground">
                Nenhum ativo encontrado para &ldquo;{query}&rdquo;.
              </li>
            )}
          </ul>
        </div>
      )}
    </div>
  );
}
