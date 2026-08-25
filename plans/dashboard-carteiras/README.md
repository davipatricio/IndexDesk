# Plano Multifile — Dashboard de Carteiras & Portfolios

> Branch: `feat/portfolio-dashboard` · Criado: 2026-08-25 · Origem: grill com o dono (66 decisões).
> Este diretório é o **rastreador vivo** do que foi feito e do que está pendente.
> Expande a fase `PHASE-03` do roadmap (`.roadmap/phases/03-portfolio-automation.json`).

## Como usar

Cada tarefa é um checkbox nos arquivos de marco. Legenda:

- `[x]` ✅ feito (com referência ao commit quando relevante)
- `[ ]` 🔲 pendente
- `⛔ BLOCKED:` pendente com impedimento explícito descrito na linha

Regra: ao concluir uma tarefa, marcar o checkbox no mesmo commit que entrega o código.

## Índice de arquivos

| Arquivo | Conteúdo |
| :--- | :--- |
| [decisions.md](decisions.md) | Registro integral das decisões do grill (fonte de verdade de escopo) |
| [data-model.md](data-model.md) | Schema SQL das tabelas novas do módulo Portfolio |
| [backend-api.md](backend-api.md) | Módulo .NET, endpoints, jobs do Worker |
| [frontend.md](frontend.md) | Rotas, telas, componentes, estados |
| [calculations.md](calculations.md) | Motor de cálculo (PM, TWR/MWR, accrual, fiscal) |
| [milestones/m-p1-foundation.md](milestones/m-p1-foundation.md) | Marco 1 — Fundação (carteiras + transações + posições) |
| [milestones/m-p2-performance.md](milestones/m-p2-performance.md) | Marco 2 — Performance & gráficos |
| [milestones/m-p3-asset-classes.md](milestones/m-p3-asset-classes.md) | Marco 3 — Cobertura completa de classes (RF/accrual etc.) |
| [milestones/m-p4-tax.md](milestones/m-p4-tax.md) | Marco 4 — Fiscal (simulador, ledger) |
| [milestones/m-p5-sharing-goals-export.md](milestones/m-p5-sharing-goals-export.md) | Marco 5 — Compartilhamento, metas, export |

## Status dos marcos

| Marco | Escopo | Status |
| :--- | :--- | :--- |
| M-P1 | Fundação: CRUD carteiras, transações BUY/SELL, PM, posições, home mínima | 🚧 em andamento |
| M-P2 | Performance: snapshots, TWR/MWR, benchmarks, análise | 🔲 não iniciado |
| M-P3 | Classes completas: RF accrual, Tesouro, cripto top100, previdência | 🔲 não iniciado |
| M-P4 | Fiscal: simulador resgate, ledger prejuízos, DARF projeção | 🔲 não iniciado |
| M-P5 | Público/link, clone, metas, alocação-alvo, CSV/XLSX | 🔲 não iniciado |

## Tensões e bloqueios transversais

1. **Edição de transação × imutabilidade:** dono escolheu edição livre; aceite original do roadmap
   pede imutabilidade/auditabilidade. Resolução adotada no plano: edição lógica (nova linha
   `amended_transaction_id`, histórico preservado). Ver `decisions.md §7`.
2. ⛔ **BLOCKED — fonte de cotação cripto top 100:** decidir entre sidecar yfinance (`BTC-USD`
   + fx ou pares `-BRL`) vs extensão AwesomeAPI. PoC necessário antes de M-P3. Enquanto bloqueado,
   cripto entra como preço manual.
3. ⛔ **BLOCKED — catálogo Tesouro Direto:** seed manual de títulos vs raspagem oficial. Não impede
   RF genérica (M-P3 pode começar sem Tesouro específico).
4. ⛔ **BLOCKED — fundos de investimento:** preço de cota automático depende de MVP-003 (informe
   diário CVM, reaberto). MVP usa cota manual por ativo+data.
5. Resumo semanal, notificações, social, calendário de eventos, rebalanceamento, import CSV,
   aportes recorrentes: **explicitamente fora do MVP** (decisão do dono).

## Definição de pronto (por marco)

- Lint/typecheck/test verde (`bun run lint && bun run typecheck && bun run test`)
- DDL aplicado no banco local + script versionado em `docker/init-db/`
- Roadmap regenerado (`bun run roadmap:validate && bun run roadmap:generate`) quando um epic/task
  da PHASE-03 mudar de estado
- Skill `indexdesk` atualizada (`current-features.md`, `project-state.md`)
- UI pt-BR natural, sem exceção crua; superfícies fiscais com disclaimer
