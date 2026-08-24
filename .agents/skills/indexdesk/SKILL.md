---
name: indexdesk
description: Conhecimento interno do projeto IndexDesk (repositório etfhub-b3) — plataforma de inteligência, backtest e analytics de ETFs e BDRs da B3. Use quando trabalhar neste repositório (apps/web ou apps/backend), implementar funcionalidades, ingestão de provedores, calculadoras/backtests, SEO programático, ou precisar das regras do mercado brasileiro (B3, CVM, imposto de renda/DARF, Tesouro Direto, CDI/Selic/IPCA) e das fórmulas financeiras usadas nos cálculos. Contém também o estado atual do projeto e o roadmap.
metadata:
  author: davipatricio
  scope: repository
---

# IndexDesk — Skill interna do projeto

Plataforma web brasileira focada em **ETFs e BDRs de ETF listados na B3** (mais ações e índices de referência).
Marca no código: **IndexDesk** · Nome nos documentos de produto: **ETFHub B3**.
Monorepo Bun + Turborepo: `apps/web` (Next.js 16 SSR-First) + `apps/backend` (.NET 9 monólito modular).

## Como usar esta skill

Este é o índice com regras que valem sempre. **Leia apenas os arquivos necessários para a tarefa atual**
(divisão pensada para economizar tokens):

### Regras de ouro (valem sempre)

1. **Local-first (NON-NEGOTIABLE):** nenhuma requisição de usuário chama API externa em tempo real.
   O frontend só fala com a API .NET local; provedores são lidos **apenas** pelo `IndexDesk.Worker` agendado.
   → detalhes em [`references/architecture/providers-sync.md`](references/architecture/providers-sync.md)
2. **Nunca hardcodar indicadores macro nem alíquotas fiscais no código.** CDI/Selic/IPCA vêm da tabela
   `macro_economic_series` (séries SGS); regras tributárias por ativo vivem em `etf_metadata.*`.
   → mapeamento em [`market/_overview.md`](market/_overview.md)
3. **Escritas idempotentes:** re-executar um job no mesmo dia atualiza, nunca duplica (COPY/upsert, hypertables).
4. **UI em pt-BR natural:** sem jargão técnico (API, worker, ingestão, IDs de roadmap) no copy visível ao usuário;
   nunca renderizar exceções cruas. Todas as páginas são públicas por padrão (inclusive `/admin`) — sem gates de auth.
   → detalhes em [`references/process/conventions.md`](references/process/conventions.md)
5. **Conteúdo fiscal é educacional:** toda ferramenta/cálculo fiscal exibe disclaimer, premissas e data/fonte dos dados.
   Textos fiscais novos exigem revisão editorial antes de virar verdade de produto (ver RISK-003).
6. **Roadmap não se edita à mão:** atualize `.roadmap/**/*.json` → `bun run roadmap:validate` → `bun run roadmap:generate`.

### Mapa de arquivos

| Arquivo | Quando ler |
| :--- | :--- |
| **references/product/** | |
| [modules.md](references/product/modules.md) | Trabalhar em catálogo `/etf/[ticker]`, comparador, backtest, calculadoras, admin ou hub de notícias |
| [seo-growth.md](references/product/seo-growth.md) | Rotas programáticas, JSON-LD, sitemaps, OG images, ferramentas públicas e conversão |
| **references/architecture/** | |
| [frontend-web.md](references/architecture/frontend-web.md) | Qualquer código em `apps/web`: SSR-first, TanStack, Serwist PWA, gráficos, shadcn/base-nova |
| [backend-dotnet.md](references/architecture/backend-dotnet.md) | Qualquer código em `apps/backend`: módulos, BuildingBlocks, Api/Worker, formatação C#, testes |
| [data-model.md](references/architecture/data-model.md) | Migrations, queries, novas tabelas/campos, hipertables TimescaleDB |
| [providers-sync.md](references/architecture/providers-sync.md) | Ingestão, jobs Quartz, rate limits, cache Redis, resiliência Polly |
| **references/process/** | |
| [project-state.md](references/process/project-state.md) | Saber o que já existe/pronto, bloqueadores atuais, como atualizar o roadmap |
| [current-features.md](references/process/current-features.md) | Antes de usar/estender endpoints, rotas ou jobs — inventário do que **já está implementado** |
| [common-mistakes.md](references/process/common-mistakes.md) | Antes de commit/PR — checklist ❌→✅ de erros comuns deste repo |
| [decisions-risks.md](references/process/decisions-risks.md) | Antes de decidir arquitetura/ferramenta; decisões abertas (DEC-\*) e riscos (RISK-\*) |
| [conventions.md](references/process/conventions.md) | Comandos turbo, quality gates, idioma da UI, visibilidade de rotas, contratos DTO/erro |
| **how-tos/** | |
| [feature-development.md](how-tos/feature-development.md) | Antes de implementar feature — ciclo em 7 passos, quando criar plano em `plans/`, DOD por fase, modo sidecar vs nativo |
| [backend-module.md](how-tos/backend-module.md) | Adicionar módulo backend ou rota — pattern 2 extensões, hosts, DTOs, OpenAPI, persistência/timescale, jobs ingest local-first |
| [validation.md](how-tos/validation.md) | Antes de commit/PR e ao integrar job de ingestão — gates de qualidade (CSharpier vs dotnet format, cache turbo), resiliência pool/breaker, testes offline/smoke, verificação pós-deploy (`sync_job_logs`, health) |
| **market/** | |
| [_overview.md](market/_overview.md) | Sempre que for tocar em regras de mercado/fisco — mapa e regras-fonte de dados |
| [b3-operacoes.md](market/b3-operacoes.md) | Tickers, horários, índices, corporate actions, liquidação, feriados B3 |
| [cvm-regulacao-dados.md](market/cvm-regulacao-dados.md) | Informe diário, CDA, cad_fi, CNPJ, ICVM 555, fluxo/captação |
| [imposto-renda-variavel.md](market/imposto-renda-variavel.md) | IR de ETFs/ações/BDRs: swing/day trade, isenção R$20k (não vale p/ ETF), DARF 6015, compensação |
| [imposto-renda-fixa.md](market/imposto-renda-fixa.md) | Come-cotas, tabela regressiva, Lei 13.043/14 (ETFs RF), LCI/LCA isentas, IOF |
| [tesouro-direto.md](market/tesouro-direto.md) | Tesouro Selic/IPCA+/Prefixado, custódia B3, marcação a mercado |
| [indicadores-selic-cdi-ipca.md](market/indicadores-selic-cdi-ipca.md) | Definições, séries SGS (11/12/433/189), acumulação base 252, conversões de taxas |
| [calculos-financeiros.md](market/calculos-financeiros.md) | Fórmulas: accrual, CAGR, vol, Sharpe, drawdown, Fisher, overlap, PM, TWR/MWR, rebalance |

### Fontes canônicas no repositório

Quando precisar do texto integral: `PRODUCT.md`, `STACK_SETUP.md`, `MODELS.md`, `PROVIDERS.md`,
`PROVIDERS_SYNC.md`, `SEO_TOOLS.md`, `ROADMAP.md` (gerado), além de `CLAUDE.md` raiz e
`apps/*/CLAUDE.md`. Esta skill resume-os; em conflito, prevalece o doc-fonte mais recente.

### CLAUDE.md scoped aninhados (código específico)

Além desta skill, existem `CLAUDE.md`/`AGENTS.md` colados no código que documentam convenções locais —
leia o mais próximo do arquivo que for editar. Top-level: `apps/web/CLAUDE.md`, `apps/backend/CLAUDE.md`,
`apps/backend/src/CLAUDE.md`, hosts (`IndexDesk.Api` com README implícito no CLAUDE, `IndexDesk.Worker`
com README + `Jobs/CLAUDE.md`), os 3 módulos (`Auth`, `Analytics`, `MarketData`) e a coleção
`BuildingBlocks/` (índice + 1 doc por bloco). Módulos também têm docs por subpasta
(`MarketData/{Clients,Ingestion,Resilience,Health,...}`, `Auth/{Endpoints,Security,Services,...}`) e
`tools/providers/sidecar/CLAUDE.md`.
Regras de domínio (mercado/fisco/fórmulas) ficam aqui na skill (`market/*`); regras locais de código
ficam nesses arquivos. Ao criar doc novo, siga o padrão `CLAUDE.md` + symlink `AGENTS.md`.

### Manutenção da skill (definição de pronto)

Mudou estado/features/convenções do projeto? Atualize a skill correspondente antes de encerrar — ver
seção **"Skills internas do repo"** no `CLAUDE.md` raiz.
