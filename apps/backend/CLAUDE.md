# apps/backend — Backend Instructions (scoped)

> Scoped supplement to root [`CLAUDE.md`](../../CLAUDE.md) and [`STACK_SETUP.md`](../../STACK_SETUP.md).
> Covers the **.NET 9 Modular Monolith** (`IndexDesk.sln`). Product docs call the platform _ETFHub B3_;
> code/branding uses **IndexDesk**. **Do not modify code** when only instruction updates are requested —
> edits here are markdown only.

## 1. What this workspace is

A single **Modular Monolith** solution (`IndexDesk.sln`, .NET 9, C# 13) with two hosts and strictly bounded
domain modules:

- **`IndexDesk.Api`** — ASP.NET Core HTTP host. Minimal API endpoint groups + OpenAPI served by Scalar UI (dev only).
- **`IndexDesk.Worker`** — background host. **Quartz.NET** schedulers for external data ingestion.
- **`Modules/`** — domain boundaries: `Auth`, `MarketData`, `Analytics`, `Portfolio` (Fase 2/3).
- **`BuildingBlocks/`** — shared infra: `Common`, `Persistence`, `Cache`, `Resilience`, `Messaging`, `Observability`.

External providers are read **only** by `IndexDesk.Worker` on a schedule — see Local-First §6.

## 2. Target framework & compiler (from `Directory.Build.props`)

| Setting                   | Value                                                                           |
| :------------------------ | :------------------------------------------------------------------------------ |
| `TargetFramework`         | `net9.0`                                                                        |
| `LangVersion`             | `13.0`                                                                          |
| `Nullable`                | `enable`                                                                        |
| `ImplicitUsings`          | `enable`                                                                        |
| `EnforceCodeStyleInBuild` | `true` (`.editorconfig` + analyzers run during build)                           |
| `AnalysisLevel`           | `latest-recommended`                                                            |
| `TreatWarningsAsErrors`   | `false` (intentionally not strict — keep it that way unless changed via review) |

> `Directory.Build.props` is a **global** turbo `globalDependency`. Editing it invalidates all backend caches.

## 3. Commands (run from repo root via Turborepo, or `cd apps/backend`)

```bash
# Whole monorepo (preferred):
bun run dev            # next dev (web) + dotnet watch (backend worker/api)
bun run build          # next build + dotnet build -c Release
bun run lint           # oxlint (web) + dotnet format analyzers (backend)
bun run typecheck      # tsc (web) + dotnet build/analyzers (backend)
bun run test           # vitest (web) + dotnet test (backend)
bun run format         # oxfmt (web) + dotnet csharpier format (backend)
bun run format:check   # CI gate: oxfmt --check + csharpier check

# Backend-only (cd apps/backend):
dotnet build -c Release            # build the solution
dotnet watch --project src/IndexDesk.Api     # hot-reload the API
dotnet watch --project src/IndexDesk.Worker  # hot-reload the Worker
dotnet test                        # run Unit + Integration test projects
dotnet test tests/IndexDesk.UnitTests
dotnet test tests/IndexDesk.IntegrationTests
```

> Turbo `build` caches `bin/**` and `obj/**` (`outputs`). `test` `dependsOn: ["build"]`. `dev` is
> persistent/non-cached.

## 4. Formatting & static analysis — CSharpier vs `dotnet format`

Use **both**, with a strict division of labor to avoid formatter conflicts:

- **CSharpier (latest, local .NET tool)** — owns _layout/whitespace_ only.
  - `dotnet csharpier format .` (format) / `dotnet csharpier check .` (CI gate).
  - Install via `.config/dotnet-tools.json` (local tool manifest).
- **`dotnet format`** — owns _style + analyzer_ rules (SDK/`.editorconfig` analyzers).
  - Run `dotnet format style` and `dotnet format analyzers`.
  - **Do NOT run `dotnet format whitespace`** — it fights CSharpier over whitespace. CSharpier wins whitespace;
    `dotnet format` handles everything else.
- **CI gate**: `turbo run format:check` calls `csharpier check` + `oxfmt --check`; `lint` calls
  `dotnet format analyzers` + `oxlint`. Keep `EnforceCodeStyleInBuild=true` so analyzer violations fail the build.

## 5. Module boundaries & conventions

### Module pattern (follow exactly)

Each module exposes two static extension methods on the host:

```csharp
public static class XModuleExtensions
{
    public static IServiceCollection AddXModule(this IServiceCollection services) { ... }
    public static IEndpointRouteBuilder MapXEndpoints(this IEndpointRouteBuilder app) { ... }
}
```

- Hosts wire modules in `Program.cs`:
  `builder.Services.AddAuthModule(...); AddMarketDataModule(); AddAnalyticsModule();`
  and `app.MapAuthEndpoints(); MapMarketDataEndpoints(); MapAnalyticsEndpoints();`
- Endpoints use **Minimal APIs** grouped under `/api/v1/<module>`
  (e.g. `app.MapGroup("/api/v1/analytics").WithTags("Analytics")`). See `AnalyticsModuleExtensions.cs`.
- Requests/responses are **`record`** types (immutable DTOs). Validation returns `Results.BadRequest(...)`.
- Business logic (Sharpe, drawdown, real-yield/Fisher, backtest) lives in `Calculators/` as pure functions.

### Dependency direction (strict)

- `Modules/*` → may reference `BuildingBlocks/*` only. Modules must **not** reference each other directly.
- `BuildingBlocks/*` → no domain knowledge; shared infra only.
- `IndexDesk.Api` / `IndexDesk.Worker` → compose modules + building blocks.
- `MarketData` references `FrameworkReference Include="Microsoft.AspNetCore.App"` (needs endpoint mapping).
  Other modules pull only what they need (no gratuitous framework references).

### Building Blocks

- **Persistence** (`BuildingBlocks.Persistence`): EF Core + Npgsql; batch writes via `NpgsqlBinaryImporter` (COPY).
  Idempotent upserts for re-runnable daily ingests.
- **Cache** (`BuildingBlocks.Cache`): `ICacheService` over StackExchange.Redis. Api falls back to an
  in-memory `ICacheService` (`InMemoryCacheFallback`) when Redis is down locally — preserve that fallback.
- **Resilience** (`BuildingBlocks.Resilience`): Polly pipelines (retry/backoff, circuit breaker, rate limiter).
- **Messaging** (`BuildingBlocks.Messaging`): MassTransit + RabbitMQ for ingest/cache-invalidation events.
- **Observability** (`BuildingBlocks.Observability`): `AddIndexDeskObservability(...)` OpenTelemetry → Jaeger.
  Both Api and Worker call it first in `Program.cs`.
- **Common** (`BuildingBlocks.Common`): `Results`, `Pagination`, `Time` helpers — cross-cutting value types.

### API host specifics (`IndexDesk.Api/Program.cs`)

- JWT Bearer + HttpOnly refresh cookies (Auth module). Scalar UI (`/scalar/v1`) in Development only — served by
  `Microsoft.AspNetCore.OpenApi` (`MapOpenApi()`), **no Swashbuckle/Swagger**.
- CORS policy `"AllowWeb"` allows the web origin (`Web:AppUrl`, default `http://localhost:3000`) with credentials.
- Health check at `/health`. Root `/` returns status + `/scalar/v1` doc link.
- `public partial class Program {}` is required for `WebApplicationFactory` integration tests — do not remove.

### Worker specifics (`IndexDesk.Worker/Program.cs`)

- `Host.CreateApplicationBuilder` + `AddQuartz`. `AddQuartzHostedService(q => q.WaitForJobsToComplete = true)`.
- Ingestion jobs (e.g. `BcbSyncJob`, group `MarketDataIngest`) scheduled by cron — BCB at `0 0 23 ? * * *`
  (23:00 UTC). Add CVM (≈04:00), Brapi (post-close) following the same pattern.

## 6. Local-First constraint (NON-NEGOTIABLE)

No **user request** ever triggers a real-time external API call. Target: backtest/compare hits only local
Postgres + Redis (<~10ms).

- External providers (BCB SGS, CVM open data, Brapi, Yahoo, ANBIMA/B3) are read **only** by `IndexDesk.Worker`
  scheduled jobs, which stream + upsert into Postgres and publish cache-invalidation events to RabbitMQ.
- `Modules.Analytics` consumes those events to invalidate Redis, then re-warms from Postgres.
- **Idempotency:** scheduled re-runs must update, not duplicate (TimescaleDB hypertables or COPY upsert).
- CVM large `inf_diario_fi_YYYYMM.zip` → stream with CsvHelper + strict CNPJ filter + `NpgsqlBinaryImporter`
  (avoid RAM spikes; 50k+ rows/s).
- Secrets/keys live in `.env` (`Providers__*`, `ConnectionStrings__*`) and `appsettings.json` — never in code.

## 7. Testing

- **xUnit** (`2.9.3`) + **FluentAssertions** (`8.0.1`) + **coverlet.collector** (CI coverage).
- Two test projects:
  - `tests/IndexDesk.UnitTests` — pure logic (e.g. `Calculators/FinancialCalculatorsTests.cs`). References
    `BuildingBlocks.Common` + the `Modules/*` projects.
  - `tests/IndexDesk.IntegrationTests` — `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory<Program>`)
    against `IndexDesk.Api`. References only the Api project (see `ApiEndpointsTests.cs`).
- Run via `dotnet test` (Turbo `test` → `dependsOn: build`). Coverage emitted to `TestResults/**` (cached).
- New module features: add a unit test for the calculator/endpoint and an integration test for the endpoint group.
