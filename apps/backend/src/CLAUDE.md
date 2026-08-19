# apps/backend/src — Source Conventions (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md). This file is about **how to add code** inside `src/`:
> new modules, endpoints, building blocks, and the dependency rules that keep the monolith modular.
> Read [`../../../CLAUDE.md`](../../../CLAUDE.md) and [`../../../STACK_SETUP.md`](../../../STACK_SETUP.md)
> for product/data-provider context. **Do not modify code** when only instruction updates are requested.

## Layout (mirror exactly when adding projects)

```text
src/
├── IndexDesk.Api/            # HTTP host. Program.cs composes modules + building blocks.
├── IndexDesk.Worker/         # Quartz host. Program.cs registers ingest jobs.
├── Modules/
│   ├── IndexDesk.Modules.Auth/        # users, JWT, HttpOnly refresh, Argon2id/BCrypt
│   ├── IndexDesk.Modules.MarketData/  # asset catalog, quotes, CVM PL history
│   ├── IndexDesk.Modules.Analytics/   # backtest, Sharpe/drawdown, real-yield (Fisher), correlation
│   └── IndexDesk.Modules.Portfolio/   # Fase 2/3: carteiras, CDI accrual, DARF
└── BuildingBlocks/
    ├── BuildingBlocks.Common/        # Results, Pagination, Time (no deps)
    ├── BuildingBlocks.Persistence/   # EF Core + Npgsql + COPY importer
    ├── BuildingBlocks.Cache/         # ICacheService (Redis + InMemory fallback)
    ├── BuildingBlocks.Resilience/     # Polly pipelines
    ├── BuildingBlocks.Messaging/      # MassTransit + RabbitMQ events
    └── BuildingBlocks.Observability/ # OpenTelemetry → Jaeger
```

## Adding a domain module

1. Create `src/Modules/IndexDesk.Modules.<Name>/` with a `<Name>.csproj` (SDK style).
2. Mirror the existing module's `.csproj`: reference only the `BuildingBlocks/*` it needs
   (MarketData also adds `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to map endpoints).
3. Implement the two-extension pattern (see `AnalyticsModuleExtensions.cs`):
   - `Add<Name>Module(this IServiceCollection)` — register services.
   - `Map<Name>Endpoints(this IEndpointRouteBuilder)` — `app.MapGroup("/api/v1/<name>").WithTags("<Name>")`,
     Minimal API handlers returning `Results.Ok/BadRequest`.
4. Register in **both** hosts' `Program.cs`:
   `builder.Services.Add<Name>Module(...)` and `app.Map<Name>Endpoints();`
5. Requests/responses = immutable `record`s. Put math in a `Calculators/` folder as pure static functions
   (no I/O, no DbContext) so they stay unit-testable.

## Adding a Building Block

1. Create `src/BuildingBlocks/BuildingBlocks.<Name>/` with `<Name>.csproj`.
2. **No domain references.** Building blocks are infra only; modules depend on them, never the reverse.
3. Expose an `Add<Name>(this IServiceCollection, IConfiguration)` extension and call it early in each host's
   `Program.cs` (Observability is always first).
4. Keep interfaces in the building block; hosts/consumers depend on the interface, not the implementation.

## Dependency rules (enforced by structure, not tooling)

- `Modules/*` → `BuildingBlocks/*` **only**. No cross-module references.
- `BuildingBlocks/*` → no `Modules/*`. No domain types.
- `IndexDesk.Api` / `IndexDesk.Worker` → compose modules + building blocks. Hosts may reference both layers.
- Cache, Persistence, Messaging are injected via interfaces; never `new` the Redis/EF/MassTransit clients inline.

## Conventions that must hold

- `Nullable enable` + `ImplicitUsings enable` are global (`Directory.Build.props`). Use nullable annotations; no `#nullable` toggles.
- `EnforceCodeStyleInBuild=true` → `.editorconfig`/analyzer violations fail `dotnet build`. Run
  `dotnet csharpier format .` + `dotnet format analyzers` before pushing (see `../CLAUDE.md` §4).
- Keep `public partial class Program {}` in `IndexDesk.Api/Program.cs` — required by `WebApplicationFactory`.
- Ingestion writes are **idempotent** (upsert/COPY, not insert). CVM zips stream via CsvHelper + CNPJ filter
  - `NpgsqlBinaryImporter` (no full-file buffering).
- External providers are touched **only** from `IndexDesk.Worker` jobs — never from an API request path.
- Secrets stay in `.env` (`Providers__*`, `ConnectionStrings__*`); `appsettings.json` binds config, not secrets.
- Traces: every cross-boundary call (Next.js → Api → Module → Redis/Postgres) should be OpenTelemetry-instrumented
  via `BuildingBlocks.Observability`.
