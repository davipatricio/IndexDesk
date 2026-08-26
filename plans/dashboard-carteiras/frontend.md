# Frontend — Dashboard de Carteiras (`apps/web`)

Primeira superfície **autenticada** do app. Grupo de rotas novo `(dashboard)` ao lado de `(public)`.
Auth: consome `/api/v1/auth/*` existente; middleware redireciona sem sessão.

## Rotas

| Rota | Conteúdo | Marco |
| :--- | :--- | :--- |
| `(dashboard)/dashboard` | Home = consolidado: hero patrimônio + Δ dia + retorno período, chart área com aportes, tabela de posições, alocação por classe, timeline vencimentos, cards das carteiras | M-P1 (mínimo) → M-P2 completo |
| `(dashboard)/dashboard/carteiras/nova` | Wizard 3 etapas + revisão de carteira | M-P1 |
| `(dashboard)/dashboard/c/[id]` | Detalhe da carteira com abas Posições · Análise · Metas · Fiscal · Transações | M-P1 mínimo, abas por marco |
| `(dashboard)/dashboard/c/[id]/transacoes/nova` | Wizard de transação (reuso do stepper) | M-P1 |
| `c/[slug]` (grupo público) | Página pública indexável SSG/ISR + JSON-LD + OG dinâmico; respeita `public_values_mode` e identidade | M-P5 |

## Componentes-chave

- `PortfolioHero` — patrimônio grande, Δ dia, retorno do período selecionado.
- `WealthAreaChart` — lightweight-charts v5 (padrão do `price-hero-chart.tsx`), marcações de aporte,
  benchmarks sobrepostos (CDI/IPCA/IBOV via `macro-series`).
- `PositionsTable` — tabela densa padrão do repo (header sticky, tabular), sparklines server-rendered,
  coluna contribuição, agrupamento por corretora com sub-total.
- `AllocationBars` — % atual vs alvo com desvio verde/vermelho.
- `MaturityTimeline` — vencimentos/carências RF.
- `RedemptionSimulator` — resgate parcial/total com IR/IOF/come-cotas/isenções + disclaimer.
- `GoalCards` — multi-metas com progresso e projeção run-rate/juros.
- `WizardStepper` — 3 passos + revisão, reutilizado por carteira/transação.

## Estados

- Demo seedada deletável **e** empty state guiado no primeiro acesso (decisão grill §6).
- Offline: Serwist cacheia última leitura read-only.
- Mobile-first: tabelas viram cards empilhados; tema segue sistema.

## Dados

- Fetchers em `lib/api-client.ts` seguindo DTOs existentes.
- SSR-first: React Query `prefetchQuery` + dehydration na home/detalhe; mutações no client.
- Estado de URL com `nuqs` para período/benchmark/aba (mesma convenção de `/rankings`).

## Export

CSV + XLSX gerados server-side (`Content-Disposition`) nas telas Posições e Transações. M-P5.
