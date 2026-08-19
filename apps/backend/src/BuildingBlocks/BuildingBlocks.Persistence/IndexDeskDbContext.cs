using IndexDesk.BuildingBlocks.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.BuildingBlocks.Persistence;

public class IndexDeskDbContext : DbContext
{
    public IndexDeskDbContext(DbContextOptions<IndexDeskDbContext> options)
        : base(options) { }

    public DbSet<AssetEntity> Assets => Set<AssetEntity>();
    public DbSet<AssetQuoteEntity> AssetQuotes => Set<AssetQuoteEntity>();
    public DbSet<AssetDividendEntity> AssetDividends => Set<AssetDividendEntity>();
    public DbSet<SyncJobLogEntity> SyncJobLogs => Set<SyncJobLogEntity>();
    public DbSet<MacroEconomicSeriesEntity> MacroEconomicSeries => Set<MacroEconomicSeriesEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<AssetEntity>(entity =>
        {
            entity.ToTable("assets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.Ticker).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.AssetType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Cnpj).HasMaxLength(14);
            entity.Property(e => e.Isin).HasMaxLength(12);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.TradingViewSymbol).HasMaxLength(50);
        });

        modelBuilder.Entity<AssetQuoteEntity>(entity =>
        {
            entity.ToTable("asset_quotes");
            entity.HasKey(e => new { e.AssetId, e.Date });
            entity.Property(e => e.Open).HasPrecision(14, 4);
            entity.Property(e => e.High).HasPrecision(14, 4);
            entity.Property(e => e.Low).HasPrecision(14, 4);
            entity.Property(e => e.Close).HasPrecision(14, 4);
            entity.Property(e => e.AdjClose).HasPrecision(14, 4);
            entity.Property(e => e.Volume).HasPrecision(18, 2);
            entity.Property(e => e.SourceProvider).HasMaxLength(50);

            entity
                .HasOne(e => e.Asset)
                .WithMany(a => a.Quotes)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetDividendEntity>(entity =>
        {
            entity.ToTable("asset_dividends");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Rate).HasPrecision(14, 6);
            entity.Property(e => e.DividendType).HasMaxLength(50);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.SourceProvider).HasMaxLength(50);

            entity
                .HasOne(e => e.Asset)
                .WithMany(a => a.Dividends)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasIndex(e => new
                {
                    e.AssetId,
                    e.ComDate,
                    e.Rate,
                })
                .IsUnique();
        });

        modelBuilder.Entity<SyncJobLogEntity>(entity =>
        {
            entity.ToTable("sync_job_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ProviderName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.StartedAt);
        });

        modelBuilder.Entity<MacroEconomicSeriesEntity>(entity =>
        {
            entity.ToTable("macro_economic_series");
            entity.HasKey(e => new { e.SeriesCode, e.Date });
            entity.Property(e => e.Value).HasPrecision(12, 6);
        });
    }
}
