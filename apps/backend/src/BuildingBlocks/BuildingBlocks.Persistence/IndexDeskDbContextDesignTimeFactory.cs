using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IndexDesk.BuildingBlocks.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the <see cref="IndexDeskDbContext"/>
/// from this Persistence project without booting the Api/Worker host (FND-013).
/// Reads <c>IDX_DESIGNTIME_CONNECTION</c> (env var) or falls back to the local dev
/// connection string. Never used at runtime — runtime always goes through the host DI.
/// </summary>
public sealed class IndexDeskDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IndexDeskDbContext>
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=indexdesk;Username=indexdesk;Password=indexdesk_dev_secret;";

    public IndexDeskDbContext CreateDbContext(string[] args)
    {
        var connection = System.Environment.GetEnvironmentVariable("IDX_DESIGNTIME_CONNECTION")
            ?? DefaultConnection;

        var builder = new DbContextOptionsBuilder<IndexDeskDbContext>();
        builder.UseNpgsql(connection);

        return new IndexDeskDbContext(builder.Options);
    }
}
