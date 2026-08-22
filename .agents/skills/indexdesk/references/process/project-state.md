# Estado Atual do Projeto & Roadmap

> Snapshot do `ROADMAP.md` gerado em **2026-08-22**. Para o estado exato, rode `bun run roadmap:check`
> ou leia `ROADMAP.md`. Este resumo pode estar defasado.

## Visão geral

- **Ciclo:** `active_development` · scaffold concluído, produto em construção.
- **Progresso:** 53% (28/53 tarefas). 0 em andamento · 25 não iniciadas.

| Fase | Escopo | Status | Progresso |
| :--- | :--- | :--- | :---: |
| **00 Foundation** | Monorepo Bun+Turbo, Next.js SSR-first, monólito .NET, Docker/env, auth/RBAC, OTel, CI | 🔵 in_progress (P0) | 94% (15/16) |
| **01 MVP & Core Intelligence** | Ingestão local-first, APIs MarketData/Analytics, catálogo, comparador, backtest, calculadoras, admin, notícias, PWA/SEO | 🔵 in_progress (P0) | 59% (13/22) |
| **02 Growth & Programmatic SEO** | Saved backtests, expansão SEO, PDF export, newsletter/alertas | ⬜ not_started (P2) | 0% (0/6) |
| **03 Portfolio & Tax Automation** | Carteiras/transações, PM, TWR/MWR, accrual RF, eventos, DARF automation | ⬜ not_started (P2) | 0% (0/9) |

Dependências: PHASE-00 → 01 → 02 → 03. Auth→admin→saved backtests→portfolios; holdings→overlap→páginas SEO.

## ✅ Concluído (não recriar/refazer)

- Monorepo Bun + turbo.json (JS e .NET), Oxlint/Oxfmt/TS moderno + CSharpier/dotnet format.
- `apps/web`: Next.js 16.3 + Tailwind v4 + shadcn base-nova + Vitest; SSR-first/cache components.
- `apps/backend`: `IndexDesk.sln` (Api + Worker + Modules Auth/MarketData/Analytics + BuildingBlocks),
  REST/OpenAPI Scalar, cliente tipado.
- Docker compose local + `.env.example`.
- Auth/RBAC (Argon2id/BCrypt, refresh HttpOnly), OpenTelemetry→Jaeger, health checks, testes integração + CI.
- **Ingestão completa (MVP-001..006):** Quartz scheduler, BCB SGS, CVM streaming (COPY), Brapi/Yahoo,
  B3/ANBIMA/gestoras, Polly/idempotência/cache.
- **APIs (MVP-007/008):** MarketData + Analytics (comparação ≤6 ativos, backtest validado).
- **Público (MVP-009..011):** catálogo/páginas de ativo com painel fiscal, comparador, simulador de backtest.
- TanStack suite + hydration (MVP-019), gráficos híbridos Lightweight Charts/Recharts/Visx (MVP-020).

## ⬜ Pendente na fase atual (próximo trabalho provável)

- **FND-013** migrations/banco (bloqueador — ver abaixo).
- Calculadoras públicas: overlap/tax drag (MVP-012), macro/RF/fluxo CVM (MVP-013), DARF/aposentadoria (MVP-014).
- Admin: curadoria + locks por campo (MVP-015), uploads + monitor de sync (MVP-016).
- Notícias/relatórios: modelo+APIs (MVP-017), hubs públicos (MVP-018).
- Serwist PWA formalizada (MVP-021) e SEO técnico/sitemaps (MVP-022).

## 🚧 Bloqueadores e notas de ambiente

1. **FND-013 pendente:** migrations não aplicadas porque os containers Docker não foram iniciados (por solicitação do dono do repo).
2. SDK .NET local usa `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (libicu ausente no Debian sem sudo).
3. `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.1` registra advisory NU1902 — atualizar antes de produção.

## Como atualizar o roadmap

1. Editar a fase/tarefa/decisão/risco em `.roadmap/**/*.json` (**nunca editar `ROADMAP.md` direto**).
2. `bun run roadmap:validate` → 3. `bun run roadmap:generate` → 4. revisar diff e manter `CLAUDE.md` sincronizado.
