# Estado Atual do Projeto & Roadmap

> Snapshot do `ROADMAP.md` gerado em **2026-08-23** (pós-MVP-023 + iniciativa provider-sync). Para o estado exato, rode
> `bun run roadmap:check` ou leia `ROADMAP.md`. Este resumo pode estar defasado.

## Visão geral

- **Ciclo:** `active_development` · scaffold concluído, produto em construção.
- **Progresso:** 50% (28/56 tarefas). 0 em andamento · 28 não iniciadas.

| Fase | Escopo | Status | Progresso |
| :--- | :--- | :--- | :---: |
| **00 Foundation** | Monorepo Bun+Turbo, Next.js SSR-first, monólito .NET, Docker/env, auth/RBAC, OTel, CI | 🔵 in_progress (P0) | 94% (15/16) |
| **01 MVP & Core Intelligence** | Ingestão local-first, APIs MarketData/Analytics, catálogo, comparador, backtest, rankings, calculadoras, admin, notícias, PWA/SEO | 🔵 in_progress (P0) | 52% (13/25) |
| **02 Growth & Programmatic SEO** | Saved backtests, expansão SEO, PDF export, newsletter/alertas | ⬜ not_started (P2) | 0% (0/6) |
| **03 Portfolio & Tax Automation** | Carteiras/transações, PM, TWR/MWR, accrual RF, eventos, DARF automation | 🔵 in_progress (P2) | M-P1 concluído; PORT-001/002 em andamento (PR #3) |

Dependências: PHASE-00 → 01 → 02 → 03. Auth→admin→saved backtests→portfolios; holdings→overlap→páginas SEO.

## ✅ Concluído (não recriar/refazer)

- Monorepo Bun + turbo.json (JS e .NET), Oxlint/Oxfmt/TS moderno + CSharpier/dotnet format.
- `apps/web`: Next.js 16.3 + Tailwind v4 + shadcn base-nova + Vitest; SSR-first/cache components.
- `apps/backend`: `IndexDesk.sln` (Api + Worker + Modules Auth/MarketData/Analytics + BuildingBlocks),
  REST/OpenAPI Scalar, cliente tipado.
- Docker compose local + `.env.example`.
- Auth/RBAC (Argon2id/BCrypt, refresh HttpOnly), OpenTelemetry→Jaeger, health checks, testes integração + CI.
- **Ingestão (MVP-001/002/004..006):** Quartz scheduler, BCB SGS, Brapi/Yahoo, B3/ANBIMA/gestoras,
  Polly/idempotência/cache. **CVM streaming reaberto (MVP-003)** — ver bloqueadores.
- **APIs (MVP-007/008):** MarketData + Analytics (comparação ≤6 ativos, backtest validado).
- **Público (MVP-009..011):** catálogo/páginas de ativo com painel fiscal, comparador, simulador de backtest.
- **Rankings (MVP-023):** `GET /api/v1/assets/rankings` + página `/rankings` (métrica × tipo × direção
  na URL via nuqs; `avgVolume30D` nas estatísticas; cache Redis 10 min).
- TanStack suite + hydration (MVP-019), gráficos híbridos Lightweight Charts/Recharts/Visx (MVP-020).
- **Redesign frontend fases A/B/C (2026-08-22):** home mini-dashboard, tabelas densas + sparklines,
  página de ativo com hero chart (lightweight-charts), seção "Simular investimento" vs benchmarks e
  painéis fiscais por classe (FiiTaxCard p/ FII).
- **Benchmarks IBOV/IFIX ingeridos** (IBOV 2890 cotações desde 2015; IFIX forward-only) +
  `GET /api/v1/assets/macro-series` (taxas brutas p/ acumulação client-side, cache 24h).
- **Iniciativa provider-sync (Fases 0–4 implementadas 2026-08-23; plano em
  `plans/provider-sync-scrapers.md`):** sidecar Python (`tools/providers/sidecar`, NDJSON v1)
  com clients C# `YfinanceSidecarClient`/`TradingViewSidecarClient`/`InfoMoneySidecarClient`
  (+ `sidecar fetch` anti-WAF); cadeia OHLCV Brapi → YahooSidecar → TVSidecar (HG Brasil fora,
  InfoMoney secundária); jobs `MarketDataDailySyncJob`/`FxRatesDailySyncJob`/`TradingViewDailySyncJob`/
  `HoldingsWeeklySyncJob` (tabela nova `fx_rates`, holdings em `etf_holdings`); pool de chaves +
  breaker por provider (`PARTIAL_WARNING` soft); backfill CLI multi-provider; recon + 7 HARs em
  `tools/providers/recon/`. Detalhe: [`current-features.md`](current-features.md).

## ⬜ Pendente na fase atual (próximo trabalho provável)

- **Provider-sync — calibração final:** credenciais TV (cookie autenticado) e token Brapi free
  pendentes (Fase 0 bloqueada neles); calibrar 429 da fila de proventos pós-token; DDL manual de
  `etf_holdings`/`fx_rates` vira migration formal quando FND-013 avançar.

- **FND-013** migrations/banco (roadmap segue `not_started`; migrations já aplicadas no banco local — ver abaixo).
- **MVP-003 reaberto:** ingestão CVM streaming (`inf_diario_fi`, CNPJ filter, COPY) — pré-requisito de
  MVP-025 (premium/desconto vs PL + captação líquida) e de parte do MVP-024.
- Screener avançado no catálogo (MVP-024).
- Calculadoras públicas: overlap/tax drag (MVP-012), macro/RF/fluxo CVM (MVP-013), DARF/aposentadoria (MVP-014).
- Admin: curadoria + locks por campo (MVP-015), uploads + monitor de sync (MVP-016).
- Notícias/relatórios: modelo+APIs (MVP-017), hubs públicos (MVP-018).
- Serwist PWA formalizada (MVP-021) e SEO técnico/sitemaps (MVP-022).

## 🚧 Bloqueadores e notas de ambiente

1. **FND-013:** containers Docker agora **rodando** e migrations aplicadas no banco local (tabelas
   verificadas em 2026-08-22); task permanece `not_started` no roadmap — aceite exige restaurar banco
   vazio e reaplicar.
2. **MVP-003 reaberto:** implementação CVM anterior foi removida no purge de dados sintéticos (commit `532fde5`); nenhum job CVM existe hoje.
3. SDK .NET local usa `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (libicu ausente no Debian sem sudo).
4. `OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.1` registra advisory NU1902 — atualizar antes de produção.

## Como atualizar o roadmap

1. Editar a fase/tarefa/decisão/risco em `.roadmap/**/*.json` (**nunca editar `ROADMAP.md` direto**).
2. `bun run roadmap:validate` → 3. `bun run roadmap:generate` → 4. revisar diff e manter `CLAUDE.md` sincronizado.
