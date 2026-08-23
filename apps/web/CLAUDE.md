# apps/web — Frontend Instructions (scoped)

> Scoped supplement to root [`CLAUDE.md`](../../CLAUDE.md) and [`STACK_SETUP.md`](../../STACK_SETUP.md).
> This file covers the Next.js 16.3 app (`@indexdesk/web`). For product/SEO/data-provider
> context, defer to the root docs. **Do not modify code** when only instruction updates are requested —
> edits here are markdown only.
>
> When your work changes implemented features/routes/conventions here, also update the repo skill
> `.agents/skills/indexdesk/references/process/current-features.md` (see "Repo Skill" section in root `CLAUDE.md`).

## 1. What this workspace is

A **SSR-First** Next.js 16.3 App Router app (`src/` directory) for the B3 ETF intelligence platform
(product name in docs: _ETFHub B3_; code/branding: _IndexDesk_). It renders catalog pages (`/etf/[ticker]`,
`/bdr/[ticker]`), comparators, calculators, backtest simulator, a News/Reports hub, and an `/admin` backoffice.
All data is fetched from the .NET API (`apps/backend`) — **never call external providers directly** (see Local-First §6).

## 2. Runtime & tooling (real versions, pinned in `package.json`)

| Concern                      | Tool / Version                                                                                                             | Notes                                                                                           |
| :--------------------------- | :------------------------------------------------------------------------------------------------------------------------- | :---------------------------------------------------------------------------------------------- |
| Package manager              | **Bun** (`bun@1.4.0` at root)                                                                                              | Do not use npm/yarn/pnpm. Run `bun` from repo root.                                             |
| Framework                    | **Next.js 16.3.1** (App Router, `src/`)                                                                                    | Turbopack for dev + build.                                                                      |
| React                        | **19.2.4**                                                                                                                 | RSC by default; `"use client"` only where interactivity is required.                            |
| Language                     | **TypeScript ^7.0.2**                                                                                                      | `module: preserve`, `moduleResolution: bundler`, `noEmit`, `verbatimModuleSyntax`.              |
| Lint                         | **Oxlint ^1.79.0**                                                                                                         | `oxlint.config.ts` / `.oxlintrc.json` at repo root; `--type-aware` only if CI cost is measured. |
| Format                       | **Oxfmt ^0.64.0**                                                                                                          | Official formatter for JS/TS/JSON/MD in this workspace.                                         |
| PWA                          | **Serwist 9** (`@serwist/next`, `@serwist/turbopack`, `serwist`)                                                           | Turbopack-native service worker.                                                                |
| UI                           | **Base UI Nova** = shadcn `style: "base-nova"` over **`@base-ui/react ^1.7.0`**                                            | Added components live in `src/components/ui`. See §4.                                           |
| Styling                      | **Tailwind CSS v4** (`@tailwindcss/postcss`)                                                                               | Config lives in `src/app/globals.css` (CSS-first).                                              |
| Charts                       | **`lightweight-charts ^5.2.1`** (Canvas/financial), **`recharts ^3.10.1`** (SVG/donut), **`@visx/* ^3.12`** (Venn/heatmap) |                                                                                                 |
| Server-state                 | **`@tanstack/react-query ^5.101`** + Devtools                                                                              |                                                                                                 |
| Tables/Forms/Virtual/Hotkeys | **`@tanstack/react-table ^9.1`**, **`react-form ^1.33`**, **`react-virtual ^3.14`**, **`react-hotkeys ^0.10`**             |                                                                                                 |
| URL state                    | **`nuqs ^2.9.6`** (wrapped by `NuqsAdapter` in `providers.tsx`)                                                            |                                                                                                 |
| Client state                 | **`zustand ^5.0.15`** + **`@tanstack/store` / `@tanstack/db`** (offline layer)                                             |                                                                                                 |
| Registry extras              | `calligraph ^1.4`, `motion ^13`                                                                                            | Deps pulled by the vendored `@kinetic/scrub-number-field`; do not import directly in app code.  |
| Tests                        | **Vitest ^3.0.7** + Testing Library + `jsdom`                                                                              | `vitest.config.ts`, `vitest.setup.ts`.                                                          |

## 3. Commands (run from repo root via Turborepo, or `cd apps/web`)

```bash
# Whole monorepo (preferred) — Turborepo fans out per workspace:
bun run dev            # next dev --turbopack  + backend dotnet watch
bun run build          # next build (Turbopack) + dotnet build -c Release
bun run lint           # oxlint . (web) + dotnet format analyzers (backend)
bun run typecheck      # tsc --noEmit (web) + dotnet build/analyzers (backend)
bun run test           # vitest run (web) + dotnet test (backend)
bun run format         # oxfmt . (web) + dotnet csharpier format (backend)
bun run format:check   # CI gate: oxfmt --check . + csharpier check

# Web-only (cd apps/web):
bun dev                # next dev --turbopack
bun build              # next build
bun start              # next start
bun lint               # oxlint .
bun format             # oxfmt .
bun format:check       # oxfmt --check .
bun typecheck          # tsc --noEmit
bun test               # vitest run
bun test:watch         # vitest
bun test:coverage      # vitest run --coverage
```

> Turbo caches `build` outputs (`.next/**`) and `test` outputs (`coverage/**`). `dev` and `format` are
> non-cached/persistent. `globalDependencies` include `.env*`, `tsconfig.json`, `Directory.Build.props`,
> `.editorconfig` — changing those invalidates caches.

## 4. UI components — Base UI Nova (shadcn `base-nova`)

- `components.json` sets `"style": "base-nova"`, `rsc: true`, `tailwind.css: "src/app/globals.css"`,
  `iconLibrary: "lucide"`, `cssVariables: true`.
- **Add new primitives** with the shadcn CLI: `bunx shadcn@latest add <component>` — they resolve to
  `@base-ui/react` components and land in `src/components/ui`. Do not hand-roll primitives that shadcn provides.
- Path aliases (from `components.json` / `tsconfig.json`): `@/components`, `@/components/ui`, `@/lib`,
  `@/lib/utils`, `@/hooks`. Always import via `@/`, never relative paths across folders.
- `src/lib/utils.ts` exports `cn()` (clsx + tailwind-merge). Variants use `class-variance-authority`.
- Animations: `tw-animate-css`. Icons: `lucide-react` (already in `optimizePackageImports`).

### 4.1 Third-party registries (policy)

Official shadcn already ships **Base UI variants** for every core primitive (`ui.shadcn.com/docs/components/base/*`);
with `style: "base-nova"` the plain CLI form resolves to them. Only reach for a third-party registry when the
official set lacks the component.

**Primitive rule:** this app standardizes on **`@base-ui/react`**. Never install _interactive_ components built on
Radix UI or other primitive libs — they duplicate primitives, split focus/a11y behavior and add bundle weight.
Visual-only registries (charts on Recharts, OG images on Satori) carry no primitive and are exempt.

**Priority order**

1. Official: `bunx shadcn@latest add <name>` (resolves base-nova / Base UI).
2. Base UI-native registries (`registries` map in `components.json`): `@kinetic`, `@basecn`, `@coss`, `@lumiui`.
3. Visual-only: `@evilcharts` (Recharts-styled), `@ogimagecn` (Satori OG images).
4. `@reui` — lookup/reference only for patterns; do **not** install as a dependency source by default.

**Approved registry items and their destination**

| Registry item                                | Destination here                                                                            | Status                                     |
| :------------------------------------------- | :------------------------------------------------------------------------------------------ | :----------------------------------------- |
| `@kinetic/scrub-number-field`                | Numeric inputs in backtest/calculators (`ScrubNumberField`)                                 | Installed; used by `terminal-backtest.tsx` |
| `@evilcharts/*` (area/donut/bar)             | Allocation donut + comparison bars when those surfaces are built                            | Approved, install on first render site     |
| `@kibo-ui/file-upload`                       | Admin CSV holdings upload (MVP-016)                                                         | Approved, install with the admin feature   |
| `@dsikeres1/*` date-range picker             | Backtest/comparador period selection if native dates fall short                             | Candidate                                  |
| `@ogimagecn/*`                               | Dynamic per-ticker OG images (MVP-022)                                                      | Approved                                   |
| Rich-text editor for admin reports (MVP-017) | Decide between `@prosekit` (lighter) and `@shadcn-editor` (Lexical); avoid `@plate` (heavy) | Open decision                              |
| `@lytenyte` grid                             | Only if TanStack Table + react-virtual cannot handle 10y+ daily quote tables                | Fallback                                   |

**Banned categories:** animation libraries (`@magicui`, `@aceternity`, `@animate-ui`, `@react-bits` — violate the
project MOTION ≤ 3 / anti-slop rules), template dashboards (`@bundui`, `@shadcnblocks`, …), ready-made theme packs,
AI/chat/billing/auth/maps/web3 collections (no product use case; auth is our own JWT backend).

**Rules of engagement**

- Install an item only when its render site exists — no dead components parked in `src/components/ui`.
- Vendored files are ours: fix real bugs in place (e.g. strict-mode fixes) and silence intentional patterns via
  targeted `overrides` in the root `.oxlintrc.json` (see the `scrub-number-*` / `use-controllable-state` block),
  never blanket ignores.
- Review the installed source at add time (registry code runs in our bundle).

## 5. Architecture & conventions

### SSR-First + React Query hydration (SEO-critical)

1. **Server Component** (`src/app/**/page.tsx`): call `prefetchQuery` internally and `dehydrate(queryClient)`.
2. **Client Component**: wrap in `<HydrationBoundary state={dehydratedState}>` and consume via `useQuery()`.
3. Result: full HTML for crawlers, no layout shift on hydration. `src/app/providers.tsx` wires
   `QueryClientProvider` + `NuqsAdapter` (+ Devtools in dev). `src/lib/query-client.ts` holds the singleton.

### Data fetching client (`src/lib/api-client.ts`)

- Typed DTOs aligned with the backend contract: `AssetDto`, `AssetDetailDto` (+ nested
  `stats`/`fiscal` blocks), `QuoteItem`, `PerformanceResponse`, `AssetRankingDto` +
  `RANKING_METRICS` whitelist, `MarketIndicatorDto`, `AssetDividendsDto`, `BacktestRequest/Response`,
  `RealYieldResponse`, plus spark/series types (`QuoteSparkPointDto`, `MacroRatePointDto`). Do not
  widen them with page-specific mock fields.
- Base URL: `process.env.NEXT_PUBLIC_API_URL` (server default `http://127.0.0.1:5000`; in-browser
  default is same-origin, served by the dev rewrite). API routes are `/api/v1/*`.
- ISR per fetcher via `fetch(..., { next: { revalidate } })`: asset list 60 · detail/rankings 300 ·
  quotes/performance/sparks 900 · dividends 1800 · market-indicators/macro-series 3600 (seconds).
- Auth flow lives here too: access token kept **in memory only** (`get/setAccessToken`), refresh via
  HttpOnly cookie (`POST /api/v1/auth/refresh`, single-flight `performRefresh`), `fetchWithAuth`
  retries once after refresh on `401`.
- **API-only data source (NON-NEGOTIABLE):** market-data and analytics fetchers must call the local
  .NET API and must not import fixture catalogs, synthesize unknown assets, or calculate replacement
  responses when the request fails. Preserve an empty array returned by the API as a valid empty state.
- Fetchers must surface transport and HTTP failures as `Error` values so route-level error boundaries and
  client query states can render a truthful error/retry UI. A `404` for an asset/detail/quote/performance
  endpoint is not a successful mock response — `fetchAssetDividends` is the one deliberate exception:
  `404` maps to `null` ("never paid locally" is a valid state).
- Authentication may retain its explicitly documented session behavior, but market-data fallbacks must
  never be used to make a catalog or asset page appear populated while the API is unavailable.

### Market-data consumption map (all local-first)

| Surface                                                                 | Fetchers / endpoints                                                                                                                                      |
| :---------------------------------------------------------------------- | :-------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Catalog `/ativos`, home movers, rankings rows, tooltip sparklines       | `fetchAssets`, `fetchAssetRankings`, `fetchQuoteSparks` → `/api/v1/assets[... ]`, `/rankings`, `/quotes/batch`                                            |
| Detail sheet `/ativos/[ticker]` (re-exported by `/etf`, `/bdr`, `/fii`) | `fetchAssetDetail` + `fetchAssetQuotes(days: 7300)` + `fetchAssetDividends`; JSON-LD `FinancialProduct` + `generateMetadata` from the same detail payload |
| "What if" simulation on detail page                                     | `fetchQuoteSparks(['IBOV','IFIX'])` + `fetchMacroRateSeries(['CDI'])` — benchmark curves accumulated client-side from local series                        |
| Backtest `/ferramentas/backtest`                                        | `POST /api/v1/analytics/backtest`                                                                                                                         |
| Real yield `/ferramentas/rendimento-real`                               | `GET /api/v1/analytics/real-yield`                                                                                                                        |

- React Query wrappers with normalized uppercase tickers live in `src/hooks/use-asset-queries.ts`
  (`assetKeys` factory; staleTime 5 min detail / 15 min quotes+performance).
- **No web consumer of `GET /api/v1/providers/health` exists yet** — provider health is backend-only
  today (`ProviderHealthService`). Add a fetcher + surface deliberately instead of assuming one exists.
- OG images per ticker are **metadata text only** today (`openGraph.title/description`); no dynamic OG
  image route is built (the `@ogimagecn` registry item remains approved-but-uninstalled).

### App routing layout

- `src/app/(public)/ativos/page.tsx` — asset catalog (`asset-explorer`). **`src/app/(public)/ativos/[ticker]/page.tsx`
  is the canonical detail page**; `/etf/[ticker]`, `/bdr/[ticker]` and `/fii/[ticker]` are thin
  re-exports of it (`export { default, generateMetadata, instant }`) so one implementation serves
  all asset classes. Do not fork them — extend the canonical page.
- Other public routes: `/comparador`, `/ferramentas/backtest`, `/ferramentas/rendimento-real`,
  `/rankings`, `/entrar` (auth).
- `src/app/(admin)/admin/page.tsx` — backoffice entry. The granular `/admin/assets|holdings|sync-jobs`
  pages and the news/reports hubs (`/noticias`, `/relatorios`) are planned but do not exist yet —
  check the tree before referencing them.
- `src/app/~offline` — offline fallback route. `src/app/sw.ts` — Serwist worker source.
- Other: `layout.tsx`, `page.tsx`, `loading.tsx`, `error.tsx`, `not-found.tsx`, `globals.css`, `manifest.json`.

### Next config flags (`next.config.ts`)

- `cacheComponents: true` + `partialPrefetching: true`: blocking routes that must render full HTML
  per request export `const instant = false` (see the ticker page) instead of opting out globally.
- `reactCompiler: true`; Turbopack dev+build with `turbopackMemoryEviction` and Rust React Compiler
  experiments enabled.
- Dev rewrite: `/api/v1/:path*` → `http://127.0.0.1:5000/api/v1/:path*` — same-origin API access in
  dev without CORS; keep it aligned with the backend port.

### PWA / Service Worker (Serwist + Turbopack) — `src/app/sw.ts`

- Wrapped by `withSerwist` from `@serwist/turbopack` in `next.config.ts`. Do not switch to `next-pwa`/workbox.
- Cache strategies (keep these):
  - **Asset sheets** (`/etf/`, `/bdr/`): `StaleWhileRevalidate`, `maxEntries: 100`, `maxAgeSeconds: 24h`.
  - **Tools / comparator** (`/ferramentas/`, `/comparador`): `CacheFirst`, `maxEntries: 50`, `maxAgeSeconds: 7d`.
  - `...defaultCache` merges Serwist's sensible defaults.
- Scope note: matchers cover **document navigations only** for those prefixes. `/ativos/[ticker]`
  (the canonical page), `/fii/`, and all `/api/v1/*` JSON responses are NOT service-worker cached —
  series data offline comes from the HTTP/React Query caches, not from Serwist. Extend matchers
  deliberately if that changes.
- `navigationPreload: true`, `skipWaiting: true`, `clientsClaim: true`.

### Charts

- Time-series / quotes / equity curve / drawdown → `lightweight-charts` (Canvas, ~60fps).
- Allocation / Top-10 holdings / donut → `recharts` (SVG, theme-aware via Tailwind tokens).
- Overlap Venn / correlation heatmap → `@visx/group` + `@visx/shape`.

## 6. Local-First constraint (NON-NEGOTIABLE)

No user-facing request may call an external provider (BCB, CVM, Brapi, Yahoo, …) in real time.

- The web app only talks to the .NET API and Redis (both local). External ingest happens in `apps/backend`
  `IndexDesk.Worker` on a schedule.
- `api-client.ts` already encapsulates this; new data needs a backend endpoint, **not** a direct provider call.
- Provider API keys / latency settings live in `.env` (`Providers__*`, `ConnectionStrings__*` at backend) —
  never hardcode secrets. Frontend reads only `NEXT_PUBLIC_API_URL`.

## 7. Testing

- **Vitest** (`vitest run`), browser env `jsdom`, setup in `vitest.setup.ts` (`@testing-library/jest-dom`).
- RTL stack: `@testing-library/react`, `@testing-library/user-event`. Path resolution via `vite-tsconfig-paths`.
- Put unit tests next to code in `src/lib/__tests__/` (or co-located `*.test.ts(x)`).
- Keep the offline-fallback paths covered — they are the offline-first safety net.
- `bun test:coverage` for coverage (`coverage/**`, cached by Turbo).

## 8. User-facing language and route visibility

- Rendered copy, SEO metadata, accessible labels, loading states, and error messages use clear, natural pt-BR aimed at investors. Do not expose implementation terms such as API, backend, endpoint, worker, local-first, persisted data, ingestion, transport status, or roadmap IDs (for example, `MVP-011`). These terms may remain in source comments, DTOs, query keys, API-client code, and architecture documentation.
- Never render raw exception or API error messages. Map failures to stable, helpful messages such as “Não foi possível carregar os ativos agora. Tente novamente em instantes.” Keep technical details available only to internal diagnostics.
- Every Next.js page is public by default, including `/admin` and `/ferramentas/backtest`. The `(public)` and `(admin)` folders are organizational route groups and do not provide access control. Do not add middleware or page-level authentication gates. Authentication remains optional for account actions and must not prevent public pages, catalogues, comparisons, calculators, or tools from rendering.
- `src/middleware.ts` is an explicit pass-through. Middleware tests must keep anonymous access coverage for `/admin`, nested admin routes, and public tools, with no authentication redirect or `Location` header.
- Public market-data pages still use only the local application data layer and must preserve honest loading, empty, unavailable, and retry states. Public visibility does not permit fixture data, synthetic values, or direct provider calls.

<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->
