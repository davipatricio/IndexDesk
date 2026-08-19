# apps/web — Frontend Instructions (scoped)

> Scoped supplement to root [`CLAUDE.md`](../../CLAUDE.md) and [`STACK_SETUP.md`](../../STACK_SETUP.md).
> This file covers the Next.js 16.3 app (`@indexdesk/web`). For product/SEO/data-provider
> context, defer to the root docs. **Do not modify code** when only instruction updates are requested —
> edits here are markdown only.

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

## 5. Architecture & conventions

### SSR-First + React Query hydration (SEO-critical)

1. **Server Component** (`src/app/**/page.tsx`): call `prefetchQuery` internally and `dehydrate(queryClient)`.
2. **Client Component**: wrap in `<HydrationBoundary state={dehydratedState}>` and consume via `useQuery()`.
3. Result: full HTML for crawlers, no layout shift on hydration. `src/app/providers.tsx` wires
   `QueryClientProvider` + `NuqsAdapter` (+ Devtools in dev). `src/lib/query-client.ts` holds the singleton.

### Data fetching client (`src/lib/api-client.ts`)

- Typed DTOs (`AssetDto`, `QuoteItem`, `BacktestRequest/Response`, `RealYieldResponse`).
- Base URL: `process.env.NEXT_PUBLIC_API_URL` (default `http://localhost:5000`). API routes are `/api/v1/*`.
- Uses `fetch(..., { next: { revalidate } })` for ISR at the route boundary
  (asset list `revalidate: 60`, single asset `revalidate: 300`).
- **Graceful offline fallback**: every fetcher catches and returns `DEFAULT_ASSETS` / computed mock so the
  UI works without the backend. Keep this pattern for new fetchers (offline-first is a product requirement).

### App routing layout

- `src/app/(public)/` — catalog, comparators, calculators, news/reports (`/etf/[ticker]`, `/bdr/[ticker]`,
  `/comparador`, `/ferramentas`, `/noticias`, `/relatorios`).
- `src/app/(admin)/` — backoffice (`/admin/assets`, `/admin/holdings`, `/admin/sync-jobs`).
- `src/app/~offline` — offline fallback route. `src/app/sw.ts` — Serwist worker source.
- Other: `layout.tsx`, `page.tsx`, `loading.tsx`, `error.tsx`, `not-found.tsx`, `globals.css`, `manifest.json`.

### PWA / Service Worker (Serwist + Turbopack) — `src/app/sw.ts`

- Wrapped by `withSerwist` from `@serwist/turbopack` in `next.config.ts`. Do not switch to `next-pwa`/workbox.
- Cache strategies (keep these):
  - **Asset sheets** (`/etf/`, `/bdr/`): `StaleWhileRevalidate`, `maxEntries: 100`, `maxAgeSeconds: 24h`.
  - **Tools / comparator** (`/ferramentas/`, `/comparador`): `CacheFirst`, `maxEntries: 50`, `maxAgeSeconds: 7d`.
  - `...defaultCache` merges Serwist's sensible defaults.
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

<!-- BEGIN:nextjs-agent-rules -->

# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` (resolved from this file's directory; in monorepos the `next` package may not be visible from the repo root) before writing any code. Heed deprecation notices.

This block is written and re-added by `next dev` — verify at `node_modules/next/dist/server/lib/generate-agent-files.js`. Removing it from a diff only re-creates the uncommitted change; committing it with your work keeps the tree clean.

<!-- END:nextjs-agent-rules -->
