# MarketData/Health — Agregação de saúde dos provedores (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md). Este doc detalha o que o mapa de pastas do módulo resume para `Health/`.

## Responsabilidade

Derivar status de saúde por provedor a partir das linhas de `sync_job_logs`. A agregação é **pura e
estática**: recebe os logs já materializados e devolve os DTOs de `Domain/ProviderHealthDtos.cs` —
zero I/O, testável offline sem banco.

## Inventário

- `ProviderHealthAggregator.cs` — classe estática `internal`:
  - `KnownProviders`: catálogo fixo (`Brapi`, `YahooFinance`, `HGBrasil`, `BCB`, `CVM`) com display name
    e papel em pt-BR; ordem do catálogo define o contrato estável da resposta.
  - `BuildProviders(logs)` e overload `BuildProviders(logs, keyStatsByProvider)`: agrupa logs por
    `ProviderName`, ordena por `StartedAt desc`; providers conhecidos primeiro, depois os que aparecem
    só nos logs/pool (ex.: `TradingView`, `AwesomeApi`).
  - `OverallStatus(providers)`: todos NEVER_SYNCED → `UNKNOWN`; qualquer UNHEALTHY → `UNHEALTHY`;
    qualquer DEGRADED → `DEGRADED`; senão `HEALTHY`.
  - Mapeamento por provider: último log `FAILED` → UNHEALTHY, `PARTIAL_WARNING` → DEGRADED, resto HEALTHY;
    sem logs → `NEVER_SYNCED`. `Totals` soma jobs/records/status counts; `Issues` lista as últimas
    20 linhas FAILED/PARTIAL_WARNING.
  - `DescribeUnknown(key)`: rótulos para `ALL` (pipeline de fallback, falha total) e `UNKNOWN`
    (erro interno não atribuído).

## Consumo

- `Services/ProviderHealthService` busca `SyncJobLogs` (`AsNoTracking`), junta o `IApiKeyPool.Snapshot()`
  (contadores por índice de chave) e chama este agregador → endpoint `GET /api/v1/providers/health`.
- Status derivado alimenta o dashboard operacional; NÃO é health check de processo
  (`/health` do host é outra coisa).

## Regras locais

- **Nenhuma query nova aqui**: se precisar de mais dado, estenda a query em `Services/` e passe o
  resultado como parâmetro — a pasta continua pura.
- Status é derivado **do log mais recente** por provider, não da janela inteira; a janela só alimenta totais/issues.
- `Provider.PoolExhausted` / `Provider.CircuitOpen` chegam como `PARTIAL_WARNING` nos logs (decisão de
  `Ingestion/DailyCloseChain`); aqui viram DEGRADED — degradação esperada nunca deve pintar UNHEALTHY
  sem um FAILED real.
- Contadores de chave são identificados por índice (`#{Index}`); valor de chave jamais entra nos DTOs.
- Novo provider conhecido = nova linha em `KnownProviders` (display/role pt-BR) + entrada correspondente
  no job que grava `sync_job_logs` com o mesmo `ProviderName` — as duas pontas têm que bater exato.
