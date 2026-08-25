# Marco M-P1 — Fundação

Objetivo: usuário cria carteira, lança transações BUY/SELL/INCOME e vê posições com PM e valuation
na home consolidada mínima.

## DDL

- [ ] `docker/init-db/02-portfolio.sql` com tabelas `portfolios`, `portfolio_transactions`,
      `portfolio_positions_summary` (+ índices)
- [ ] DDL aplicado no banco dev local (verificado via psql)

## Backend — entidades & infraestrutura

- [ ] `PortfolioEntity` / `PortfolioTransactionEntity` em
      `BuildingBlocks.Persistence/Entities` + mapeamento no `IndexDeskDbContext`
- [ ] Módulo novo `IndexDesk.Modules.Portfolio` (`AddPortfolioModule`/`MapPortfolioExtensions`)
- [ ] Referência do módulo adicionada a `IndexDesk.Api.csproj` + wiring em `Program.cs`
- [ ] DDL versionado também como nota em `data-model.md` (feito) — pendente: rodar no banco

## Backend — serviços & endpoints

- [ ] `IPortfolioService` — CRUD + limite de 3 por usuário + ownership check (404 cross-user)
- [ ] `ITransactionService` — criar/listar/amendar transações; validação de tipo/sinais/data;
      edição lógica (`amended_transaction_id`)
- [ ] `AveragePriceCalculator` puro (PM com fees, SELL sem tocar PM)
- [ ] `PositionProjector` — BUY/SELL/INCOME/CORP_ACTION(split,bôn,subsc)/TRANSFER + valuation com
      último close de `asset_quotes` (+ `fx_rates` p/ moeda estrangeira); idempotente
- [ ] Endpoints `/api/v1/portfolios` (CRUD), `/transactions` (POST/GET/PUT), resumo com posições
- [ ] DTOs OpenAPI anotados (`WithName`/`WithSummary`), códigos de erro padronizados

## Testes

- [ ] Unitários `AveragePriceCalculatorTests` (compra simples, fees, múltiplas compras, venda,
      venda total, venda além da posição = erro)
- [ ] Unitários `PositionProjectorTests` (BUY→posição, SELL parcial, INCOME soma caixa, split,
      bonificação, subscrição, transferência entre carteiras sem ganho falso, FX congelado)
- [ ] Integração: limite de 3 carteiras + isolamento cross-user (403/404)

## Frontend mínimo

- [ ] Grupo `(dashboard)` com layout autenticado (nav + guard client-side)
- [ ] `/dashboard`: hero patrimônio consolidado + tabela de posições agregadas + cards das carteiras
- [ ] `/dashboard/carteiras/nova`: form título/descrição/perfil
- [ ] `/dashboard/c/[id]`: resumo + tabela de posições da carteira + botão nova transação
- [ ] `/dashboard/c/[id]/transacoes/nova`: wizard 3 etapas + revisão (BUY/SELL/INCOME)
- [ ] Empty state guiado quando zero carteiras
- [ ] Fetchers/DTOs em `lib/api-client.ts`

## Fora deste marco

Snapshots/TWR/MWR/benchmarks (M-P2), RF/accrual (M-P3), fiscal (M-P4), público/metas/export (M-P5).
