using IndexDesk.BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IndexDesk.IntegrationTests.Auth;

/// <summary>
/// WebApplicationFactory that isolates integration tests on a dedicated PostgreSQL
/// database (<c>indexdesk_test</c>) and ensures the schema (plus seeded RBAC roles
/// and permissions) exists before the first request. Uses the same local Postgres
/// instance as development via the default connection string, swapping only the
/// database name so tests never touch dev data.
/// </summary>
public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestDatabaseName = "indexdesk_test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(
            (context, config) =>
            {
                var baseConnection =
                    config.Build().GetConnectionString("DefaultConnection")
                    ?? "Host=localhost;Port=5432;Database=indexdesk;Username=indexdesk;Password=indexdesk_dev_secret";

                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = ReplaceDatabase(
                            baseConnection,
                            TestDatabaseName
                        ),
                    }
                );
            }
        );

        builder.ConfigureTestServices(services =>
        {
            // Bind the DbContext to the test database explicitly. The app's own
            // registration is removed to guarantee the test connection wins.
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<IndexDeskDbContext>)
            );
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<IndexDeskDbContext>(
                (sp, options) =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
                }
            );
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IndexDeskDbContext>();
        // Sync-over-async is acceptable here: test bootstrap must complete before any
        // request is served, and the signature is fixed by WebApplicationFactory.
        DatabaseInitializer.MigrateAsync(db).GetAwaiter().GetResult();

        return host;
    }

    private static string ReplaceDatabase(string connectionString, string database)
    {
        // Works for the appsettings default and the .env connection string
        // (both use "Database=indexdesk;").
        var normalized = connectionString.Contains(
            $"Database={TestDatabaseName}",
            StringComparison.OrdinalIgnoreCase
        )
            ? connectionString
            : connectionString.Replace(
                "Database=indexdesk",
                $"Database={database}",
                StringComparison.OrdinalIgnoreCase
            );

        if (normalized.Contains($"Database={database}", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        // Unknown format fallback: parse and rewrite via Npgsql if available.
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = database,
        };
        return builder.ConnectionString;
    }
}
