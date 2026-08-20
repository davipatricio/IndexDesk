# 🗺️ IndexDesk — Roadmap de Execução

> **Arquivo gerado:** não edite `ROADMAP.md` diretamente. Atualize `.roadmap/**/*.json` e execute `bun run roadmap:generate`.

> **Estado atual:** requisitos documentados, implementação ainda não scaffoldada.

**Atualizado em:** 2026-08-19 · **Estado:** `active_development` · **Progresso:** [████████████░░░░░░░░░] 45% (24/53)

**Tarefas:** 53 total · 24 concluídas · 0 em andamento · 0 bloqueadas · 29 não iniciadas/deferidas

## Estado do projeto

- **Ciclo de vida:** `active_development`
- **Tracking do roadmap scaffoldado:** `true`
- **Código do produto scaffoldado:** `true`
- **Commits registrados no snapshot:** `0`
- **Bloqueadores:** Nenhum bloqueador crítico permanece (FND-013 resolvido após iniciar containers Docker)

> **Nota:** Setup inicial do monorepo IndexDesk concluído sem commit: Bun + Turborepo, Next.js 16.3.1 SSR/PWA, shadcn Base UI Nova, Vitest, solução modular .NET 9, Docker Compose, CI e instruções escopadas. Páginas atuais são scaffold demonstrativo; o desenvolvimento de produto permanece nas fases seguintes.

## Visão por fase

| Fase | Status | Prioridade | Progresso | Dependências |
| :--- | :--- | :---: | :---: | :--- |
| **00 — Foundation & Platform Scaffold** | 🔵 `complete` | `P0` | 100% (16/16) | — |
| **01 — MVP & Core Market Intelligence** | 🟢 `in_progress` | `P0` | 68% (15/22) | `PHASE-00` |
| **02 — Growth, Programmatic SEO & Retention** | ⬜ `not_started` | `P2` | 0% (0/6) | `PHASE-01` |
| **03 — Portfolio, Fixed Income & Tax Automation** | ⬜ `not_started` | `P2` | 0% (0/9) | `PHASE-01`, `PHASE-02` |

## Dependências críticas

```text
PHASE-00 foundation + infrastructure
  -> PHASE-01 persistence + ingestion + APIs + public MVP
  -> PHASE-02 saved backtests + programmatic SEO + conversion
  -> PHASE-03 portfolios + fixed income + events + tax automation

Auth/RBAC/audit -> admin -> saved backtests -> portfolios/alerts
Holdings ingestion -> overlap -> comparison/SEO overlap pages
Macro/holidays -> real yield -> backtest/fixed income accrual
Quotes/corporate actions/transactions -> portfolio performance/rebalance/DARF
News/reports/storage/RSS -> public hubs + admin publishing
```

## Fase 00 — Foundation & Platform Scaffold

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [████████████] 100% (3/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `FND-001` | Criar workspace raiz Bun | `P0` | `easy` | ✅ `complete` | — |
| `FND-002` | Configurar turbo.json para JS e .NET | `P0` | `medium` | ✅ `complete` | `FND-001` |
| `FND-003` | Configurar Oxlint, Oxfmt, TypeScript 7 e quality gates | `P1` | `medium` | ✅ `complete` | `FND-001` |
| `FND-004` | Scaffoldar apps/web com Next.js 16.3 | `P0` | `medium` | ✅ `complete` | `FND-001`, `FND-002` |
| `FND-005` | Adicionar Tailwind e Shadcn locals | `P1` | `medium` | ✅ `complete` | `FND-004` |
| `FND-006` | Criar configuração de qualidade e testes do web | `P1` | `medium` | ✅ `complete` | `FND-003`, `FND-004` |
| `FND-007` | Configurar SSR-first e cache components | `P0` | `hard` | ✅ `complete` | `FND-004` |
| `FND-008` | Criar IndexDesk.sln e hosts .NET | `P0` | `hard` | ✅ `complete` | `FND-002` |
| `FND-009` | Configurar REST/OpenAPI e cliente tipado | `P0` | `medium` | ✅ `complete` | `FND-008` |
| `FND-010` | Criar BuildingBlocks de infraestrutura | `P0` | `hard` | ✅ `complete` | `FND-008` |
| `FND-011` | Criar Docker Compose local | `P0` | `medium` | ✅ `complete` | — |
| `FND-012` | Criar .env.example e configuração local | `P0` | `easy` | ✅ `complete` | — |
| `FND-013` | Criar migrations e inicialização do banco | ⬜ `not_started` | `hard` | ⬜ `not_started` | `FND-010`, `FND-011` |
| `FND-014` | Implementar Auth e RBAC base | `P0` | `hard` | ✅ `complete` | `FND-013` |
| `FND-015` | Instrumentar OpenTelemetry e health checks | `P0` | `hard` | ✅ `complete` | `FND-010`, `FND-011` |
| `FND-016` | Configurar testes de integração e CI | `P1` | `hard` | ✅ `complete` | `FND-002`, `FND-006`, `FND-008`, `FND-013` |

## Fase 01 — MVP & Core Market Intelligence

**Status:** 🟢 `in_progress` · **Prioridade:** `P0` · **Progresso:** [███████████░░░░░] 68% (15/22)

**Objetivo:** Entregar dados locais confiáveis, catálogo público, comparação, backtest, calculadoras, admin, conteúdo e SEO/PWA básicos.
**Depende de:** `PHASE-00`

### Local-First Data Ingestion

_Ingerir dados de providers em background, validar, persistir de forma idempotente e invalidar cache._

**Status:** 🟢 `in_progress` · **Prioridade:** `P0` · **Progresso:** [█████████░░░░] 73% (7/10)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `MVP-001` | Implementar scheduler Quartz do Worker | `P0` | `hard` | ✅ `complete` | `FND-008`, `FND-010`, `FND-012`, `FND-015` |
| `MVP-002` | Ingerir BCB SGS e calendário macro | `P0` | `medium` | ✅ `complete` | `MVP-001`, `FND-013` |
| `MVP-003` | Implementar ingestão CVM streaming | `P0` | `complex` | ✅ `complete` | `MVP-001`, `FND-013` |
| `MVP-004` | Ingerir Brapi, Yahoo e feeds de preços | `P0` | `hard` | ✅ `complete` | `MVP-001`, `FND-013` |
| `MVP-005` | Ingerir B3, ANBIMA e composição de gestoras | `P1` | `hard` | ✅ `complete` | `MVP-001`, `MVP-003` |
| `MVP-006` | Aplicar resiliência, idempotência e cache | `P0` | `hard` | ✅ `complete` | `MVP-002`, `MVP-003`, `MVP-004`, `MVP-005` |
| `MVP-007` | Implementar MarketData APIs | `P0` | `hard` | ✅ `complete` | `MVP-006` |
| `MVP-008` | Implementar Analytics APIs | `P0` | `complex` | ✅ `complete` | `MVP-002`, `MVP-004`, `MVP-007` |
| `MVP-009` | Construir catálogo e páginas de ativo | `P0` | `hard` | ⬜ `not_started` | `MVP-007`, `FND-007` |
| `MVP-010` | Construir comparador de até seis ativos | `P0` | `hard` | ⬜ `not_started` | `MVP-008`, `FND-007` |
| `MVP-011` | Construir simulador público de backtest | `P0` | `complex` | ⬜ `not_started` | `MVP-008`, `FND-005`, `FND-006` |
| `MVP-012` | Implementar overlap e tax drag | `P1` | `hard` | ⬜ `not_started` | `MVP-003`, `MVP-008` |
| `MVP-013` | Implementar calculadoras macro e renda fixa pública | `P1` | `medium` | ⬜ `not_started` | `MVP-002`, `MVP-007` |
| `MVP-014` | Implementar DARF ETF e aposentadoria | `P1` | `hard` | ⬜ `not_started` | `MVP-007`, `MVP-009` |
| `MVP-015` | Implementar curadoria e locks por campo | `P1` | `hard` | ⬜ `not_started` | `FND-014`, `FND-013`, `MVP-007` |
| `MVP-016` | Implementar uploads e monitoramento de sync | `P1` | `hard` | ⬜ `not_started` | `MVP-001`, `MVP-015` |
| `MVP-017` | Criar modelo e APIs de notícias/relatórios | `P1` | `hard` | ⬜ `not_started` | `FND-013`, `MVP-016` |
| `MVP-018` | Construir hubs públicos de notícias e relatórios | `P1` | `medium` | ⬜ `not_started` | `MVP-001`, `MVP-017` |
| `MVP-019` | Integrar TanStack suite e cache hydration | `P0` | `hard` | ⬜ `not_started` | `FND-004`, `FND-007`, `MVP-007` |
| `MVP-020` | Integrar gráficos financeiros híbridos | `P1` | `hard` | ⬜ `not_started` | `MVP-010`, `MVP-011`, `DEC-005` |
| `MVP-021` | Configurar Serwist PWA offline-first | `P1` | `hard` | ⬜ `not_started` | `MVP-019`, `MVP-018` |
| `MVP-022` | Implementar SEO técnico e sitemaps runtime/cacheados | `P0` | `hard` | ⬜ `not_started` | `MVP-009`, `MVP-010`, `MVP-013`, `MVP-018` |

### Core Data Model & APIs

_Expor dados locais normalizados para web, analytics e admin via API única._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [✅] 100% (2/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `MVP-007` | Implementar MarketData APIs | `P0` | `hard` | ✅ `complete` | `MVP-006` |
| `MVP-008` | Implementar Analytics APIs | `P0` | `complex` | ✅ `complete` | `MVP-002`, `MVP-004`, `MVP-007` |

### Public Catalog, Comparison & Backtest

_Entregar as principais experiências públicas para descoberta e análise de ETFs/BDRs._

**Status:** ⬜ `not_started` · **Prioridade:** `P0` · **Progresso:** [░░░░░░░░░░░░] 0% (0/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `MVP-009` | Construir catálogo e páginas de ativo | `P0` | `hard` | ⬜ `not_started` | `MVP-007`, `FND-007` |
| `MVP-010` | Construir comparador de até seis ativos | `P0` | `hard` | ⬜ `not_started` | `MVP-008`, `FND-007` |
| `MVP-011` | Construir simulador público de backtest | `P0` | `complex` | ⬜ `not_started` | `MVP-008`, `FND-005`, `FND-006` |

### Public Financial Tools

_Disponibilizar calculadoras sem login usando dados previamente ingeridos._

**Status:** ⬜ `not_started` · **Prioridade:** `P1` · **Progresso:** [░░░░░░░░░░░░] 0% (0/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `MVP-012` | Implementar overlap e tax drag | `P1` | `hard` | ⬜ `not_started` | `MVP-003`, `MVP-008` |
| `MVP-013` | Implementar calculadoras macro e renda fixa pública | `P1` | `medium` | ⬜ `not_started` | `MVP-002`, `MVP-007` |
| `MVP-014` | Implementar DARF ETF e aposentadoria | `P1` | `hard` | ⬜ `not_started` | `MVP-007`, `MVP-009` |

### Admin Curatorship & Editorial Operations

_Permitir curadoria manual, correção de divergências, uploads e publicação editorial segura._

**Status:** ⬜ `not_started` · **Prioridade:** `P1` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `MVP-015` | Implementar curadoria e locks por campo | `P1` | `hard` | ⬜ `not_started` | `FND-014`, `FND-013`, `MVP-007` |
| `MVP-016` | Implementar uploads e monitoramento de sync | `P1` | `hard` | ⬜ `not_started` | `MVP-001`, `MVP-015` |

### News & Manager Reports Hub

_Publicar notícias, fatos relevantes e relatórios automáticos ou manuais associados a ativos._

**Status:** ⬜ `not_started` · **Prioridade:** `P1` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `MVP-017` | Criar modelo e APIs de notícias/relatórios | `P1` | `hard` | ⬜ `not_started` | `FND-013`, `MVP-016` |
| `MVP-018` | Construir hubs públicos de notícias e relatórios | `P1` | `medium` | ⬜ `not_started` | `MVP-001`, `MVP-017` |

### SSR, PWA, Charts & SEO MVP

_Entregar a fundação de aquisição orgânica, offline-first e visualização financeira._

**Status:** ⬜ `not_started` · **Prioridade:** `P0` · **Progresso:** [░░░░░░░░░░░░] 0% (0/4)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `MVP-019` | Integrar TanStack suite e cache hydration | `P0` | `hard` | ⬜ `not_started` | `FND-004`, `FND-007`, `MVP-007` |
| `MVP-020` | Integrar gráficos financeiros híbridos | `P1` | `hard` | ⬜ `not_started` | `MVP-010`, `MVP-011`, `DEC-005` |
| `MVP-021` | Configurar Serwist PWA offline-first | `P1` | `hard` | ⬜ `not_started` | `MVP-019`, `MVP-018` |
| `MVP-022` | Implementar SEO técnico e sitemaps runtime/cacheados | `P0` | `hard` | ⬜ `not_started` | `MVP-009`, `MVP-010`, `MVP-013`, `MVP-018` |

## Fase 02 — Growth, Programmatic SEO & Retention

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░░░░░] 0% (0/6)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `GROW-001` | Implementar saved_backtests e URLs públicas | `P2` | `hard` | ⬜ `not_started` | `FND-014`, `MVP-011` |
| `GROW-002` | Expandir hubs de categorias e gestoras | `P2` | `medium` | ⬜ `not_started` | `MVP-022`, `MVP-007` |
| `GROW-003` | Expandir comparações e páginas de overlap | `P2` | `hard` | ⬜ `not_started` | `MVP-012`, `GROW-001` |
| `GROW-004` | Instrumentar SEO e Core Web Vitals | `P2` | `medium` | ⬜ `not_started` | `GROW-002`, `GROW-003` |
| `GROW-005` | Adicionar exportação de relatórios PDF | `P2` | `hard` | ⬜ `not_started` | `GROW-001`, `DEC-004` |
| `GROW-006` | Criar preferências de newsletter e alertas | `P2` | `hard` | ⬜ `not_started` | `FND-014`, `GROW-005` |

## Fase 03 — Portfolio, Fixed Income & Tax Automation

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░░░░░] 0% (0/9)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `PORT-001` | Implementar carteiras e transações | `P2` | `hard` | ⬜ `not_started` | `FND-014`, `FND-013` |
| `PORT-002` | Calcular preço médio e posições | `P2` | `complex` | ⬜ `not_started` | `PORT-001`, `MVP-004` |
| `PORT-003` | Implementar TWR e MWR/TIR | `P2` | `complex` | ⬜ `not_started` | `PORT-002`, `MVP-002`, `MVP-004` |
| `PORT-004` | Implementar posições de renda fixa e accrual CDI/Selic/IPCA | `P2` | `complex` | ⬜ `not_started` | `MVP-002`, `PORT-001`, `FND-013` |
| `PORT-005` | Implementar tributação regressiva e isenções | `P2` | `hard` | ⬜ `not_started` | `PORT-004` |
| `PORT-006` | Construir calendário unificado de eventos | `P2` | `hard` | ⬜ `not_started` | `MVP-002`, `MVP-004`, `MVP-005` |
| `PORT-007` | Implementar alertas e preferências de mercado | `P3` | `hard` | ⬜ `not_started` | `PORT-006`, `GROW-006` |
| `PORT-008` | Criar rebalanceamento por aportes | `P3` | `hard` | ⬜ `not_started` | `PORT-002`, `PORT-003` |
| `PORT-009` | Implementar apuração mensal de DARF | `P3` | `complex` | ⬜ `not_started` | `PORT-002`, `PORT-005`, `PORT-008` |

## Decisões em aberto

| ID | Decisão | Status | Prioridade | Recomendação |
| :--- | :--- | :---: | :---: | :--- |
| `DEC-001` | Canonicalizar o bloqueio editorial de metadados | `open` | `P0` | Usar locked_fields JSONB como persistência canônica por campo, manter is_manually_overridden como flag de conveniência e expor metadata_lock apenas como contrato de API/editorial. |
| `DEC-002` | Definir política de cache histórico e SLOs | `open` | `P0` | Adotar TTL por classe: quotes fechadas persistidas no banco e Redis por 30 dias, dados intraday 15 minutos, macro 24 horas e holdings 7 dias; medir cache hit e endpoint local separadamente. |
| `DEC-003` | TimescaleDB self-hosted versus PostgreSQL puro | `open` | `P0` | Começar com TimescaleDB self-hosted em Docker e validar RAM, backup, compressão e throughput; manter migrations compatíveis com particionamento PostgreSQL 18 como fallback. |
| `DEC-004` | Fechar provedores de contingência e storage editorial | `open` | `P1` | Escolher por custo e cobertura após um spike; abstrair provider e storage atrás de interfaces no Worker/API, sem acoplar páginas públicas ao fornecedor. |
| `DEC-005` | Validar TanStack DB e biblioteca primária de gráficos | `open` | `P1` | Usar Lightweight Charts para séries longas e executar spike comparando TanStack DB, TanStack Store e IndexedDB antes de fixar a camada offline; Recharts fica para gráficos agregados. |
| `DEC-007` | Fixar a toolchain moderna de qualidade do frontend e C# | `open` | `P0` | Usar Oxlint/Oxfmt latest, TypeScript com tsconfig moderno (module preserve, moduleResolution bundler, strict, noEmit) e CSharpier + dotnet format analyzers/style. Fixar TypeScript 7 somente quando a versão estiver publicada e validada com Next.js/Turbopack; até lá, registrar o bloqueio e usar a versão estável mais próxima. |
| `DEC-006` | Manter o local-first estrito para calculadoras | `accepted` | `P0` | Calculadoras e páginas públicas leem apenas Postgres/Redis; o Worker ingere BCB, CVM, B3 e demais fontes em background. |

## Riscos

| ID | Risco | Probabilidade | Impacto | Status | Mitigação |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `RISK-001` | APIs não oficiais e feeds instáveis | `high` | `high` | `open` | Adapters isolados, snapshots locais, Polly, circuit breaker, logs de sync, providers de contingência e publicação manual. |
| `RISK-002` | Custo operacional do TimescaleDB e mensageria | `medium` | `high` | `open` | Medição de RAM/CPU/backup no spike inicial, profiles Docker opcionais e fallback para PostgreSQL 18 particionado. |
| `RISK-003` | Precisão fiscal e regulatória | `medium` | `critical` | `open` | Metadados versionados, fonte/competência explícitas, revisão editorial, disclaimers e testes de cenários; não tratar cálculo como aconselhamento fiscal. |
| `RISK-004` | Streaming CVM e arquivos grandes | `medium` | `high` | `open` | CsvHelper em stream, filtro por CNPJ, NpgsqlBinaryImporter/COPY, idempotência, métricas de linhas e testes com arquivos reais. |
| `RISK-005` | Serwist e Turbopack no modo offline | `medium` | `high` | `open` | Versionar cache, testar atualização/rollback do worker, separar dados públicos de dados autenticados e validar em dispositivos reais. |
| `RISK-006` | Segurança do backoffice e uploads | `medium` | `critical` | `open` | RBAC forte, auditoria, sanitização, validação MIME/tamanho, antivírus, storage privado, URLs assinadas e manual override por campo. |
| `RISK-007` | SEO programático duplicado ou de baixa qualidade | `medium` | `high` | `open` | Whitelist de pares relevantes, canonical URLs, sitemap segmentado, noindex para páginas finas e monitoramento de Search Console. |
| `RISK-008` | Entrega de notificações e storage editorial | `medium` | `medium` | `open` | Manter conversão opcional, definir provider/consentimento no Phase 02 e implementar abstrações com retry e opt-out. |

## Documentos-fonte

- `PRODUCT.md` — Produto, MVP, admin, notícias e roadmap futuro
- `STACK_SETUP.md` — Stack, monólito modular, PWA, TanStack, gráficos e Turborepo
- `MODELS.md` — Modelo de dados, séries temporais, carteiras e conteúdo
- `PROVIDERS.md` — Provedores, ingestão, holdings, cache e contingências
- `PROVIDERS_SYNC.md` — Local-first, streaming CVM, idempotência e resiliência
- `SEO_TOOLS.md` — Ferramentas públicas, SEO programático e conversão
- `CLAUDE.md` — Regras operacionais e resumo arquitetural

## Como atualizar

1. Edite a fase/decisão/risco correspondente em `.roadmap/`.
2. Execute `bun run roadmap:validate`.
3. Execute `bun run roadmap:generate`.
4. Revise o diff gerado e mantenha `CLAUDE.md` sincronizado com a implementação real.