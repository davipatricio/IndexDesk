# Backend — Módulo `IndexDesk.Modules.Portfolio`

Segue `how-tos/backend-module.md` (pattern 2 extensões: `AddPortfolioModule` / `MapPortfolioEndpoints`).
Referencia `BuildingBlocks.Persistence` (EF Core, `IndexDeskDbContext`) e o módulo Auth
(`PERMISSION:` policies `portfolio:read`/`portfolio:write`, user id via claim).

## Endpoints

### M-P1 (fundação)

| Método | Rota | Nome | Permissão |
| :--- | :--- | :--- | :--- |
| POST | `/api/v1/portfolios` | CreatePortfolio | portfolio:write (limite 3) |
| GET | `/api/v1/portfolios` | ListPortfolios | autenticado |
| GET | `/api/v1/portfolios/{id}` | GetPortfolio (resumo + posições) | dono |
| PATCH | `/api/v1/portfolios/{id}` | UpdatePortfolio | dono |
| DELETE | `/api/v1/portfolios/{id}` | DeletePortfolio | dono |
| POST | `/api/v1/portfolios/{id}/transactions` | CreateTransaction | dono |
| GET | `/api/v1/portfolios/{id}/transactions` | ListTransactions (paginado/filtro) | dono |
| PUT | `/api/v1/portfolios/{id}/transactions/{txId}` | AmendTransaction (edição lógica) | dono |

Contrato de erro segue padrão do repo: `{ code, message }` com códigos
`Portfolio.NotFound`, `Portfolio.LimitReached`, `Portfolio.TransactionInvalid`,
`Portfolio.AssetNotFound`.

### M-P2+ (performance)

```
GET /api/v1/portfolios/{id}/performance?from&to&benchmark=CDI,IPCA,IBOV   TWR/MWR/simples
GET /api/v1/portfolios/{id}/allocation                                    atual vs alvo
GET /api/v1/portfolios/{id}/timeline                                      vencimentos RF
```

### M-P4/M-P5

```
POST /api/v1/portfolios/{id}/simulate-redemption   resgate parcial/total
GET  /api/v1/portfolios/{id}/tax-projection        DARF projetado
GET  /api/v1/portfolios/{id}/tax-ledger            prejuízos por classe
CRUD /api/v1/portfolios/{id}/goals                 metas + projeção
POST /api/v1/portfolios/{id}/share-link            gerar/revogar link restrito
GET  /api/v1/public/portfolios/{slug}              página pública (sem auth)
POST /api/v1/portfolios/clone-from/{sourceId}      clonar pública
```

## Serviços

- `IPortfolioService` — CRUD carteiras + limite de 3 + slug para público.
- `ITransactionService` — validação de tipos/sinais, retroativo ilimitado, edição lógica.
- `IPositionProjector` — reconstrói posições a partir das transações vigentes; PM ponderado com
  despesas; SELL reduz quantidade sem tocar PM; CORP_ACTION ajusta qtd/PM; INCOME soma caixa;
  TRANSFER move custo entre carteiras sem ganho falso. Valuation com último close de
  `asset_quotes` (+ `fx_rates` quando moeda estrangeira). Idempotente e re-executável.

## Jobs do Worker

| Job | Agenda | Função | Marco |
| :--- | :--- | :--- | :--- |
| `PortfolioAccrualDailyJob` | diário pós MarketData/FX (~22:40 UTC) | accrual RF/previdência/sintético com séries macro locais; idempotente | M-P3 |
| `PortfolioSnapshotDailyJob` | diário ~23:30 UTC | upsert `portfolio_daily_snapshots`; idempotente | M-P2 |
| `PortfolioValuationRefreshJob` | sob demanda/pós-sync | reprojeta summary quando cotações mudam | M-P1 (básico) |

## Testes

- Unitários (xUnit + FluentAssertions, projeto `IndexDesk.UnitTests`): calculadores puros de PM,
  projeção de posições (compra/venda/corp action/transfer), validações de transação.
- Integração (`IndexDesk.IntegrationTests`): CRUD de carteiras com limite, fluxo
  transação→posição, isolamento entre usuários (403/404 cross-user).

