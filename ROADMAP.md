# 🗺️ IndexDesk — Roadmap de Execução

> **Arquivo gerado:** não edite `ROADMAP.md` diretamente. Atualize `.roadmap/**/*.json` e execute `bun run roadmap:generate`.
> **Estado atual:** requisitos documentados, implementação ainda não scaffoldada.

**Atualizado em:** 2026-08-22 · **Estado:** `scaffolded` · **Progresso:** [███████████░░░░░░░░░] 56% (32/57)
**Tarefas:** 57 total · 32 concluídas · 3 em andamento · 0 bloqueadas · 22 não iniciadas/deferidas

## Estado do projeto

- **Ciclo de vida:** `active_development`
- **Tracking do roadmap scaffoldado:** `true`
- **Código do produto scaffoldado:** `true`
- **Commits registrados no snapshot:** `1`
- **Bloqueadores:** FND-013 permanece pendente: migrations não foram aplicadas porque containers Docker não foram iniciados, conforme solicitado. MVP-003 (ingestão CVM) reaberto: a implementação anterior foi removida no purge de dados sintéticos e nenhum job CVM existe no Worker. O SDK .NET local usa DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 porque libicu não está instalado no Debian sem sudo. OpenTelemetry.Exporter.OpenTelemetryProtocol 1.11.1 registra o advisory NU1902 e deve ser atualizado antes de produção.
- **Nota:** Setup inicial e fundamentação concluídos. Phase 01: ingestão Brapi/Yahoo/BCB, APIs MarketData/Analytics, catálogo com painel fiscal, páginas de ativo, comparador (≤6 ativos), backtest público, suite TanStack + gráficos e hub de rankings (/rankings + GET /api/v1/assets/rankings, MVP-023). MVP-003 reaberto após purge; screener avançado (MVP-024) e premium/desconto vs PL via CVM (MVP-025) planejados.

## Visão por fase

| Fase | Status | Prioridade | Progresso | Dependências |
| :--- | :--- | :---: | :---: | :--- |
| **00 — Foundation & Platform Scaffold** | 🔵 `in_progress` | `P0` | 94% (16/17) | — |
| **01 — MVP & Core Market Intelligence** | 🔵 `in_progress` | `P0` | 52% (13/25) | `PHASE-00` |
| **02 — Growth, Programmatic SEO & Retention** | ⬜ `not_started` | `P2` | 0% (0/6) | `PHASE-01` |
| **03 — Portfolio, Fixed Income & Tax Automation** | ⬜ `not_started` | `P2` | 33% (3/9) | `PHASE-01`, `PHASE-02` |

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

## Fases e tarefas

## Fase 00 — Foundation & Platform Scaffold

**Status:** 🔵 `in_progress` · **Prioridade:** `P0` · **Progresso:** [███████████████████░] 94% (16/17)
**Objetivo:** Transformar o repositório de documentação em um monorepo executável, observável e reproduzível.
**Depende de:** nenhuma fase

### Monorepo Bun + Turborepo

_Criar os workspaces e o pipeline único de desenvolvimento do frontend e backend._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [████████████] 100% (3/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `FND-001` | Criar workspace raiz Bun | `P0` | `easy` | ✅ `complete` | — |
| `FND-002` | Configurar turbo.json para JS e .NET | `P0` | `medium` | ✅ `complete` | `FND-001` |
| `FND-003` | Configurar Oxlint, Oxfmt, TypeScript 7 e quality gates | `P1` | `medium` | ✅ `complete` | `FND-001` |

<details>
<summary>Critérios e entregáveis</summary>

- **FND-001 — Criar workspace raiz Bun**
  - Critérios: bun install funciona na raiz; O workspace declara somente apps previstos; Scripts raiz apontam para Turbo
  - Entregáveis: package.json; bun.lock; configuração de workspace
  - Notas: Registrar versões exatas após o scaffold. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-002 — Configurar turbo.json para JS e .NET**
  - Critérios: Turbo executa build/test/dev por filtro; Saídas bin/obj do .NET são cacheáveis; Variáveis de ambiente usadas por cada task são declaradas
  - Entregáveis: turbo.json; scripts raiz documentados
  - Notas: Validar cache local antes de configurar cache remoto. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-003 — Configurar Oxlint, Oxfmt, TypeScript 7 e quality gates**
  - Critérios: turbo run lint, format e typecheck têm comandos reais; Oxlint usa presets TypeScript/React/Next.js e Oxfmt ignora artefatos; tsconfig usa target/lib modernos, module preserve, moduleResolution bundler, strict, noEmit e verbatimModuleSyntax; CSharpier check e dotnet format analyzers/style passam sem disputar whitespace; Falhas de quality gate retornam código não zero
  - Entregáveis: oxlint.config.ts; oxfmt.config.ts; tsconfig.json moderno; .config/dotnet-tools.json com CSharpier; configuração dotnet format/analyzers; scripts Turbo lint/format/typecheck
  - Notas: Fixar TypeScript 7 somente se a versão estiver publicada; caso contrário registrar bloqueio e usar a versão estável mais próxima até a disponibilidade. Não usar dotnet format whitespace junto com CSharpier. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.

</details>

### Next.js SSR-First Web Foundation

_Scaffoldar o frontend em apps/web com App Router, src e componentes locais._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [████████████] 100% (4/4)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `FND-004` | Scaffoldar apps/web com Next.js 16.3 | `P0` | `medium` | ✅ `complete` | `FND-001`, `FND-002` |
| `FND-005` | Adicionar Tailwind e Shadcn locais | `P1` | `medium` | ✅ `complete` | `FND-004` |
| `FND-006` | Criar configuração de qualidade e testes do web | `P1` | `medium` | ✅ `complete` | `FND-003`, `FND-004` |
| `FND-007` | Configurar SSR-first e cache components | `P0` | `hard` | ✅ `complete` | `FND-004` |

<details>
<summary>Critérios e entregáveis</summary>

- **FND-004 — Scaffoldar apps/web com Next.js 16.3**
  - Critérios: next dev inicia pela raiz via turbo; src/app e src/components existem; Não há packages/ui ou apps/docs
  - Entregáveis: apps/web/package.json; apps/web/src/app; apps/web/src/components
  - Notas: Confirmar a documentação da versão antes de configurar cache components. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-005 — Adicionar Tailwind e Shadcn locais**
  - Critérios: Componentes Shadcn são importáveis dentro de apps/web; Tema claro/escuro base funciona; Tailwind v4 compila no build
  - Entregáveis: apps/web/src/components/ui; tokens e estilos globais
  - Notas: Manter componentes simples até o design system ser fechado. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-006 — Criar configuração de qualidade e testes do web**
  - Critérios: Há pelo menos um teste executável; turbo run test --filter=web funciona; Falha de typecheck impede o build
  - Entregáveis: configuração de testes; primeiro teste de renderização
  - Notas: Registrar decisão quando houver spike. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-007 — Configurar SSR-first e cache components**
  - Critérios: Página de exemplo entrega HTML com dados pré-renderizados; Client Components são usados apenas onde há interatividade; Política de cache é documentada
  - Entregáveis: convenção de data fetching; página SSR de exemplo
  - Notas: Validar headers e invalidação com dados fictícios antes de dados reais. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.

</details>

### Modular Monolith Backend Foundation

_Criar a solução .NET única com API, Worker, módulos delimitados e REST/OpenAPI._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [████████████] 100% (3/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `FND-008` | Criar IndexDesk.sln e hosts .NET | `P0` | `hard` | ✅ `complete` | `FND-002` |
| `FND-009` | Configurar REST/OpenAPI e cliente tipado | `P0` | `medium` | ✅ `complete` | `FND-008` |
| `FND-010` | Criar BuildingBlocks de infraestrutura | `P0` | `hard` | ✅ `complete` | `FND-008` |

<details>
<summary>Critérios e entregáveis</summary>

- **FND-008 — Criar IndexDesk.sln e hosts .NET**
  - Critérios: dotnet build da solução funciona; API e Worker têm entrypoints independentes; Portfolio fica reservado para Phase 03
  - Entregáveis: apps/backend/IndexDesk.sln; IndexDesk.Api; IndexDesk.Worker; projetos de módulos
  - Notas: Não recriar microserviços separados nem YARP no MVP. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-009 — Configurar REST/OpenAPI e cliente tipado**
  - Critérios: OpenAPI é gerado de forma determinística; Cliente tipado pode chamar um endpoint de health/demo; oRPC e Elysia não entram no MVP
  - Entregáveis: configuração OpenAPI; cliente gerado ou typed fetch
  - Notas: Escolher @hey-api/openapi-ts ou typed fetch em decisão técnica. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-010 — Criar BuildingBlocks de infraestrutura**
  - Critérios: Cada integração tem uma abstração testável; Módulos dependem de interfaces e não de clients concretos; OpenTelemetry pode ser habilitado sem alterar módulos
  - Entregáveis: BuildingBlocks.Common; Persistence; Cache; Resilience; Messaging; Observability
  - Notas: Manter apenas abstrações realmente compartilhadas. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.

</details>

### Local Infrastructure & Configuration

_Reproduzir a infraestrutura de desenvolvimento com Docker, dados persistentes e segredos fora do Git._

**Status:** 🔵 `in_progress` · **Prioridade:** `P0` · **Progresso:** [████████░░░░] 67% (2/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `FND-011` | Criar Docker Compose local | `P0` | `medium` | ✅ `complete` | — |
| `FND-012` | Criar .env.example e configuração local | `P0` | `easy` | ✅ `complete` | — |
| `FND-013` | Criar migrations e inicialização do banco | `P0` | `hard` | ⬜ `not_started` | `FND-010`, `FND-011` |

<details>
<summary>Critérios e entregáveis</summary>

- **FND-011 — Criar Docker Compose local**
  - Critérios: Serviços iniciam com volumes persistentes; Healthchecks detectam indisponibilidade; Jaeger recebe OTLP
  - Entregáveis: docker-compose.yml; volumes e healthchecks
  - Notas: Validar versão da imagem antes de fixar o tag. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-012 — Criar .env.example e configuração local**
  - Critérios: Todos os serviços locais têm defaults documentados; Segredos ficam fora do repositório; API e Worker usam a mesma convenção de configuração
  - Entregáveis: .env.example; documentação de configuração
  - Notas: Separar providers essenciais do MVP de contingências futuras. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-013 — Criar migrations e inicialização do banco**
  - Critérios: Migrations são repetíveis em banco vazio; Hypertables e índices são criados quando Timescale está disponível; Fallback por particionamento é documentado/testável
  - Entregáveis: migrations; scripts de bootstrap Timescale/Postgres; seed mínimo de enums
  - Notas: Não marcar complete sem restaurar banco vazio e aplicar migrations.

</details>

### Sync Resilience & Self-Healing

_Garantir que a pipeline de ingestão se recupere sozinha de reinícios e downtime sem intervenção manual — single-writer entre CLI/API/job/catch-up._

**Status:** 🔵 `in_progress` · **Prioridade:** `P0` · **Progresso:** [████████████] 100% (1/1)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `OPS-018` | Auto-retomada de sincronização diária (Sync Bootstrap) | `P1` | `hard` | ✅ `complete` | `FND-013` |

<details>
<summary>Critérios e entregáveis</summary>

- **OPS-018 — Auto-retomada de sincronização diária (Sync Bootstrap)**
  - Critérios: Worker sem quotes do dia anterior roda catch-up de 1 dia automaticamente; Worker com 30 dias de gap respeita MaxBacklogDays (não trava em loop infinito); CLI --backfill iniciado em paralelo com catch-up recebe erro e aborta sem pisar em dados; Endpoints /sync/daily e /sync/backfill retornam 409 Conflict se lock ocupado; Postgres auto-libera lock se processo for killed -9; Suíte de testes unitários cobre BusinessDayCalculator e DividendQueueOutcome (28+ casos)
  - Entregáveis: BuildingBlocks.Persistence/Services/AdvisoryLockExtensions.cs; Entities/MarketHolidayEntity.cs + DbSet + mapeamento; MarketData/Ingestion/BusinessDayCalculator.cs + MarketHolidayQueries.cs; IndexDesk.Worker/Services/SyncBootstrapService.cs + DI no Program.cs; Lock no CLI --backfill; Lock nos endpoints /sync/daily e /sync/backfill; Tabela market_holidays seedada com 24 feriados B3 (2025+2026); Config Sync:CatchUp:* no appsettings.json; DEC-008 (Sync Bootstrap) no skill .agents/skills/indexdesk
  - Notas: Plano completo em plans/auto-retomada-sync.md. Implementado em 2026-08-30; decisão aceita DEC-008.

</details>

### Security, Observability & Engineering Baseline

_Estabelecer autenticação, RBAC, tracing, health checks e qualidade mínima antes do MVP._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [████████████] 100% (3/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `FND-014` | Implementar Auth e RBAC base | `P0` | `hard` | ✅ `complete` | `FND-013` |
| `FND-015` | Instrumentar OpenTelemetry e health checks | `P0` | `hard` | ✅ `complete` | `FND-010`, `FND-011` |
| `FND-016` | Configurar testes de integração e CI | `P1` | `hard` | ✅ `complete` | `FND-002`, `FND-006`, `FND-008`, `FND-013` |

<details>
<summary>Critérios e entregáveis</summary>

- **FND-014 — Implementar Auth e RBAC base**
  - Critérios: Refresh token não é armazenado em texto puro; Rotas admin exigem role adequada; Revogação e expiração são testadas
  - Entregáveis: Auth module; policies RBAC; testes de autenticação
  - Notas: Usar Argon2id ou BCrypt conforme decisão de segurança. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-015 — Instrumentar OpenTelemetry e health checks**
  - Critérios: Trace correlaciona Next/API/módulo/Redis/Postgres quando aplicável; Health endpoint informa dependências; Falhas de provider têm métricas e correlation id
  - Entregáveis: instrumentação OTel; health endpoints; dashboards/logging mínimo
  - Notas: Nunca registrar tokens ou dados financeiros pessoais nos spans. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.
- **FND-016 — Configurar testes de integração e CI**
  - Critérios: CI executa validação do roadmap; CI testa build/lint/test sem segredos; Teste de integração sobe dependências isoladas ou usa containers efêmeros
  - Entregáveis: workflow CI; fixtures de integração; documentação de comandos
  - Notas: Adicionar somente depois que os comandos reais estiverem scaffoldados. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session. Scaffolded in initial IndexDesk setup; validation runs are recorded in the session.

</details>

## Fase 01 — MVP & Core Market Intelligence

**Status:** 🔵 `in_progress` · **Prioridade:** `P0` · **Progresso:** [██████████░░░░░░░░░░] 52% (13/25)
**Objetivo:** Entregar dados locais confiáveis, catálogo público, comparação, backtest, calculadoras, admin, conteúdo e SEO/PWA básicos.
**Depende de:** `PHASE-00`

### Local-First Data Ingestion

_Ingerir dados de providers em background, validar, persistir de forma idempotente e invalidar cache._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [█████████░░░] 71% (5/7)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `MVP-001` | Implementar scheduler Quartz do Worker | `P0` | `hard` | ✅ `complete` | `FND-008`, `FND-010`, `FND-012`, `FND-015` |
| `MVP-002` | Ingerir BCB SGS e calendário macro | `P0` | `medium` | ✅ `complete` | `MVP-001`, `FND-013` |
| `MVP-003` | Implementar ingestão CVM streaming | `P0` | `complex` | ⬜ `not_started` | `MVP-001`, `FND-013` |
| `MVP-004` | Ingerir Brapi, Yahoo e feeds de preços | `P0` | `hard` | ✅ `complete` | `MVP-001`, `FND-013` |
| `MVP-005` | Ingerir B3, ANBIMA e composição de gestoras | `P1` | `hard` | ✅ `complete` | `MVP-001`, `MVP-003` |
| `MVP-006` | Aplicar resiliência, idempotência e cache | `P0` | `hard` | ✅ `complete` | `MVP-002`, `MVP-003`, `MVP-004`, `MVP-005` |
| `MVP-025` | Derivar premium/desconto vs PL e captação líquida do informe CVM | `P1` | `medium` | ⬜ `not_started` | `MVP-003`, `MVP-004` |

<details>
<summary>Critérios e entregáveis</summary>

- **MVP-001 — Implementar scheduler Quartz do Worker**
  - Critérios: Cada job tem schedule configurável; Execuções têm correlation id e sync_job_logs; Falha de um provider não interrompe os demais
  - Entregáveis: Quartz jobs; job configuration; job execution logging
  - Notas: Começar com execução manual habilitada para testes.
- **MVP-002 — Ingerir BCB SGS e calendário macro**
  - Critérios: Séries 12, 11, 433 e 189 persistem com data/valor; Reprocessamento do período não duplica linhas; Calculadoras leem somente dados locais
  - Entregáveis: BCB adapter; macro sync job; fixtures históricas
  - Notas: Registrar data de coleta e origem no modelo quando necessário.
- **MVP-003 — Implementar ingestão CVM streaming**
  - Critérios: Processa arquivo grande sem carregar todo ZIP em RAM; Filtra CNPJs antes do COPY; Reexecução é idempotente; Métricas registram linhas processadas/inseridas/ignoradas
  - Entregáveis: CVM informe adapter; CDA holdings adapter; streaming COPY pipeline; performance test
  - Notas: REABERTO 2026-08-22: implementação anterior foi removida no purge de dados sintéticos (commit 532fde5) — nenhum job CVM existe hoje no Worker; colunas alvo incluem VL_QUOTA, CAPTC_DIA, RESG_DIA.
- **MVP-004 — Ingerir Brapi, Yahoo e feeds de preços**
  - Critérios: Batch quotes são usados quando provider suporta; Rate limits são aplicados por provider; Benchmarks e FX são identificados como séries distintas; Proventos entram no modelo correto
  - Entregáveis: Brapi adapter; Yahoo adapter; quote normalization; dividend sync
  - Notas: Persistir snapshot local antes de expor ao frontend.
- **MVP-005 — Ingerir B3, ANBIMA e composição de gestoras**
  - Critérios: Holdings recebem origem e data de referência; Falha de um feed permite fallback ou alerta; Feriados alimentam market_holidays; Índices teóricos têm composição versionada
  - Entregáveis: manager feed adapters; B3/ANBIMA sync; source provenance
  - Notas: Upload manual do admin é fallback obrigatório quando não houver provider.
- **MVP-006 — Aplicar resiliência, idempotência e cache**
  - Critérios: Retry usa backoff e respeita rate limit; Circuit breaker abre e recupera; Eventos invalidam somente chaves afetadas; TTL por classe é testado
  - Entregáveis: Polly policies; cache key strategy; MassTransit events; failure tests
  - Notas: Histórico fechado: 30 dias; intraday: 15 min; macro: 24h; holdings: 7 dias, pendente de DEC-002.
- **MVP-025 — Derivar premium/desconto vs PL e captação líquida do informe CVM**
  - Critérios: Premium/discount = cotação fechamento ÷ VL_QUOTA − 1, com data-fonte visível; Captação líquida diária = CAPTC_DIA − RESG_DIA, idempotente; Métricas entram em etf_analytics_summary (premium_discount_pct, flow_30d); Alerta de prêmio/desconto extremo é disparado pós-ingest
  - Entregáveis: premium discount calculator; flow aggregation; analytics columns
  - Notas: Diferencial competitivo identado na análise etfsbrasil/B3/investidor10 (2026-08).

</details>

### Core Data Model & APIs

_Expor dados locais normalizados para web, analytics e admin via API única._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [████████████] 100% (2/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `MVP-007` | Implementar MarketData APIs | `P0` | `hard` | ✅ `complete` | `MVP-006` |
| `MVP-008` | Implementar Analytics APIs | `P0` | `complex` | ✅ `complete` | `MVP-002`, `MVP-004`, `MVP-007` |

<details>
<summary>Critérios e entregáveis</summary>

- **MVP-007 — Implementar MarketData APIs**
  - Critérios: Endpoints suportam ticker, tipo, data e paginação; Dados fiscais mostram origem/atualização; Queries de séries usam índices/chunk exclusion
  - Entregáveis: MarketData module endpoints; DTOs OpenAPI; query tests
  - Notas: Separar resumo de ativo de séries detalhadas.
- **MVP-008 — Implementar Analytics APIs**
  - Critérios: Comparação aceita até seis ativos/índices; Backtest valida pesos e datas; Métricas têm testes de casos extremos; Resultados repetidos podem usar Redis
  - Entregáveis: Analytics endpoints; backtest request/response contracts; benchmark tests
  - Notas: Motor de cálculo pode usar Span/Memory após profiling, não por premissa.

</details>

### Public Catalog, Comparison & Backtest

_Entregar as principais experiências públicas para descoberta e análise de ETFs/BDRs._

**Status:** ✅ `complete` · **Prioridade:** `P0` · **Progresso:** [██████████░░] 80% (4/5)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `MVP-009` | Construir catálogo e páginas de ativo | `P0` | `hard` | ✅ `complete` | `MVP-007`, `FND-007` |
| `MVP-010` | Construir comparador de até seis ativos | `P0` | `hard` | ✅ `complete` | `MVP-008`, `FND-007` |
| `MVP-011` | Construir simulador público de backtest | `P0` | `complex` | ✅ `complete` | `MVP-008`, `FND-005`, `FND-006` |
| `MVP-023` | Construir hub público de rankings por métrica | `P1` | `medium` | ✅ `complete` | `MVP-007`, `MVP-009` |
| `MVP-024` | Evoluir catálogo em screener avançado | `P2` | `hard` | ⬜ `not_started` | `MVP-023` |

<details>
<summary>Critérios e entregáveis</summary>

- **MVP-009 — Construir catálogo e páginas de ativo**
  - Critérios: HTML inicial contém dados essenciais; Painel fiscal informa come-cotas, IR e ausência de isenção quando aplicável; Link TradingView usa símbolo persistido; Estados vazio/erro são claros
  - Entregáveis: catalog route; asset detail routes; tax panel; TradingView link
  - Notas: Exibir disclaimer e data de atualização.
- **MVP-010 — Construir comparador de até seis ativos**
  - Critérios: URL representa ativos e período; Limite de seis é validado; Tabela compara risco/retorno e custos; Gráfico é acessível e responsivo
  - Entregáveis: comparison route; comparison table; normalized return chart
  - Notas: Usar agregação server-side para séries longas.
- **MVP-011 — Construir simulador público de backtest**
  - Critérios: Pesos somam 100%; Períodos inválidos são rejeitados; Resultados incluem CAGR/volatilidade/Sharpe/drawdown e retorno real; Não exige login
  - Entregáveis: backtest form; result charts; metrics table
  - Notas: Adicionar limites e cache por input normalizado.
- **MVP-023 — Construir hub público de rankings por métrica**
  - Critérios: Métricas: retorno 30d/6m/12m/no ano, variação do dia, volatilidade, Sharpe, drawdown, volume médio; Ativos sem dado suficiente na métrica ficam ao final em qualquer direção; Estado tipo/métrica/direção representado na URL (nuqs); Cache Redis de leitura (~10 min); nenhuma chamada externa em runtime
  - Entregáveis: rankings endpoint + DTOs; /rankings page + tabela; avgVolume30D nas estatísticas
  - Notas: Implementado sobre cotações locais; captação líquida entra com MVP-003/025.
- **MVP-024 — Evoluir catálogo em screener avançado**
  - Critérios: Filtros combináveis validados server-side com paginação; URL reflete todos os filtros (nuqs); Export CSV respeita filtros ativos; Campos taxa/PL/provedor aparecem quando disponíveis localmente
  - Entregáveis: screener filters; CSV export; shared filter state
  - Notas: Benchmark de referência: screener do etfsbrasil (filtros por gestora/região/provedor).

</details>

### Public Financial Tools

_Disponibilizar calculadoras sem login usando dados previamente ingeridos._

**Status:** ⬜ `not_started` · **Prioridade:** `P1` · **Progresso:** [░░░░░░░░░░░░] 0% (0/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `MVP-012` | Implementar overlap e tax drag | `P1` | `hard` | ⬜ `not_started` | `MVP-003`, `MVP-008` |
| `MVP-013` | Implementar calculadoras macro e renda fixa pública | `P1` | `medium` | ⬜ `not_started` | `MVP-002`, `MVP-007` |
| `MVP-014` | Implementar DARF ETF e aposentadoria | `P1` | `hard` | ⬜ `not_started` | `MVP-007`, `MVP-009` |

<details>
<summary>Critérios e entregáveis</summary>

- **MVP-012 — Implementar overlap e tax drag**
  - Critérios: Overlap mostra holdings repetidos e pesos agregados; Tax drag explicita premissas US/Irlanda/B3; Resultados são reproduzíveis
  - Entregáveis: overlap calculator; tax drag calculator; calculation tests
  - Notas: Mostrar data de referência de cada composição.
- **MVP-013 — Implementar calculadoras macro e renda fixa pública**
  - Critérios: Fórmula Fisher é documentada; CDI/Selic/IPCA são identificados por série e data; Fluxo mostra data D+1 e fonte CVM
  - Entregáveis: real yield tool; fixed income equivalence tool; CVM flow dashboard
  - Notas: Não buscar BCB em runtime.
- **MVP-014 — Implementar DARF ETF e aposentadoria**
  - Critérios: Calcula 15%/20% conforme input e classe; Mostra código 6015 e vencimento calculado; Explicita ausência da isenção de R$20k para ETFs; Regra dos 4% mostra premissas
  - Entregáveis: DARF calculator; retirement calculator; legal disclaimer
  - Notas: Não apresentar resultado como orientação fiscal individual.

</details>

### Admin Curatorship & Editorial Operations

_Permitir curadoria manual, correção de divergências, uploads e publicação editorial segura._

**Status:** ⬜ `not_started` · **Prioridade:** `P1` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `MVP-015` | Implementar curadoria e locks por campo | `P1` | `hard` | ⬜ `not_started` | `FND-014`, `FND-013`, `MVP-007` |
| `MVP-016` | Implementar uploads e monitoramento de sync | `P1` | `hard` | ⬜ `not_started` | `MVP-001`, `MVP-015` |

<details>
<summary>Critérios e entregáveis</summary>

- **MVP-015 — Implementar curadoria e locks por campo**
  - Critérios: Admin edita descrição, símbolo TradingView e dados fiscais; Campos bloqueados são ignorados pelo sync; Conflitos exibem provider/origem/data; Audit log guarda antes/depois
  - Entregáveis: admin assets routes; metadata lock API; audit UI
  - Notas: Implementar contrato canônico após decisão.
- **MVP-016 — Implementar uploads e monitoramento de sync**
  - Critérios: Upload valida MIME, tamanho, schema e preview; Trigger manual exige confirmação e permissão; Logs exibem duração, linhas e erro; Arquivos não são servidos publicamente sem sanitização/storage seguro
  - Entregáveis: admin sync routes; holdings upload pipeline; sync dashboard
  - Notas: Definir object storage em DEC-004.

</details>

### News & Manager Reports Hub

_Publicar notícias, fatos relevantes e relatórios automáticos ou manuais associados a ativos._

**Status:** ⬜ `not_started` · **Prioridade:** `P1` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `MVP-017` | Criar modelo e APIs de notícias/relatórios | `P1` | `hard` | ⬜ `not_started` | `FND-013`, `MVP-016` |
| `MVP-018` | Construir hubs públicos de notícias e relatórios | `P1` | `medium` | ⬜ `not_started` | `MVP-017`, `FND-007` |

<details>
<summary>Critérios e entregáveis</summary>

- **MVP-017 — Criar modelo e APIs de notícias/relatórios**
  - Critérios: Slug é único e sanitizado; Conteúdo é sanitizado antes de renderizar; Associações N:N funcionam; Status publicado/rascunho é respeitado
  - Entregáveis: content migrations; content APIs; admin editor contracts
  - Notas: Suportar origem automática e author manual.
- **MVP-018 — Construir hubs públicos de notícias e relatórios**
  - Critérios: Páginas são SSR e paginadas; Notícia vinculada aparece no ativo relacionado; PDF pode ser visualizado/baixado conforme permissão; Origem e data são visíveis
  - Entregáveis: news hub; reports hub; asset news section
  - Notas: Usar canonical e noindex onde necessário.

</details>

### SSR, PWA, Charts & SEO MVP

_Entregar a fundação de aquisição orgânica, offline-first e visualização financeira._

**Status:** 🔵 `in_progress` · **Prioridade:** `P0` · **Progresso:** [██████░░░░░░] 50% (2/4)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `MVP-019` | Integrar TanStack suite e cache hydration | `P0` | `hard` | ✅ `complete` | `FND-004`, `FND-007`, `MVP-007` |
| `MVP-020` | Integrar gráficos financeiros híbridos | `P1` | `hard` | ✅ `complete` | `MVP-010`, `MVP-011`, `DEC-005` |
| `MVP-021` | Configurar Serwist PWA offline-first | `P1` | `hard` | ⬜ `not_started` | `MVP-019`, `MVP-018` |
| `MVP-022` | Implementar SEO técnico e sitemaps runtime/cacheados | `P0` | `hard` | ⬜ `not_started` | `MVP-009`, `MVP-010`, `MVP-013`, `MVP-018` |

<details>
<summary>Critérios e entregáveis</summary>

- **MVP-019 — Integrar TanStack suite e cache hydration**
  - Critérios: Prefetch/dehydrate/hydrate funciona em página real; URL state usa nuqs; Tabelas/listas grandes usam virtualização quando necessário; DB/offline é isolado de dados autenticados sensíveis
  - Entregáveis: TanStack providers; hydration boundary; state conventions
  - Notas: Não persistir dados privados em cache público.
- **MVP-020 — Integrar gráficos financeiros híbridos**
  - Critérios: Equity/drawdown renderizam séries longas com zoom/pan; Alocação e métricas têm tooltips acessíveis; Bundle e performance são medidos; Biblioteca final fica registrada em decisions.json
  - Entregáveis: chart components; performance benchmark; accessibility checks
  - Notas: Sempre oferecer tabela ou dados alternativos acessíveis.
- **MVP-021 — Configurar Serwist PWA offline-first**
  - Critérios: App instala como PWA; Asset pages usam stale-while-revalidate; Calculadoras/assets estáticos têm fallback offline; Deploy atualiza worker sem servir cache incompatível
  - Entregáveis: Serwist config; manifest; offline UX; service worker tests
  - Notas: Não cachear rotas autenticadas no mesmo escopo público.
- **MVP-022 — Implementar SEO técnico e sitemaps runtime/cacheados**
  - Critérios: FinancialProduct/BreadcrumbList/WebApplication JSON-LD valida; Sitemap indexa listas de ativos e comparações relevantes; Sitemaps podem ser gerados em runtime e cacheados; lastmod usa atualização local ingerida; Páginas finas têm canonical/noindex
  - Entregáveis: metadata helpers; OG routes; sitemap index/routes; robots
  - Notas: Whitelist de comparações no MVP; expansão fica na Phase 02.

</details>

## Fase 02 — Growth, Programmatic SEO & Retention

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░░░░░░░░░] 0% (0/6)
**Objetivo:** Expandir aquisição orgânica e criar conversões persistentes sem colocar as ferramentas públicas atrás de login.
**Depende de:** `PHASE-01`

### Saved & Public Backtests

_Persistir simulações e permitir compartilhamento estável com associação opcional a usuário._

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░] 0% (0/1)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `GROW-001` | Implementar saved_backtests e URLs públicas | `P2` | `hard` | ⬜ `not_started` | `FND-014`, `MVP-011` |

<details>
<summary>Critérios e entregáveis</summary>

- **GROW-001 — Implementar saved_backtests e URLs públicas**
  - Critérios: Usuário autenticado salva simulação; Anônimo pode compartilhar quando permitido; Snapshot registra versão/data dos dados; Delete/revoke funciona
  - Entregáveis: saved backtest API; share route; ownership policies
  - Notas: Guardar parâmetros e métricas; recalcular sob demanda com aviso de versão.

</details>

### Programmatic SEO Expansion

_Ampliar hubs, páginas comparativas e sitemaps sem criar conteúdo fino ou duplicado._

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░] 0% (0/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `GROW-002` | Expandir hubs de categorias e gestoras | `P2` | `medium` | ⬜ `not_started` | `MVP-022`, `MVP-007` |
| `GROW-003` | Expandir comparações e páginas de overlap | `P2` | `hard` | ⬜ `not_started` | `MVP-012`, `GROW-001` |
| `GROW-004` | Instrumentar SEO e Core Web Vitals | `P2` | `medium` | ⬜ `not_started` | `GROW-002`, `GROW-003` |

<details>
<summary>Critérios e entregáveis</summary>

- **GROW-002 — Expandir hubs de categorias e gestoras**
  - Critérios: Cada hub possui descrição e filtros indexáveis; Dados mostram atualização e origem; Páginas vazias não são indexadas
  - Entregáveis: category hubs; manager hubs; SEO metadata
  - Notas: Priorizar hubs com demanda validada.
- **GROW-003 — Expandir comparações e páginas de overlap**
  - Critérios: Whitelist de pares é baseada em relevância; URLs possuem canonical e dados comparativos únicos; Sitemap não inclui combinações ilimitadas
  - Entregáveis: comparison generator; overlap pair routes; sitemap segment
  - Notas: Manter limite e telemetria de indexação.
- **GROW-004 — Instrumentar SEO e Core Web Vitals**
  - Critérios: Métricas não coletam dados sensíveis sem consentimento; Falhas de sitemap são alertadas; CWs são acompanhados por rota; Eventos de funil são definidos
  - Entregáveis: SEO telemetry; CWV dashboard; conversion events
  - Notas: Separar métricas de produto de dados de carteira.

</details>

### Conversion, Export & Notifications

_Adicionar ações de alto valor que justificam cadastro, mantendo calculadoras básicas públicas._

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `GROW-005` | Adicionar exportação de relatórios PDF | `P2` | `hard` | ⬜ `not_started` | `GROW-001`, `DEC-004` |
| `GROW-006` | Criar preferências de newsletter e alertas | `P2` | `hard` | ⬜ `not_started` | `FND-014`, `GROW-005` |

<details>
<summary>Critérios e entregáveis</summary>

- **GROW-005 — Adicionar exportação de relatórios PDF**
  - Critérios: PDF contém memória de cálculo e disclaimer; Arquivo tem expiração/controle de acesso; Falha de geração pode ser reprocessada; Não bloqueia ferramenta pública
  - Entregáveis: PDF renderer; artifact storage; download flow
  - Notas: Definir object storage em DEC-004.
- **GROW-006 — Criar preferências de newsletter e alertas**
  - Critérios: Consentimento é granular e revogável; Preferências não habilitam envio sem confirmação; Usuário vê histórico/estado de inscrição; Jobs de envio têm retry e idempotência
  - Entregáveis: notification preference model; subscription API; delivery abstraction
  - Notas: Fica fora do MVP público.

</details>

## Fase 03 — Portfolio, Fixed Income & Tax Automation

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [███████░░░░░░░░░░░░░] 33% (3/9)
**Objetivo:** Adicionar gestão completa de carteiras, renda fixa, eventos, rentabilidade e automação fiscal após a base local-first estar madura.
**Depende de:** `PHASE-01`, `PHASE-02`

### Portfolio Management

_Registrar transações, posições e múltiplas carteiras com regras fiscais rastreáveis._

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [████████████] 100% (3/3)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `PORT-001` | Implementar carteiras e transações | `P2` | `hard` | ✅ `complete` | `FND-014`, `FND-013` |
| `PORT-002` | Calcular preço médio e posições | `P2` | `complex` | ✅ `complete` | `PORT-001`, `MVP-004` |
| `PORT-003` | Implementar TWR e MWR/TIR | `P2` | `complex` | ✅ `complete` | `PORT-002`, `MVP-002`, `MVP-004` |

<details>
<summary>Critérios e entregáveis</summary>

- **PORT-001 — Implementar carteiras e transações**
  - Critérios: Tipos e sinais de quantidade são validados; Custos e corretora são preservados; Transferências têm origem/destino identificáveis; Permissões por usuário funcionam
  - Entregáveis: Portfolio module; transaction API; transaction tests
  - Notas: M-P1 implementado (PR #3, feat/portfolio-dashboard): CRUD carteiras (limite 3), transações BUY/SELL/INCOME/CORP_ACTION/TRANSFER com edição lógica auditável, PM + projeção de posições, valuation local-first. Pendente: importação retroativa em lote e DDL formal em migration (FND-013). Plano vivo: plans/dashboard-carteiras/. | Entregue no PR #3 (feat/portfolio-dashboard): CRUD carteiras (limite 3), transações BUY/SELL/INCOME/CORP_ACTION/TRANSFER com edição lógica auditável + índice único anti-race, PM com fees, projeção reconstrutível testada. Wizard frontend c/ caixa sintético RF e corp actions.
- **PORT-002 — Calcular preço médio e posições**
  - Critérios: PM pondera custos conforme regra definida; Sells reduzem posição sem distorcer custo; Splits ajustam quantidade/PM; Snapshot pode ser recalculado do log
  - Entregáveis: position projector; PM calculator; reconciliation tests
  - Notas: PositionProjector + AveragePriceCalculator implementados e testados (19 unitários); valuation com último close. Pendente: snapshots materializados, splits via feed de corporate actions, reconciliação. | PositionProjector + AveragePriceCalculator (261 unitários), summary on-demand, snapshots diários (hypertable + job idempotente). Contribuição por posição implementada.
- **PORT-003 — Implementar TWR e MWR/TIR**
  - Critérios: TWR neutraliza fluxos externos; MWR/TIR pondera datas de fluxo; Benchmarks CDI/IPCA/Ibovespa/S&P são comparáveis; Casos sem fluxo e fluxo no mesmo dia são testados
  - Entregáveis: performance engine; benchmark comparison API; financial math tests
  - Notas: Documentar convenções de valuation e timezone. | PerformanceEngine: TWR diário, XIRR MWR, vol/Sharpe/maxDD; endpoint /performance com benchmarks CDI/IBOV; painel frontend c/ presets+datas livres (nuqs) e aba Análise.

</details>

### Fixed Income Accrual Engine

_Atualizar posições de CDB/LCI/LCA/Tesouro e outros indexadores usando séries macro locais._

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `PORT-004` | Implementar posições de renda fixa e accrual CDI/Selic/IPCA | `P2` | `complex` | 🔵 `in_progress` | `MVP-002`, `PORT-001`, `FND-013` |
| `PORT-005` | Implementar tributação regressiva e isenções | `P2` | `hard` | 🔵 `in_progress` | `PORT-004` |

<details>
<summary>Critérios e entregáveis</summary>

- **PORT-004 — Implementar posições de renda fixa e accrual CDI/Selic/IPCA**
  - Critérios: Suporta CDI_PERCENT, CDI_PLUS, SELIC, IPCA_PLUS e PREFIXED; Capitaliza somente dias úteis aplicáveis; Reexecução do job é idempotente; Valor e data do último accrual são auditáveis
  - Entregáveis: fixed income module; accrual job; financial math tests
  - Notas: Validar fórmulas com fonte oficial e revisão financeira. | FixedIncomeAccrualCalculator puro (CDI%/CDI+/Selic/IPCA+/Prefixado) + PortfolioAccrualDailyJob idempotente + valuation de sintético/RF por ativo. Pendente: wizard de produtos RF por ativo, Tesouro ⛔ catálogo, vetores c/ séries reais.
- **PORT-005 — Implementar tributação regressiva e isenções**
  - Critérios: Faixas de prazo são versionadas; Isenção e retenção são explicitadas; Resultado mostra premissas e competência; Testes cobrem fronteiras 180/360/720 dias
  - Entregáveis: tax projection service; versioned tax rules; tax tests
  - Notas: Requer revisão editorial/fiscal antes de uso real. | TaxCalculators puros c/ 34 testes (fronteiras exatas, IOF, come-cotas, isenções PF) expostos em /tax/*; pendente versionamento das faixas e revisão editorial (RISK-003).

</details>

### Market Events & Notifications

_Centralizar eventos de proventos, macro, índices e fatos corporativos, com alertas opcionais._

**Status:** ⬜ `not_started` · **Prioridade:** `P2` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `PORT-006` | Construir calendário unificado de eventos | `P2` | `hard` | ⬜ `not_started` | `MVP-002`, `MVP-004`, `MVP-005` |
| `PORT-007` | Implementar alertas e preferências de mercado | `P3` | `hard` | ⬜ `not_started` | `PORT-006`, `GROW-006` |

<details>
<summary>Critérios e entregáveis</summary>

- **PORT-006 — Construir calendário unificado de eventos**
  - Critérios: Eventos duplicados são reconciliados; Datas futuras e históricas são distintas; Página pública exibe origem e atualização; Corporate actions ajustam séries/posições quando aplicável
  - Entregáveis: event ingestion; calendar API; calendar UI
  - Notas: Usar audit trail para alterações de eventos.
- **PORT-007 — Implementar alertas e preferências de mercado**
  - Critérios: Usuário configura limiar/canal; Eventos não geram duplicatas; Falhas de delivery são reprocessáveis; Opt-out é imediato
  - Entregáveis: alert rules; notification worker; delivery adapters
  - Notas: Depende de provider e storage/queue decididos na Phase 02.

</details>

### Smart Rebalancing & DARF Automation

_Apoiar aportes, compensação de prejuízos e apuração mensal sem executar ordens automaticamente._

**Status:** ⬜ `not_started` · **Prioridade:** `P3` · **Progresso:** [░░░░░░░░░░░░] 0% (0/2)

| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |
| :--- | :--- | :---: | :---: | :--- | :--- | 
| `PORT-008` | Criar rebalanceamento por aportes | `P3` | `hard` | ⬜ `not_started` | `PORT-002`, `PORT-003` |
| `PORT-009` | Implementar apuração mensal de DARF | `P3` | `complex` | 🔵 `in_progress` | `PORT-002`, `PORT-005`, `PORT-008` |

<details>
<summary>Critérios e entregáveis</summary>

- **PORT-008 — Criar rebalanceamento por aportes**
  - Critérios: Considera preços atuais locais e aporte disponível; Explica desvio e arredondamento de cotas; Não recomenda venda por padrão; Resultado é determinístico
  - Entregáveis: rebalance planner; recommendation API; scenario tests
  - Notas: Mostrar que a sugestão não é recomendação de investimento.
- **PORT-009 — Implementar apuração mensal de DARF**
  - Critérios: Segrega classes e day/swing trade; Mantém saldo de prejuízos por classe; Mostra código 6015, valor e vencimento; Relatório exige revisão/confirmação do usuário
  - Entregáveis: tax ledger; DARF report; export integration
  - Notas: Revisão fiscal obrigatória antes de produção. | Projeção DARF mensal por classe c/ isenções entregue (/tax/darf); apuração completa c/ compensação de prejuízos permanece pendente (ledger derivado on-demand).

</details>

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

