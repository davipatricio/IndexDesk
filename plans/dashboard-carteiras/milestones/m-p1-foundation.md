# Marco M-P1 — Fundação

Objetivo: usuário cria carteira, lança transações BUY/SELL/INCOME e vê posições com PM e valuation
na home consolidada mínima.

## DDL

- [x] `docker/init-db/03-portfolio.sql` com tabelas `portfolios`, `portfolio_transactions`,
      `portfolio_positions_summary` (+ índices) — colunas PascalCase (convenção EF do repo)
- [ ] DDL aplicado no banco dev local (verificado via psql)

## Backend — entidades & infraestrutura

- [x] `PortfolioEntity` / `PortfolioTransactionEntity` em
      `BuildingBlocks.Persistence/Entities` + mapeamento no `IndexDeskDbContext`
- [x] Módulo novo `IndexDesk.Modules.Portfolio` (`AddPortfolioModule`/`MapPortfolioEndpoints`)
- [x] Referência do módulo adicionada a `IndexDesk.Api.csproj` + wiring em `Program.cs`
- [x] DDL aplicado e verificado no banco dev (smoke E2E via curl: signup → carteira → transação → resumo)
- [x] ⛔→✅ pré-requisitos ausentes no banco recriado: `docker/init-db/02-auth-and-fx.sql`
      (tabelas de auth + seed RBAC + `fx_rates`)

## Backend — serviços & endpoints

- [x] `IPortfolioService` — CRUD + limite de 3 por usuário + ownership check (404 cross-user)
- [x] `ITransactionService` — criar/listar/amendar transações; validação de tipo/sinais/data;
      edição lógica (`amended_transaction_id`)
- [x] `AveragePriceCalculator` puro (PM com fees, SELL sem tocar PM)
- [x] `PositionProjector` — BUY/SELL/INCOME/CORP_ACTION(split,bôn,subsc)/TRANSFER + valuation com
      último close de `asset_quotes` (+ `fx_rates` p/ moeda estrangeira); idempotente
- [x] Endpoints `/api/v1/portfolios` (+ `/lookup/{ticker}` para o wizard) (CRUD), `/transactions` (POST/GET/PUT), resumo com posições
- [x] DTOs OpenAPI anotados (`WithName`/`WithSummary`), códigos de erro padronizados

## Testes

- [x] Unitários `AveragePriceCalculatorTests` (compra simples, fees, múltiplas compras, venda,
      venda total, venda além da posição = erro)
- [x] Unitários `PositionProjectorTests` (BUY→posição, SELL parcial, INCOME soma caixa, split,
      bonificação, subscrição, transferência entre carteiras sem ganho falso, FX congelado)
- [x] Smoke manual cobre limite de 3 carteiras (409) e isolamento por usuário (ownership check)
- [x] Integração automatizada (WebApplicationFactory + indexdesk_test): limite 3 → 409, cross-user 404, BUY+INCOME→resumo com fees no PM, edição lógica + isolamento cross-user (403/404)

## Frontend mínimo

[x] Grupo `(dashboard)` com layout autenticado (nav + guard client-side)
- [x] `/dashboard`: hero patrimônio consolidado + cards das carteiras (tabela agregada entra em M-P2)
- [x] `/dashboard/carteiras/nova`: form título/descrição/perfil
- [x] `/dashboard/c/[id]`: resumo + tabela de posições da carteira + botão nova transação
- [x] `/dashboard/c/[id]/transacoes/nova`: wizard 3 etapas + revisão (BUY/SELL/INCOME) com lookup de ticker
- [x] Empty state guiado quando zero carteiras
- [x] Fetchers/DTOs em `lib/api-client.ts`

## Correções colaterais descobertas nos testes

- **Auth bug fix:** token de signup saía sem claims `permission`/`role`
  (SaveChanges acontecia depois de IssueTokensAsync) → 403 em todo endpoint
  protegido para usuário recém-criado. Fix em AuthService.SignUpAsync.

## Fora deste marco

Snapshots/TWR/MWR/benchmarks (M-P2), RF/accrual (M-P3), fiscal (M-P4), público/metas/export (M-P5).
