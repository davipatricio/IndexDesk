# Backend — apps/backend (.NET 9 Monólito Modular)

Suplemento de `apps/backend/CLAUDE.md` (fonte detalhada). Solução única `IndexDesk.sln` — **nunca**
criar microserviços separados nem YARP no MVP.

## Layout

```
src/IndexDesk.Api/        Host HTTP único: Minimal APIs + OpenAPI (Scalar, dev only)
src/IndexDesk.Worker/     Host background: Quartz.NET (ingestão de provedores)
src/Modules/Auth|MarketData|Analytics/   Módulos delimitados (Portfolio = Fase 03)
src/BuildingBlocks/{Common,Persistence,Cache,Resilience,Messaging,Observability}/
tests/IndexDesk.UnitTests | tests/IndexDesk.IntegrationTests
```

## Compilador (Directory.Build.props — globalDependency do Turbo)

`net9.0` · C# `13.0` · Nullable enable · ImplicitUsings enable · `EnforceCodeStyleInBuild=true` ·
`AnalysisLevel latest-recommended` · `TreatWarningsAsErrors=false` (intencional).

## Padrão de módulo (seguir exatamente)

Cada módulo expõe dois métodos estáticos de extensão no host:

```csharp
public static class XModuleExtensions
{
    public static IServiceCollection AddXModule(this IServiceCollection services) { ... }
    public static IEndpointRouteBuilder MapXEndpoints(this IEndpointRouteBuilder app) { ... }
}
```

- Endpoints Minimal API agrupados em `/api/v1/<module>` com `.WithTags(...)`.
- Requests/responses são `record`s imutáveis; validação → `Results.BadRequest(...)`.
- Lógica financeira (Sharpe, drawdown, Fisher, backtest) em `Calculators/` como funções puras.

## Direção de dependências (estrita)

- `Modules/*` → somente `BuildingBlocks/*`. Módulos **não** referenciam módulos.
- `BuildingBlocks/*` → zero conhecimento de domínio.
- Api/Worker compõem módulos + building blocks. Apenas MarketData referencia ASP.NET Core App framework.

## BuildingBlocks (o que cada um faz)

- **Persistence:** EF Core + Npgsql; escrita em lote via `NpgsqlBinaryImporter` (COPY); upserts idempotentes.
- **Cache:** `ICacheService` sobre StackExchange.Redis; **preservar** fallback em memória (`InMemoryCacheFallback`) quando Redis está fora localmente.
- **Resilience:** Polly (retry/backoff, circuit breaker, rate limiter).
- **Messaging:** MassTransit + RabbitMQ (eventos de ingestão/invalidação de cache).
- **Observability:** `AddIndexDeskObservability(...)` OTel → Jaeger; chamado primeiro no Program.cs dos hosts.
- **Common:** helpers `Results`, `Pagination`, `Time`.

## Hosts

**Api:** JWT Bearer + refresh em cookie HttpOnly (módulo Auth); Scalar em `/scalar/v1` só em Development via
`MapOpenApi()` (**sem Swashbuckle**); CORS `"AllowWeb"` para origem web (`Web:AppUrl`, default :3000) com credentials;
health em `/health`; `public partial class Program {}` obrigatório p/ WebApplicationFactory — não remover.

**Worker:** `Host.CreateApplicationBuilder` + `AddQuartz`; `AddQuartzHostedService(q => q.WaitForJobsToComplete = true)`;
jobs por cron (ex.: BCB `0 0 23 ? * * *`), grupo `MarketDataIngest`; novos jobs seguem o mesmo padrão.

## Formatação C# (divisão estrita)

- **CSharpier** = whitespace/layout apenas (`dotnet csharpier format .` / `check .`).
- **dotnet format style / analyzers** = estilo e diagnósticos SDK.
- **NUNCA rodar `dotnet format whitespace`** (disputa whitespace com CSharpier).

## Testes

xUnit 2.9.3 + FluentAssertions 8.0.1 + coverlet. UnitTests = lógica pura (Calculators);
IntegrationTests = `WebApplicationFactory<Program>` contra o Api. Novo recurso de módulo:
1 teste unitário da calculadora/endpoint + 1 teste de integração do grupo de endpoints.
