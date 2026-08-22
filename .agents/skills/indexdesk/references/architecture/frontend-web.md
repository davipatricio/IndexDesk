# Frontend — apps/web (Next.js 16.3 SSR-First)

Suplemento de `apps/web/CLAUDE.md` (fonte detalhada). Produto: catálogo, comparador, calculadoras,
backtest, hubs de notícias/relatórios e `/admin`.

## Stack real (pinada no package.json)

Bun 1.4 · Next.js **16.3.1** (App Router, `src/`, Turbopack dev+build) · React **19.2.4** ·
TypeScript ^7.0.2 (`module: preserve`, `moduleResolution: bundler`, `noEmit`, `verbatimModuleSyntax`) ·
Oxlint ^1.79 / Oxfmt ^0.64 · Serwist 9 (`@serwist/turbopack`) · Tailwind v4 (CSS-first em `globals.css`) ·
shadcn style **base-nova** sobre `@base-ui/react` · TanStack: query ^5.101, table ^9.1, form ^1.33,
virtual ^3.14, hotkeys ^0.10, store/db · nuqs ^2.9 · zustand ^5 · Vitest ^3 + RTL + jsdom.

## Padrão SSR-First (SEO-critical)

1. Server Component (`page.tsx`): `prefetchQuery` interno → `dehydrate(queryClient)`.
2. Client Component: `<HydrationBoundary state={...}>` + `useQuery()`.
3. HTML inicial completo para crawlers, sem layout shift. Providers em `src/app/providers.tsx`
   (QueryClientProvider + NuqsAdapter); singleton em `src/lib/query-client.ts`.

## api-client (`src/lib/api-client.ts`) — regras NON-NEGOTIABLE

- Base URL `NEXT_PUBLIC_API_URL` (default `http://localhost:5000`), rotas `/api/v1/*`.
- ISR por rota via `fetch(..., { next: { revalidate } })`: lista de ativos 60s, ativo único 300s.
- DTOs tipados alinhados ao OpenAPI (`AssetDto`, `AssetDetailDto`, `QuoteItem`, `PerformanceResponse`,
  `BacktestRequest/Response`, `RealYieldResponse`). Não "alargar" com campos mock.
- **API-only data source:** nunca importar fixture/sintetizar ativo desconhecido/calcular resposta
  substituta quando a API falha. Array vazio da API = estado vazio válido. Erros de transporte/HTTP viram
  `Error` → error boundaries e estados honestos de carregado/vazio/erro/retry.
- Novo dado = novo endpoint no backend; **nunca** chamada direta a provedor.

## Rotas

- `(public)/`: `/etf/[ticker]`, `/bdr/[ticker]`, `/comparador`, `/ferramentas/*`, `/noticias`, `/relatorios`.
- `(admin)/`: `/admin/assets`, `/admin/holdings`, `/admin/sync-jobs`.
- `~offline` fallback; `sw.ts` source do Serwist.

## PWA (Serwist + Turbopack) — não trocar por next-pwa/workbox

- Asset sheets `/etf/`,`/bdr/`: StaleWhileRevalidate (100 entradas, 24h).
- Tools/comparador: CacheFirst (50 entradas, 7d). `navigationPreload/skipWaiting/clientsClaim: true`.

## Gráficos (mapeamento fixo)

Séries temporais/equity/drawdown → `lightweight-charts` (Canvas ~60fps) ·
Alocação/top-10/donut → `recharts` (SVG, tokens Tailwind) · Venn overlap/heatmap correlação → `@visx/group+shape`.

## UI & convenções

- shadcn CLI resolve para base-nova: `bunx shadcn@latest add <component>` → `src/components/ui`.
  Não hand-rollar primitivo que o shadcn fornece.
- Aliases: `@/components`, `@/components/ui`, `@/lib`, `@/lib/utils`, `@/hooks`. Nunca paths relativos entre pastas.
- `cn()` de `@/lib/utils`; variantes com CVA; ícones lucide-react; animações tw-animate-css.
- Testes co-localizados (`*.test.ts(x)` ou `src/lib/__tests__/`); `bun test` / `test:coverage`.

## Idioma e visibilidade (resumo — detalhes em ../process/conventions.md)

pt-BR natural ao usuário, sem jargão técnico nem IDs de roadmap; erros mapeados para mensagens estáveis;
todas as páginas públicas (middleware pass-through, sem gate de auth); sem fixtures em páginas públicas.
