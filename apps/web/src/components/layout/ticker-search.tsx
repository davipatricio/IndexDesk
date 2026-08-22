'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { useHotkey } from '@tanstack/react-hotkeys';
import { fetchAssets, type AssetDto } from '@/lib/api-client';
import { getAssetCategory, getAssetDetailHref } from '@/components/catalog/catalog-table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Search } from 'lucide-react';

const MAX_RESULTS = 8;

export function TickerSearch() {
  const router = useRouter();
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState('');
  const containerRef = React.useRef<HTMLDivElement>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);
  const { data, isLoading, isError } = useQuery({ queryKey: ['assets', 'search'], queryFn: () => fetchAssets(), enabled: open, staleTime: 60_000 });
  const assets = data ?? [];

  const toggle = React.useCallback(() => { setOpen((previous) => { if (!previous) setQuery(''); return !previous; }); }, []);
  React.useEffect(() => { if (open) inputRef.current?.focus(); }, [open]);
  React.useEffect(() => { if (!open) return; const onPointerDown = (event: PointerEvent) => { if (containerRef.current && !containerRef.current.contains(event.target as Node)) setOpen(false); }; document.addEventListener('pointerdown', onPointerDown); return () => document.removeEventListener('pointerdown', onPointerDown); }, [open]);
  useHotkey('Mod+K', toggle, { preventDefault: true });

  const normalized = query.trim().toLowerCase();
  const results = (normalized ? assets.filter((asset) => [asset.ticker, asset.name, (asset as unknown as { benchmark?: string; benchmarkSymbol?: string }).benchmarkSymbol ?? (asset as unknown as { benchmark?: string }).benchmark ?? ''].filter(Boolean).some((value) => value.toLowerCase().includes(normalized))) : assets).slice(0, MAX_RESULTS);
  const select = (asset: AssetDto) => { setOpen(false); setQuery(''); router.push(getAssetDetailHref(asset)); };

  return <div ref={containerRef} className="relative"><Button variant="ghost" size="sm" onClick={toggle} aria-expanded={open} aria-haspopup="dialog" aria-label="Buscar ativo" className="gap-1.5 text-muted-foreground hover:text-foreground"><Search className="size-3.5" /><span className="hidden sm:inline">Buscar</span><kbd className="hidden items-center rounded border bg-muted px-1.5 py-0.5 font-mono text-[10px] font-medium text-muted-foreground lg:inline-flex">Ctrl K</kbd></Button>{open ? <div className="absolute right-0 top-full z-50 mt-2 w-80 max-w-[calc(100vw-2rem)] rounded-lg bg-popover p-1 text-popover-foreground shadow-md ring-1 ring-foreground/10"><div className="px-1.5 py-1"><Input ref={inputRef} value={query} onChange={(event) => setQuery(event.target.value)} onKeyDown={(event) => { if (event.key === 'Escape') setOpen(false); if (event.key === 'Enter' && results[0]) select(results[0]); }} placeholder="Buscar ticker, nome ou benchmark..." aria-label="Buscar por ticker, nome ou benchmark" className="h-8 text-xs" /></div><ul className="max-h-72 divide-y divide-border/40 overflow-y-auto p-1">{isLoading ? <li className="px-2 py-4 text-center text-xs text-muted-foreground">Carregando catálogo...</li> : isError ? <li className="px-2 py-4 text-center text-xs text-destructive">Não foi possível carregar o catálogo.</li> : results.length ? results.map((asset) => <li key={asset.ticker}><button type="button" onClick={() => select(asset)} className="flex w-full items-center justify-between gap-2 rounded-md px-2 py-1.5 text-left text-sm hover:bg-accent focus-visible:bg-accent focus-visible:outline-none"><span className="flex min-w-0 flex-col"><span className="flex items-center gap-1.5 font-mono text-xs font-semibold text-foreground">{asset.ticker}<Badge variant="secondary" className="px-1 py-0 text-[9px]">{getAssetCategory(asset)}</Badge></span><span className="truncate text-[11px] text-muted-foreground">{asset.name}</span></span><span className="shrink-0 text-[11px] text-muted-foreground">{((asset as unknown as { benchmark?: string; benchmarkSymbol?: string }).benchmarkSymbol ?? (asset as unknown as { benchmark?: string }).benchmark ?? '') || '—'}</span></button></li>) : <li className="px-2 py-4 text-center text-xs text-muted-foreground">Nenhum ativo encontrado para “{query}”.</li>}</ul></div> : null}</div>;
}
