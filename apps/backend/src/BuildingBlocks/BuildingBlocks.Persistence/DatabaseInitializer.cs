using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IndexDesk.BuildingBlocks.Persistence;

/// <summary>
/// Single bootstrap point for the database schema. Applies EF Core migrations
/// (<c>MigrateAsync</c>) instead of <c>EnsureCreatedAsync</c>, so the schema evolves
/// through the <c>Migrations/</c> history (FND-013) on both hosts and test factories.
/// Idempotent: calling it multiple times is a no-op once the database is up to date.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations. Safe to call concurrently from Api and Worker against
    /// the same database: EF serializes via the <c>__EFMigrationsHistory</c> table and
    /// idempotent SQL guards (see <c>AddTimescaleAndSeed</c>).
    /// </summary>
    public static async Task MigrateAsync(IndexDeskDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>DI convenience: resolves the context from a scope and applies migrations.</summary>
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var scope = services.CreateAsyncScope();
        await using var scopeRef = scope;
        var dbContext = scopeRef.ServiceProvider.GetRequiredService<IndexDeskDbContext>();
        var logger = scopeRef.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("IndexDesk.DatabaseInitializer");

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger?.LogInformation("Database migrations applied (database is up to date).");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Database migration failed.");
            throw;
        }
    }
}
