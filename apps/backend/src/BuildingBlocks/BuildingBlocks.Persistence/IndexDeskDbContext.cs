using Microsoft.EntityFrameworkCore;

namespace IndexDesk.BuildingBlocks.Persistence;

public class IndexDeskDbContext : DbContext
{
    public IndexDeskDbContext(DbContextOptions<IndexDeskDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pgcrypto");
    }
}
