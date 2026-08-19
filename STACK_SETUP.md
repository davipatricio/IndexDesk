# 🛠️ STACK_SETUP.md — Arquitetura Técnica, Tecnologias e Configuração Inicial

## 1. Visão Geral da Stack Tecnológica

| Camada | Tecnologia | Função / Aplicação |
| :--- | :--- | :--- |
| **Frontend Framework** | **Next.js 16.3 (App Router, `src/`)** | **SSR-First**, Renderização SSG/ISR para SEO, Server Components + **Cache Components**. |
| **Package Manager / Runtime** | **Bun** | Dependências e execução dos pacotes/apps JS/TS (não npm/yarn/pnpm). |
| **TypeScript** | **TypeScript 7** | Tipagem estrita para o frontend; `module: preserve`, `moduleResolution: bundler`, target/lib modernos e `noEmit` para o typecheck do bundler. Validar a versão publicada no scaffold. |
| **Lint & Format JS/TS** | **Oxlint (latest) + Oxfmt (latest)** | Lint rápido com presets TypeScript/React/Next.js e formatter único, com configs recomendadas e ignores de artefatos. |
| **Lint & Format C#** | **CSharpier (latest) + `dotnet format` analyzers** | CSharpier para layout/whitespace; `dotnet format style/analyzers` para estilo e diagnósticos do SDK, sem conflito entre formatters. |
| **Monorepo Orchestrator** | **Turborepo (latest)** | Orquestra **todos** os workspaces (`apps/web`, `apps/backend`) — build, lint, format, typecheck, test, dev e caches via `turbo.json`. |
| **PWA & Offline-First** | **Serwist (`@serwist/next`) + Turbopack** | Service Worker moderno, cache de rotas e dados, instalação PWA e funcionamento offline. |
| **Data Fetching & Cache** | **@tanstack/react-query** | Cache de dados no cliente com desidratação/hidratação a partir do SSR do Next.js. |
| **Tabelas de Dados** | **@tanstack/react-table** | Tabelas ricas e performáticas para catálogo de ETFs, comparadores e relatórios CVM. |
| **Formulários** | **@tanstack/react-form** | Formulários fortemente tipados para simuladores de backtest, calculadoras fiscais e login. |
| **Virtualização** | **@tanstack/react-virtual** | Virtualização de listas longas (histórico diário de 10+ anos, listagem completa de ativos). |
| **Atalhos de Teclado** | **@tanstack/react-hotkeys** | Navegação rápida por teclado para investidores e *power users*. |
| **Camada de Dados Offline** | **@tanstack/db / TanStack Store** | Estruturas de dados em memória e sincronização com storage local para modo offline. |
| **Estado UI & URL** | **zustand + nuqs** | `zustand` para estado de cliente efêmero; `nuqs` para estado tipado na URL (filtros de busca, tickers). |
| **Estilização & UI** | **Tailwind CSS v4 + Shadcn UI** | Design System rápido, otimizado e responsivo (componentes colocados em `apps/web`). |
| **Gráficos & Visualização**| **TradingView Lightweight Charts + Recharts** | Canvas de 60fps para séries temporais financeiras (cotações/backtests) + SVG para alocação/donut. |
| **Comunicação Web-API**| **REST + OpenAPI (Scalar / Swagger)** | Endpoints REST no .NET com OpenAPI e cliente tipado no Next.js (`@hey-api/openapi-ts` ou fetch tipado). |
| **Backend** | **Monólito Modular .NET 9/10 (C# 12/13)** | Solução única modular com divisão estrita de domínios em projetos/pastas. |
| **Banco de Dados** | **PostgreSQL 18 + TimescaleDB (Self-Hosted)** | Séries temporais financeiras via Docker. *(Fallback: PostgreSQL 18 particionado por data).* |
| **Cache & In-Memory** | **Redis** | Cache de cotações, agregados e respostas do Backtest. |
| **Mensageria / Eventos**| **RabbitMQ (via MassTransit)** | Ingestão assíncrona de preços, invalidação de cache e reprocessamentos. |
| **Observabilidade** | **OpenTelemetry + Jaeger** | Distributed Tracing unificado entre Next.js, .NET API, Worker, Redis e Postgres. |
| **Containers** | **Docker + Docker Compose** | Ambiente isolado de desenvolvimento e produção (Postgres/Timescale, Redis, RabbitMQ, Jaeger). |

---

## 2. Arquitetura do Frontend: SSR-First, TanStack Ecosystem & PWA

### 2.1. Padrão SSR-First + TanStack Query Hydration
Para garantir pontuação máxima de SEO e Core Web Vitals no Google:
1. **Server Component (`page.tsx`)**: Executa `prefetchQuery` chamando a API .NET internamente e desidrata o estado com `dehydrate(queryClient)`.
2. **Client Component**: Consome via `<HydrationBoundary state={dehydratedState}>` e `useQuery()`.
3. **Resultado:** O HTML inicial chega completo para os robôs de busca (Googlebot), e o navegador assume o estado sem requisições adicionais ou *layout shift*.

### 2.2. O Ecossistema TanStack no IndexDesk
* **`@tanstack/react-query`**: Gerencia todo o ciclo de vida dos dados remotos, cache com `staleTime` configurado e revalidação inteligente em background.
* **`@tanstack/react-table`**: Modela as grades de comparação de ativos, lâmina de composição de fundos da CVM e matrizes de risco com ordenação, filtros por coluna e paginação.
* **`@tanstack/react-form`**: Gerencia formulários complexos de simulação de carteiras (pesos dinâmicos somando 100%, aportes mensais, escolha de benchmark).
* **`@tanstack/react-virtual`**: Renderiza somente os elementos visíveis na viewport, permitindo rolar tabelas com 5.000+ linhas de cotações diárias a 60 FPS com consumo mínimo de memória.
* **`@tanstack/react-hotkeys`**: Habilita atalhos como `Ctrl + K` (busca global de tickers), `Alt + C` (abrir comparador) e navegação rápida de abas.
* **`@tanstack/db` / TanStack Store**: Base de dados local para persistência de dados de ETFs mais acessados em IndexedDB/Storage, alimentando a experiência offline.

### 2.3. Toolchain de Qualidade: Oxlint, Oxfmt, TypeScript 7 e C#

#### JavaScript/TypeScript (`apps/web`)

* **Oxlint (latest):** usar `oxlint.config.ts` com `defineConfig`, presets TypeScript/React/Next.js e regras recomendadas. Habilitar `--type-aware` somente após medir o custo no CI; erros de correctness/imports/typescript devem ser `error`.
* **Oxfmt (latest):** usar `oxfmt.config.ts` com ignores para `.next/**`, `out/**`, `dist/**`, `coverage/**` e `node_modules/**`. Oxfmt é o formatter oficial de JS/TS/JSON/Markdown do workspace.
* **TypeScript 7:** fixar a versão no workspace quando publicada/validada. O `tsconfig` deve seguir o bundler moderno, sem emitir JavaScript:

```json
{
  "compilerOptions": {
    "target": "ES2024",
    "lib": ["ES2024", "DOM", "DOM.Iterable"],
    "module": "preserve",
    "moduleResolution": "bundler",
    "noEmit": true,
    "strict": true,
    "isolatedModules": true,
    "verbatimModuleSyntax": true,
    "resolveJsonModule": true,
    "skipLibCheck": true,
    "allowJs": false,
    "noUncheckedIndexedAccess": true,
    "noImplicitOverride": true,
    "plugins": [{ "name": "next" }]
  }
}
```

O target deve acompanhar o baseline real de browsers/runtime suportado pelo Next.js e pelos browsers do produto; não usar `ESNext` indiscriminadamente em produção sem validar polyfills. `module: preserve` + `moduleResolution: bundler` modela corretamente Bun, Turbopack e conditional exports.

#### C# (`apps/backend`)

* **CSharpier (latest):** instalar como ferramenta local via `.config/dotnet-tools.json`; `dotnet csharpier format .` formata e `dotnet csharpier check .` valida no CI.
* **`dotnet format`:** executar `dotnet format style` e `dotnet format analyzers` para regras do SDK/analyzers. Não usar `dotnet format whitespace` junto com CSharpier para evitar disputa de whitespace.
* **CI/Turborepo:** `turbo run format`, `turbo run lint` e `turbo run typecheck` devem chamar Oxfmt/Oxlint/TypeScript no web e CSharpier/`dotnet format`/`dotnet build` no backend.

### 2.4. PWA & Offline-First com Serwist (`@serwist/next`) + Turbopack
* **Por que Serwist:** É o sucessor moderno e mantido do `next-pwa` / `workbox`, com suporte nativo a TypeScript e compatibilidade com o bundler **Turbopack** do Next.js.
* **Estratégias de Cache no Service Worker:**
  * **Páginas de Ativos (`/etf/*`, `/bdr/*`):** *Stale-While-Revalidate* — exibe imediatamente a versão salva em cache do ETF e atualiza silenciosamente em segundo plano.
  * **Simulador de Backtest e Calculadoras:** *Cache-First* para assets e rotas, permitindo simulações de carteiras já cacheadas mesmo no metrô ou sem sinal de internet.
  * **Manifest PWA:** Suporte a instalação como aplicativo nativo no iOS, Android, macOS e Windows.

---

## 3. Guia de Gráficos e Visualização de Dados Financeiros 📈

Para uma plataforma de inteligência de ETFs e BDRs, recomendamos a abordagem híbrida de visualização:

| Tipo de Visualização | Biblioteca Recomendada | Justificativa Técnica |
| :--- | :--- | :--- |
| **Séries Temporais / Cotações / Curva de Patrimônio do Backtest / Drawdown** | **TradingView Lightweight Charts (`lightweight-charts`)** | **Padrão ouro do mercado financeiro.** Baseado em HTML5 Canvas/WebGL, ultraleve (~40KB bundle), renderiza 100.000+ pontos a 60 FPS com pan/zoom fluido e crosshair nativo com preços formatados em R$. |
| **Alocação de Carteira / Top 10 Holdings / Donut & Pie Charts** | **Recharts** (ou **Tremor / Shadcn UI Charts**) | Baseado em SVG e componentes React. Excelente integração estética com Tailwind CSS v4, suporte a temas Claro/Escuro e tooltips animados. |
| **Matriz de Overlap / Diagrama de Venn / Heatmap de Correlação** | **Visx (Airbnb)** ou **TanStack React Charts** | Primitivas modulares de baixo nível em SVG/D3 para gráficos matemáticos complexos e customizados que bibliotecas fechadas não suportam. |

---

## 4. Arquitetura do Monólito Modular .NET

```text
┌─────────────────────────────────────────────────────────────────────────┐
│                      Next.js 16.3 Frontend (apps/web)                   │
│      (SSR-First, TanStack Suite, Serwist PWA, zustand, nuqs)            │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ (HTTP REST / OpenAPI Client)
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    IndexDesk.Api (Host HTTP Único)                      │
│   - Endpoints Minimal APIs / Controllers com OpenAPI (Scalar / Swagger) │
│   - Autenticação JWT & Cookies HttpOnly                                 │
│   - OpenTelemetry Instrumentation Middleware                            │
└──────────────┬─────────────────────┬─────────────────────┬──────────────┘
               │ (Chamadas diretas / │ MediatR em memória) │
               ▼                     ▼                     ▼
┌──────────────────────────┐ ┌──────────────────────────┐ ┌──────────────┐
│       Modules.Auth       │ │    Modules.MarketData    │ │  Modules.    │
│ (Usuários, JWT, Hash)    │ │ (Consultas de Cotações)  │ │  Analytics   │
│                          │ │                          │ │  (Backtest,  │
│                          │ │                          │ │   Sharpe)    │
└──────────────┬───────────┘ └───────────┬──────────────┘ └──────┬───────┘
               │                         │                       │
               ▼                         ▼                       ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                          BuildingBlocks Shared                          │
│   - Persistence (Postgres 18 / TimescaleDB via EF Core + Npgsql COPY)   │
│   - Cache (StackExchange.Redis com políticas de TTL)                    │
│   - Resilience (Polly: Retry, Circuit Breaker, RateLimiting)            │
│   - Messaging (MassTransit + RabbitMQ)                                  │
│   - Observability (OpenTelemetry SDK + Jaeger Exporter)                 │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │ (Eventos RabbitMQ / Ingestão)
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                  IndexDesk.Worker (Background Ingest)                   │
│   - Quartz.NET Schedulers (BCB 23h UTC, CVM 04h, Brapi pós-fechamento)  │
│   - Ingestão CVM em Streaming (CsvHelper + NpgsqlBinaryImporter COPY)   │
│   - Publicador de eventos de atualização de cotações                    │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 5. Estrutura do Monorepo e Projetos

```text
indexdesk-monorepo/
├── apps/
│   ├── web/                         # Next.js 16.3 Frontend (SSR-First + PWA)
│   │   └── src/
│   │       ├── app/                 # App Router (Páginas Públicas, SEO, (admin) Backoffice)
│   │       │   ├── (public)/        # Rotas Públicas (/etf/[ticker], /comparador, /ferramentas)
│   │       │   └── (admin)/         # Painel Admin (/admin/assets, /admin/sync-jobs, /admin/holdings)
│   │       ├── components/          # Shadcn UI + Gráficos (Lightweight Charts + Recharts)
│   │       ├── hooks/               # Custom hooks TanStack Query / Table / Hotkeys
│   │       ├── lib/                 # Cliente API tipado, Serwist SW setup, Utils
│   │       ├── stores/              # Estado de cliente (zustand + TanStack DB)
│   │       └── search-params/       # Estado de URL tipado (nuqs)
│   └── backend/                     # Solução Monólito Modular .NET
│       ├── IndexDesk.sln
│       └── src/
│           ├── IndexDesk.Api/       # Entrypoint HTTP Web API + OpenAPI
│           ├── IndexDesk.Worker/    # Background Ingestion Host (Quartz.NET)
│           ├── Modules/
│           │   ├── Auth/            # IndexDesk.Modules.Auth
│           │   ├── MarketData/      # IndexDesk.Modules.MarketData
│           │   ├── Analytics/       # IndexDesk.Modules.Analytics
│           │   └── Portfolio/       # IndexDesk.Modules.Portfolio (Fase 2: Carteiras, Renda Fixa CDI, DARF)
│           └── BuildingBlocks/
│               ├── BuildingBlocks.Common/
│               ├── BuildingBlocks.Persistence/     # EF Core + Timescale/Postgres + Npgsql COPY
│               ├── BuildingBlocks.Cache/           # Redis Cache Service
│               ├── BuildingBlocks.Resilience/      # Políticas Polly
│               ├── BuildingBlocks.Messaging/       # MassTransit + RabbitMQ
│               └── BuildingBlocks.Observability/   # OpenTelemetry SDK + Jaeger
├── docker-compose.yml               # Postgres/TimescaleDB, Redis, RabbitMQ, Jaeger
├── turbo.json                       # Configuração do Turborepo
└── README.md
```

---

## 6. Turborepo + .NET Build System

* **Caching de Builds .NET:** Configurar `outputs` no `turbo.json` apontando para `apps/backend/**/bin/**` e `apps/backend/**/obj/**`.
* **Tarefas integradas:**
  * `turbo run build`: Compila o Next.js com Turbopack (`next build`) e a solução .NET (`dotnet build -c Release`).
  * `turbo run lint`: Executa Oxlint (latest) no web e `dotnet format analyzers`/analyzers C# no backend.
  * `turbo run format`: Executa Oxfmt (latest) no web e `dotnet csharpier format` no backend; CI usa os comandos equivalentes em modo check.
  * `turbo run typecheck`: Executa TypeScript 7 com o tsconfig moderno (`tsc --noEmit`) e compilação/analyzers .NET.
  * `turbo run test`: Executa testes unitários no frontend (Bun test/Vitest) e backend (`dotnet test`).
  * `turbo run dev`: Inicializa o dev server Next.js (Turbopack) e o backend via `dotnet watch`.
