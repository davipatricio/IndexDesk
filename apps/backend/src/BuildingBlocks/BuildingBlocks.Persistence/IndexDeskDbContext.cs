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
    public DbSet<FxRateEntity> FxRates => Set<FxRateEntity>();
    public DbSet<EtfHoldingEntity> EtfHoldings => Set<EtfHoldingEntity>();
    public DbSet<MarketHolidayEntity> MarketHolidays => Set<MarketHolidayEntity>();

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<PermissionEntity> Permissions => Set<PermissionEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RolePermissionEntity> RolePermissions => Set<RolePermissionEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    public DbSet<PortfolioEntity> Portfolios => Set<PortfolioEntity>();
    public DbSet<PortfolioTransactionEntity> PortfolioTransactions =>
        Set<PortfolioTransactionEntity>();
    public DbSet<PortfolioDailySnapshotEntity> PortfolioDailySnapshots =>
        Set<PortfolioDailySnapshotEntity>();
    public DbSet<PortfolioFixedIncomePositionEntity> PortfolioFixedIncomePositions =>
        Set<PortfolioFixedIncomePositionEntity>();
    public DbSet<PortfolioGoalEntity> PortfolioGoals => Set<PortfolioGoalEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pgcrypto");

        ConfigurePortfolio(modelBuilder);

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

        modelBuilder.Entity<FxRateEntity>(entity =>
        {
            entity.ToTable("fx_rates");
            entity.HasKey(e => new { e.Pair, e.Date });
            entity.Property(e => e.Pair).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Bid).HasPrecision(18, 8);
            entity.Property(e => e.Ask).HasPrecision(18, 8);
            entity.Property(e => e.SourceProvider).HasMaxLength(50);
            entity.HasIndex(e => e.Date);
        });

        modelBuilder.Entity<EtfHoldingEntity>(entity =>
        {
            entity.ToTable("etf_holdings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HoldingTicker).HasMaxLength(20);
            entity.Property(e => e.HoldingName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.WeightPercentage).HasPrecision(6, 4);
            entity.Property(e => e.Sector).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(3);

            entity
                .HasOne(e => e.Asset)
                .WithMany()
                .HasForeignKey(e => e.EtfAssetId)
                .OnDelete(DeleteBehavior.Cascade);

            // Idempotency key for the weekly manager-feed upsert.
            entity
                .HasIndex(e => new
                {
                    e.EtfAssetId,
                    e.AsOfDate,
                    e.HoldingTicker,
                })
                .IsUnique();
            entity.HasIndex(e => new { e.EtfAssetId, e.AsOfDate });
            entity.HasIndex(e => e.HoldingTicker);
        });

        modelBuilder.Entity<MarketHolidayEntity>(entity =>
        {
            entity.ToTable("market_holidays");
            entity.HasKey(e => e.Date);
            entity.Property(e => e.Description).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Exchange).HasMaxLength(10).IsRequired();
            entity.HasIndex(e => e.Exchange);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.EmailVerified).HasDefaultValue(false);
            entity
                .Property(e => e.Preferences)
                .HasColumnType("jsonb")
                .HasDefaultValue("{}")
                .IsRequired();
        });

        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<PermissionEntity>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<UserRoleEntity>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(e => new { e.UserId, e.RoleId });

            entity
                .HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RolePermissionEntity>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(e => new { e.RoleId, e.PermissionId });

            entity
                .HasOne(e => e.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(e => e.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.Property(e => e.ReplacedByTokenHash).HasMaxLength(128);
            entity.Property(e => e.CreatedByIp).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(255);

            entity
                .HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // RBAC is static application configuration; market data is populated only by ingestion jobs.
        SeedRbac(modelBuilder);
    }

    private static void ConfigurePortfolio(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PortfolioEntity>(entity =>
        {
            entity.ToTable("portfolios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.RiskProfile).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Visibility).HasMaxLength(10).IsRequired();
            entity.Property(e => e.PublicValuesMode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DisplayIdentity).HasMaxLength(80);
            entity.Property(e => e.Slug).HasMaxLength(80);
            entity.Property(e => e.ShareTokenHash).HasMaxLength(128);
            entity.Property(e => e.TargetAllocationJson).HasMaxLength(2000);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Slug).IsUnique().HasFilter("\"Slug\" IS NOT NULL");

            entity
                .HasMany(e => e.Transactions)
                .WithOne(t => t.Portfolio)
                .HasForeignKey(t => t.PortfolioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PortfolioTransactionEntity>(entity =>
        {
            entity.ToTable("portfolio_transactions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SyntheticIndexCode).HasMaxLength(10);
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Broker).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Quantity).HasPrecision(20, 8);
            entity.Property(e => e.UnitPrice).HasPrecision(20, 8);
            entity.Property(e => e.GrossAmount).HasPrecision(20, 8);
            entity.Property(e => e.Fees).HasPrecision(20, 8);
            entity.Property(e => e.FxRate).HasPrecision(20, 10);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.CorpActionJson).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasIndex(e => new { e.PortfolioId, e.TradeDate });
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.AmendedTransactionId);
            // Uma única versão vigente por transação original (race de amends concorrentes).
            entity
                .HasIndex(e => e.ReversedByTransactionId)
                .IsUnique()
                .HasFilter("\"ReversedByTransactionId\" IS NOT NULL");
        });

        modelBuilder.Entity<PortfolioDailySnapshotEntity>(entity =>
        {
            entity.ToTable("portfolio_daily_snapshots");
            entity.HasKey(e => new { e.PortfolioId, e.SnapshotDate });
            entity.Property(e => e.TotalValue).HasPrecision(20, 8);
            entity.Property(e => e.InvestedAmount).HasPrecision(20, 8);
            entity.Property(e => e.TwrSinceInception).HasPrecision(14, 8);
        });

        modelBuilder.Entity<PortfolioGoalEntity>(entity =>
        {
            entity.ToTable("portfolio_goals");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).HasMaxLength(20).IsRequired();
            entity.Property(e => e.TargetValue).HasPrecision(20, 8);
            entity.Property(e => e.TargetPct).HasPrecision(10, 4);
            entity.Property(e => e.MonthlyContribution).HasPrecision(20, 8);
            entity.Property(e => e.AssumedAnnualRate).HasPrecision(10, 4);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();

            entity
                .HasOne(e => e.Portfolio)
                .WithMany()
                .HasForeignKey(e => e.PortfolioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PortfolioId, e.Status });
        });

        modelBuilder.Entity<PortfolioFixedIncomePositionEntity>(entity =>
        {
            entity.ToTable("portfolio_fixed_income_positions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SyntheticIndexCode).HasMaxLength(10);
            entity.Property(e => e.Indexer).HasMaxLength(20).IsRequired();
            entity.Property(e => e.IndexerRate).HasPrecision(12, 6);
            entity.Property(e => e.Principal).HasPrecision(20, 8);
            entity.Property(e => e.Liquidity).HasMaxLength(30);
            entity.Property(e => e.TaxRegime).HasMaxLength(20);
            entity.Property(e => e.AccruedValue).HasPrecision(20, 8);

            entity.HasIndex(e => new { e.PortfolioId, e.AssetId });
        });
    }

    private static void SeedRbac(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var adminRoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var proRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var userRoleId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        modelBuilder
            .Entity<RoleEntity>()
            .HasData(
                new RoleEntity
                {
                    Id = adminRoleId,
                    Name = "Admin",
                    Description = "Administrator with full system access",
                    CreatedAt = seedDate,
                },
                new RoleEntity
                {
                    Id = proRoleId,
                    Name = "Pro",
                    Description =
                        "Pro tier subscriber with advanced analytics and unlimited backtests",
                    CreatedAt = seedDate,
                },
                new RoleEntity
                {
                    Id = userRoleId,
                    Name = "User",
                    Description = "Standard registered user",
                    CreatedAt = seedDate,
                }
            );

        var pCatalogRead = Guid.Parse("a0000001-0000-0000-0000-000000000001");
        var pAnalyticsRead = Guid.Parse("a0000001-0000-0000-0000-000000000002");
        var pBacktestUnlimited = Guid.Parse("a0000001-0000-0000-0000-000000000003");
        var pPortfolioRead = Guid.Parse("a0000001-0000-0000-0000-000000000004");
        var pPortfolioWrite = Guid.Parse("a0000001-0000-0000-0000-000000000005");
        var pAdminAccess = Guid.Parse("a0000001-0000-0000-0000-000000000006");
        var pAssetsWrite = Guid.Parse("a0000001-0000-0000-0000-000000000007");
        var pReportsPublish = Guid.Parse("a0000001-0000-0000-0000-000000000008");
        var pUsersManage = Guid.Parse("a0000001-0000-0000-0000-000000000009");

        modelBuilder
            .Entity<PermissionEntity>()
            .HasData(
                new PermissionEntity
                {
                    Id = pCatalogRead,
                    Slug = "catalog:read",
                    Description = "Read asset catalog and quotes",
                    Category = "MarketData",
                },
                new PermissionEntity
                {
                    Id = pAnalyticsRead,
                    Slug = "analytics:read",
                    Description = "Access basic analytics and calculators",
                    Category = "Analytics",
                },
                new PermissionEntity
                {
                    Id = pBacktestUnlimited,
                    Slug = "backtest:unlimited",
                    Description = "Run unlimited portfolio backtests",
                    Category = "Analytics",
                },
                new PermissionEntity
                {
                    Id = pPortfolioRead,
                    Slug = "portfolio:read",
                    Description = "Read user portfolio and positions",
                    Category = "Portfolio",
                },
                new PermissionEntity
                {
                    Id = pPortfolioWrite,
                    Slug = "portfolio:write",
                    Description = "Create and update user portfolio and transactions",
                    Category = "Portfolio",
                },
                new PermissionEntity
                {
                    Id = pAdminAccess,
                    Slug = "admin:access",
                    Description = "Access admin portal and backoffice",
                    Category = "Admin",
                },
                new PermissionEntity
                {
                    Id = pAssetsWrite,
                    Slug = "assets:write",
                    Description = "Curate assets and override metadata",
                    Category = "MarketData",
                },
                new PermissionEntity
                {
                    Id = pReportsPublish,
                    Slug = "reports:publish",
                    Description = "Publish news and manager reports",
                    Category = "Admin",
                },
                new PermissionEntity
                {
                    Id = pUsersManage,
                    Slug = "users:manage",
                    Description = "Manage users and role assignments",
                    Category = "Admin",
                }
            );

        // Admin gets all permissions
        var adminPerms = new[]
        {
            pCatalogRead,
            pAnalyticsRead,
            pBacktestUnlimited,
            pPortfolioRead,
            pPortfolioWrite,
            pAdminAccess,
            pAssetsWrite,
            pReportsPublish,
            pUsersManage,
        };

        // Pro gets catalog, analytics, unlimited backtest, portfolio
        var proPerms = new[]
        {
            pCatalogRead,
            pAnalyticsRead,
            pBacktestUnlimited,
            pPortfolioRead,
            pPortfolioWrite,
        };

        // User gets catalog, analytics, portfolio
        var userPerms = new[] { pCatalogRead, pAnalyticsRead, pPortfolioRead, pPortfolioWrite };

        var rolePermissions = new List<RolePermissionEntity>();

        foreach (var perm in adminPerms)
        {
            rolePermissions.Add(
                new RolePermissionEntity
                {
                    RoleId = adminRoleId,
                    PermissionId = perm,
                    AssignedAt = seedDate,
                }
            );
        }

        foreach (var perm in proPerms)
        {
            rolePermissions.Add(
                new RolePermissionEntity
                {
                    RoleId = proRoleId,
                    PermissionId = perm,
                    AssignedAt = seedDate,
                }
            );
        }

        foreach (var perm in userPerms)
        {
            rolePermissions.Add(
                new RolePermissionEntity
                {
                    RoleId = userRoleId,
                    PermissionId = perm,
                    AssignedAt = seedDate,
                }
            );
        }

        modelBuilder.Entity<RolePermissionEntity>().HasData(rolePermissions);
    }
}
