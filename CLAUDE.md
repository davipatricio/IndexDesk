# CLAUDE.md

Guidance for Claude Code when working in this repository. Product docs call the platform **ETFHub B3**; code/branding uses **IndexDesk**.

## Communication Style — Caveman Ultra (always)

**Always respond using the `caveman` skill at `ultra` intensity**, from the first response of every session. No trigger keyword needed — this file is the standing trigger. Load and follow `.agents/skills/caveman/SKILL.md`.

- Default intensity: **ultra**. Only explicit "stop caveman" / "normal mode" turns it off.
- Compress style, never substance: keep all technical content, code blocks, exact error strings, numbers/units, and negations verbatim.
- Preserve the user's language (typically pt-BR in this repo) — compress the style, not the language.
- Auto-suspend only for security warnings, irreversible-action confirmations, or multi-step sequences where compression risks ambiguity; resume right after.

## Agent Skills

Managed by `npx skills add` (tracked in `skills-lock.json`; canonical copies in `.agents/skills/`, mirrored into `.claude/skills/`):

- `caveman` — communication mode defined above.
- **UI work (`apps/web`):** use `design-taste-frontend` when building or redesigning pages/components; run `ui-slop-score` / `anti-ui-slop` as a pre-ship audit against generic AI-looking UI; use `ui-radar` / `ui-design` for real-screen references when a concrete design question needs evidence.

## Project State

**Scaffolded monorepo, MVP in progress — not greenfield.** `apps/web` (Next.js) and `apps/backend` (.NET) contain real code and commit history.

- Single source of truth for progress: [`ROADMAP.md`](ROADMAP.md) (regenerate with `bun run roadmap:check` after phase/task changes).
- Live snapshot: `.agents/skills/indexdesk/references/process/project-state.md`.

Read before substantial work:

- `PRODUCT.md` — product vision, modules, SEO strategy
- `STACK_SETUP.md` — architecture, stack details, modular monolith topology
- `PROVIDERS.md` — external data providers, rate limits, cache TTL rules
- `PROVIDERS_SYNC.md` — "local-first" ingest/sync philosophy
- `MODELS.md` — database schema (PostgreSQL 18 + TimescaleDB), hypertables, DDL
- `SEO_TOOLS.md` — public calculators/tools (no login), programmatic SEO pages

## Commands (run from repo root, Bun + Turborepo)

| Command | Purpose |
| :--- | :--- |
| `bun install` | install workspace dependencies |
| `bun run dev` / `build` | dev servers / production build (all workspaces) |
| `bun run lint` / `typecheck` | Oxlint + `tsc --noEmit` across workspaces |
| `bun run test` | Vitest (web) + dotnet tests via Turbo |
| `bun run format` / `format:check` | Oxfmt (+ backend formatters via Turbo) |
| `bun run roadmap:validate` / `roadmap:generate` / `roadmap:check` | roadmap tracking |

Never use npm/yarn/pnpm. Do not invent commands absent from `package.json` files.

## Architecture (summary — details in scoped docs)

- **Turborepo** orchestrates every workspace, including the .NET backend (tasks cache `bin/`/`obj/`, e.g. `turbo run build --filter=backend`). Single entry point for all workspace operations.
- **`apps/web`** — Next.js 16.3 App Router (`src/`), SSR-First (React Query `prefetchQuery` + dehydration), Serwist PWA, full TanStack suite, Tailwind v4 + Shadcn/Base UI, zustand + nuqs, Oxlint/Oxfmt, Vitest. See `apps/web/CLAUDE.md`.
- **`apps/backend`** — .NET 9 modular monolith (`IndexDesk.sln`): `IndexDesk.Api` (Minimal APIs + Scalar OpenAPI), `IndexDesk.Worker` (Quartz.NET ingestion), bounded `Modules/` (Auth, MarketData, Analytics, Portfolio), shared `BuildingBlocks/` (Persistence, Cache, Resilience, Messaging, Observability). CSharpier + `dotnet format`. See `apps/backend/CLAUDE.md`.
- **Infra:** `docker-compose.yml` — PostgreSQL 18 + TimescaleDB, Redis, RabbitMQ, Jaeger.
- **Frontend ↔ backend:** REST + OpenAPI (Scalar), typed client generation in Next.js (`@hey-api/openapi-ts` or typed fetch).

## Product (one paragraph — details in `PRODUCT.md`)

IndexDesk is a web platform for the Brazilian market focused on **ETFs and ETF BDRs**: rich asset catalog (`/etf/[ticker]`, `/bdr/[ticker]`) with CVM history, complete fiscal/tax breakdown (Come-Cotas, IR swing 15% / day trade 20%, DARF rules), public metric rankings (`/rankings`), multi-asset comparator, portfolio backtest simulator, tax calculators, News & Manager Reports hub, admin portal with metadata locking and audit logging, and heavy programmatic SEO (SSG/ISR per ticker, JSON-LD, dynamic OG images, sitemap).

## Core Principle: Local-First (`PROVIDERS_SYNC.md`)

**No user request ever calls an external API in real time.** Backtests/comparisons hit only local Postgres + Redis (<10ms target).

- External providers are read **only** by `IndexDesk.Worker` scheduled jobs (rate limits and Redis TTLs: `PROVIDERS.md`).
- Writes to Postgres must be **idempotent** (upsert/COPY logic; hypertables for series).
- Large CVM files are streamed (`CsvHelper` + `NpgsqlBinaryImporter`), never loaded whole into RAM.
- Resilience via Polly (retry + circuit breaker). Worker emits RabbitMQ events; consumers invalidate stale Redis entries.
- Provider keys/connection strings live in `.env` (`Providers__*`, `ConnectionStrings__*`), never in code or commits.

## Repo Skill: `.agents/skills/indexdesk` (keep it in sync)

This repository has an internal agent skill at `.agents/skills/indexdesk/` that mirrors project knowledge (product, architecture, current state, market/tax rules, financial formulas). **Keeping it updated is part of the definition of done**, alongside roadmap regeneration and this file. When finishing any work that changes project state, update the matching skill file before wrapping up:

| Change | Update |
| :--- | :--- |
| Feature implemented/removed, new endpoint, route or Worker job | `references/process/current-features.md` |
| Decision (DEC-\*) accepted/closed, new risk (RISK-\*) | `references/process/decisions-risks.md` |
| New convention or recurring pitfall discovered | `references/process/common-mistakes.md` (or the domain file) |
| Phase/task progress | regenerate roadmap (`roadmap:validate` + `roadmap:generate`) and refresh `references/process/project-state.md` snapshot |
| Stack/version/tooling change | the matching `references/architecture/*.md` file |

Rules of thumb: domain rules (market/tax/formulas) live in the skill's `market/*`; code-local conventions live in the scoped nested `CLAUDE.md`s (`apps/web/CLAUDE.md`, `apps/backend/CLAUDE.md`) — never duplicate one inside the other. New scoped docs follow the `CLAUDE.md` + `AGENTS.md` symlink pair pattern.
