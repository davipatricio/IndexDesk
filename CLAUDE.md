# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication Style — Caveman Ultra (always)

**Always respond using the `caveman` skill at `ultra` intensity**, from the first response of every session. No trigger keyword needed — this file is the standing trigger. Load and follow `.agents/skills/caveman/SKILL.md`.

- Default intensity: **ultra** (`/caveman ultra`). Only explicit "stop caveman" / "normal mode" turns it off.
- Compress style, never substance: keep all technical content, code blocks, exact error strings, numbers/units, and negations verbatim.
- Never invent abbreviations; if caveman phrasing is not shorter than plain phrasing, use plain.
- Preserve the user's language (typically pt-BR in this repo) — compress the style, not the language.
- Auto-suspend only for security warnings, irreversible-action confirmations, or multi-step sequences where compression risks ambiguity; resume right after.

## Agent Skills

Managed by `npx skills add` (tracked in `skills-lock.json`; canonical copies in `.agents/skills/`, mirrored into `.claude/skills/`):

- `caveman` — communication mode defined above.
- **UI work (`apps/web`):** use `design-taste-frontend` when building or redesigning pages/components; run `ui-slop-score` / `anti-ui-slop` as a pre-ship audit against generic AI-looking UI; use `ui-radar` / `ui-design` for real-screen references when a concrete design question needs evidence.

## Project State

**Greenfield.** The repository currently contains design documents (no code, no commits yet):

- `PRODUCT.md` — product vision, modules, SEO strategy
- `STACK_SETUP.md` — architecture, stack, modular monolith topology, monorepo layout
- `PROVIDERS.md` — external data providers, rate limits, cache rules
- `PROVIDERS_SYNC.md` — "local-first" ingest/sync philosophy
- `MODELS.md` — database schema (PostgreSQL 18 + TimescaleDB), hypertables, DDL definitions
- `SEO_TOOLS.md` — public calculators/tools (no login), programmatic SEO pages, and lead conversion strategy

Read these before doing substantial work. This CLAUDE.md summarizes the big picture they define.

## Product

IndexDesk is a web platform (Brazilian market) focused on **ETFs and ETF BDRs** (plus individual stocks and reference indices). Core modules:

- Rich asset catalog (`/etf/[ticker]`, `/bdr/[ticker]`) with metadata, CVM PL/shareholders history, and an interactive **TradingView chart link** (`BMFBOVESPA:{TICKER}`).
- **Complete Fiscal & Tax Breakdown:** Come-Cotas status, exact Income Tax (IR) rates (Swing 15% / Day Trade 20%), explicit warning on **no R$ 20k/month exemption** for ETFs, DARF vs. source withholding rules (Lei 13.043/14 for Fixed Income ETFs), and foreign dividend withholding (US 30% vs Ireland 15%).
- Multi-asset comparator (up to 6 assets/indices).
- Portfolio backtest simulator (rebalancing, inflation-adjusted via IPCA, CDI).
- Tax calculators (ETF overlap, tax-drag efficiency, real-yield via Fisher equation, CDI x IPCA+ equivalence).
- **News & Manager Reports Hub (`/noticias`, `/relatorios`, `/etf/[ticker]#noticias`):** Ingestion of RSS feeds, CVM material facts, and monthly manager research letters with manual creation/upload via the admin portal.
- **Admin Portal & Backoffice (`/admin`):** Asset curatorship, manual holdings/CSV upload, news/reports publishing, metadata conflict resolution with manual field locking (`metadata_lock` / `is_manually_overridden`), sync job monitoring, and audit logging.
- Heavy SEO emphasis: SSG/ISR per ticker, JSON-LD Structured Data (`FinancialProduct`), dynamic OG images (`@vercel/og`), dynamic sitemap.

## Intended Stack (per project init)

- **Bun** as the package manager/runtime (not npm/yarn/pnpm).
- **TypeScript 7** for the web workspace (pin the exact released version during scaffold; do not assume an unreleased compiler is installable).
- **Next.js 16.3** (App Router, `src/` directory) with **SSR-First** architecture: Server Components prefetch data (`prefetchQuery`) and dehydrate state for seamless client hydration via **TanStack React Query**, ensuring maximum SEO and no layout shift.
- **PWA & Offline-First:** **Serwist (`@serwist/next`) + Turbopack** for service worker lifecycle, route/data caching (_stale-while-revalidate_ on asset pages, _cache-first_ on calculators), and offline installability.
- **TanStack Ecosystem:**
  - `@tanstack/react-query` — server-state cache and hydration.
  - `@tanstack/react-table` — high-performance data tables (ETF catalog, comparator, CVM sheets).
  - `@tanstack/react-form` — type-safe forms (backtest allocations, contribution params).
  - `@tanstack/react-virtual` — virtualized lists for 10+ year daily quotes and large catalogs.
  - `@tanstack/react-hotkeys` — keyboard shortcuts (e.g. `Ctrl+K` ticker search).
  - `@tanstack/db` / TanStack Store — local client storage and offline data layer.
- `apps/web` holds **all UI components** (no shared `packages/ui`) — Tailwind CSS v4 + Shadcn UI colocated here, plus `stores/` (zustand + TanStack DB) and `search-params/` (nuqs).
- **JS/TS quality:** Oxlint (latest) with TypeScript/React/Next.js recommended presets, Oxfmt (latest) with generated-artifact ignores, and modern strict TypeScript config (`module: preserve`, `moduleResolution: bundler`, `noEmit`, `verbatimModuleSyntax`, `isolatedModules`, modern target/lib).
- **C# quality:** CSharpier (latest) as the local .NET tool for formatting; `dotnet format style`/`dotnet format analyzers` for SDK/analyzer diagnostics. Do not run whitespace formatting from both tools.
- **State management:** `zustand` for ephemeral client state, `nuqs` for type-safe URL query-string state (filters, tool inputs).
- **Data Visualization / Financial Charts:**
  - **TradingView Lightweight Charts (`lightweight-charts`)** — Canvas/WebGL 60fps standard for financial time-series (B3 quotes, backtest equity curve, drawdowns).
  - **Recharts** (or Shadcn/Tremor Charts) — SVG for portfolio allocation (donut/pie) and metrics comparison bars.
  - **Visx (Airbnb) / TanStack React Charts** — custom complex visuals (ETF Overlap Venn diagrams, correlation heatmaps).
- **Turborepo** (latest) is the monorepo orchestrator **and build system** — it drives build/lint/test/dev across every workspace, including the `.NET` backend in `apps/backend` (via `turbo.json` tasks caching `bin/` and `obj/`, e.g. `turbo run build --filter=backend`). Treat it as the single entry point for all workspace operations.
- **Frontend-Backend Communication:** Standard **REST + OpenAPI (Scalar)** with typed client generation in Next.js (`@hey-api/openapi-ts` or typed fetch). _(oRPC / Elysia+Bun are dropped for now, reserved for future consideration if needed)._

No product build/lint/test commands exist yet — the monorepo/backend is not scaffolded. The root only has roadmap tracking commands (`bun run roadmap:validate`, `bun run roadmap:generate`, `bun run roadmap:check`). Do not fabricate product commands.

## Backend Modular Monolith (.NET)

`apps/backend/IndexDesk.sln` structured as a **Modular Monolith** in .NET 9/10 (C# 12/13):

- **`IndexDesk.Api`** — Single ASP.NET Core HTTP host with Minimal APIs / Controllers, OpenAPI documentation (Scalar), and OpenTelemetry middleware.
- **`IndexDesk.Worker`** — Background worker host running **Quartz.NET** schedulers for external data ingestion (BCB, CVM, Brapi, Yahoo).
- **`Modules/`** — Strictly bounded domain modules:
  - **`Auth`** — users, JWT, HttpOnly-cookie refresh tokens, Argon2id/BCrypt hashing.
  - **`MarketData`** — asset metadata catalog, CVM quota/PL history, price series queries.
  - **`Analytics`** — in-memory backtest simulator, correlation matrix, Sharpe/drawdown, quote normalization (`Span<T>`/`Memory<T>`), heavy Redis caching.
  - **`Portfolio` (Fase 2/3)** — multi-portfolio management (Yahoo Finance / Gorila style), transaction logs (BUY, SELL, custody transfer), weighted Average Price (PM) fiscal calculation, TWR & MWR/IRR metrics, automatic daily CDI accrual for fixed income, and DARF reporting.
- **`BuildingBlocks/`** — Shared infrastructure:
  - **`Persistence`** — PostgreSQL 18 / TimescaleDB via EF Core + **Npgsql COPY** (`NpgsqlBinaryImporter`) for high-throughput batch writes.
  - **`Cache`** — Redis with specific TTL policies.
  - **`Resilience`** — Polly pipelines (exponential retry, circuit breaker, rate limiters).
  - **`Messaging`** — MassTransit + RabbitMQ for event-driven cache invalidation.
  - **`Observability`** — OpenTelemetry SDK exporting traces/metrics to Jaeger.

Shared infra via `docker-compose.yml`: **PostgreSQL 18 + TimescaleDB (self-hosted)** _(fallback to PostgreSQL 18 partitioned by date if needed)_, **Redis**, **RabbitMQ**, **Jaeger**.

## Data Providers & Rate Limits (`PROVIDERS.md`)

| Provider                     | Cost              | Rate limit      | Primary use                                                                        |
| :--------------------------- | :---------------- | :-------------- | :--------------------------------------------------------------------------------- |
| **BCB SGS** (API)            | Free              | ~100 req/min    | CDI (series `12`), Selic (`11`), IPCA (`433`), IGP-M (`189`)                       |
| **CVM** (open data)          | Free              | none hard       | Informe diário (PL/cotistas) + **CDA (Holdings & Overlap dos ETFs)**               |
| **ANBIMA & B3** (open data)  | Free              | none hard       | Feriados bancários (regra 252 dias úteis), índices IMA-B e carteiras teóricas IBOV |
| **Gestoras Direct CSV**      | Free              | none hard       | Holdings diários oficiais (iShares, Vanguard, Investo)                             |
| **Brapi.dev**                | Freemium R$29–99  | 10–1000 req/min | B3 quotes para ETFs/BDRs e proventos (Data COM/EX)                                 |
| **Yahoo Finance**            | Free (unofficial) | ~2000 req/IP/hr | Benchmarks globais (`^BVSP`, `^GSPC`, `^IXIC`), USD (`USDBRL=X`)                   |
| **FMP / HG Brasil (Backup)** | Freemium          | 250-500 req/dia | Contingência para ETFs globais UCITS e cotações B3                                 |

**Cache rules (Redis):** closed historical quotes → TTL 30 days/∞ (past data never changes); same-day data → TTL 15 min during B3 hours (10–18h); BCB/ANBIMA indicators → TTL 24h; Holdings/CDA → TTL 7 days. Ingest jobs run in `IndexDesk.Worker` (e.g. BCB daily 23:00 UTC, CVM daily 04:00 AM, Brapi after market close).

## Core Architectural Principle: Local-First (`PROVIDERS_SYNC.md`)

**No user request ever calls an external API in real time.** A backtest or comparison hits only local Postgres + Redis, targeting <10ms responses. Key implications for any work here:

- External providers are read **only** by `IndexDesk.Worker` scheduled background tasks.
- **CVM Ingestion Streaming:** Process large `inf_diario_fi_YYYYMM.zip` files using `CsvHelper` stream + strict CNPJ filter + `NpgsqlBinaryImporter` (Postgres binary `COPY`), avoiding RAM spikes and writing 50k+ rows/s.
- Writes to Postgres must be **idempotent** (re-running the same day updates rather than duplicates) — rely on TimescaleDB hypertables or upsert/COPY logic.
- Resilience via **Polly**: retry with exponential backoff + circuit breaker when a provider drops.
- `IndexDesk.Worker` emits events to **RabbitMQ**; `Modules.Analytics` consumes them to invalidate stale Redis entries, which then re-warm from Postgres.
- OpenTelemetry traces track the full flow: `Next.js -> IndexDesk.Api -> C# Module -> Redis/Postgres`.
- Provider API keys/latency concerns live in `.env` (`Providers__*`, `ConnectionStrings__*`), never in code or commits.

## Repo Skill: `.agents/skills/indexdesk` (keep it in sync)

This repository has an internal agent skill at `.agents/skills/indexdesk/` that mirrors project knowledge
(product, architecture, current state, market/tax rules, financial formulas). **Keeping it updated is part
of the definition of done**, alongside roadmap regeneration and this file. When finishing any work that changes
project state, update the matching skill file before wrapping up:

| Change | Update |
| :--- | :--- |
| Feature implemented/removed, new endpoint, route or Worker job | `references/process/current-features.md` |
| Decision (DEC-\*) accepted/closed, new risk (RISK-\*) | `references/process/decisions-risks.md` |
| New convention or recurring pitfall discovered | `references/process/common-mistakes.md` (or the domain file) |
| Phase/task progress | regenerate roadmap (`roadmap:validate` + `roadmap:generate`) and refresh `references/process/project-state.md` snapshot |
| Stack/version/tooling change | the matching `references/architecture/*.md` file |

Rules of thumb: domain rules (market/tax/formulas) live in `market/*`; code-local conventions live in the
scoped nested `CLAUDE.md`s (`apps/*/CLAUDE.md`, `IndexDesk.Worker/`, `Modules.Auth/`) — never duplicate one
inside the other. New scoped docs follow the `CLAUDE.md` + `AGENTS.md` symlink pair pattern.
