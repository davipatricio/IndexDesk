using IndexDesk.BuildingBlocks.Cache;
using IndexDesk.BuildingBlocks.Observability;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Services;
using IndexDesk.Modules.MarketData;
using IndexDesk.Modules.MarketData.Ingestion;
using IndexDesk.Modules.Portfolio;
using IndexDesk.Worker.Jobs;
using IndexDesk.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// 1. Observability
builder.Services.AddIndexDeskObservability(builder.Configuration, "IndexDesk.Worker");

// 2. Persistence (PostgreSQL / TimescaleDB)
var postgresConnection =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=indexdesk;Username=indexdesk;Password=indexdesk_dev_secret;";

builder.Services.AddDbContext<IndexDeskDbContext>(options =>
{
    options.UseNpgsql(postgresConnection);
});

// 3. Redis Cache with InMemory fallback
var redisConnection =
    builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
try
{
    var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
    builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();
}
catch
{
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<ICacheService, WorkerInMemoryCacheFallback>();
}

// 4. Modules
builder.Services.AddMarketDataModule(builder.Configuration);
builder.Services.AddPortfolioModule();

// 5. Quartz.NET Schedulers
builder.Services.AddQuartz(q =>
{
    // BCB Macro indicators (CDI, Selic, IPCA) - Scheduled daily at 23:00 UTC
    var bcbJobKey = new JobKey("BcbSyncJob", "MarketDataIngest");
    q.AddJob<BcbSyncJob>(opts => opts.WithIdentity(bcbJobKey));
    q.AddTrigger(opts =>
        opts.ForJob(bcbJobKey)
            .WithIdentity("BcbSyncTrigger", "MarketDataIngest")
            .WithCronSchedule("0 0 23 ? * * *")
    );

    // B3 Market Data Daily Sync - Scheduled Mon-Fri at 22:00 UTC (19:00 BRT).
    // Budget-aware flow: ONE Brapi batch call, then sidecar gap fill (Yahoo -> TV).
    var marketDataJobKey = new JobKey("MarketDataDailySyncJob", "MarketDataIngest");
    q.AddJob<MarketDataDailySyncJob>(opts => opts.WithIdentity(marketDataJobKey));
    q.AddTrigger(opts =>
        opts.ForJob(marketDataJobKey)
            .WithIdentity("MarketDataDailySyncTrigger", "MarketDataIngest")
            .WithCronSchedule("0 0 22 ? * MON-FRI *")
    );

    // FX daily sync (AwesomeAPI: USD/EUR/BTC-BRL) - Mon-Fri at 22:05 UTC,
    // right after the market-data job. Own job = own sync_job_logs + isolated failure.
    var fxRatesJobKey = new JobKey("FxRatesDailySyncJob", "MarketDataIngest");
    q.AddJob<FxRatesDailySyncJob>(opts => opts.WithIdentity(fxRatesJobKey));
    q.AddTrigger(opts =>
        opts.ForJob(fxRatesJobKey)
            .WithIdentity("FxRatesDailySyncTrigger", "MarketDataIngest")
            .WithCronSchedule("0 5 22 ? * MON-FRI *")
    );

    // TradingView OHLCV refresh for uncovered/stale tickers - Mon-Fri at 22:30 UTC.
    var tradingViewJobKey = new JobKey("TradingViewDailySyncJob", "MarketDataIngest");
    q.AddJob<TradingViewDailySyncJob>(opts => opts.WithIdentity(tradingViewJobKey));
    q.AddTrigger(opts =>
        opts.ForJob(tradingViewJobKey)
            .WithIdentity("TradingViewDailySyncTrigger", "MarketDataIngest")
            .WithCronSchedule("0 30 22 ? * MON-FRI *")
    );

    // Weekly holdings from manager feeds (iShares CSV, SPDR XLSX, It Now/Investo HTML)
    // - Saturdays at 08:00 UTC.
    var holdingsJobKey = new JobKey("HoldingsWeeklySyncJob", "MarketDataIngest");
    q.AddJob<HoldingsWeeklySyncJob>(opts => opts.WithIdentity(holdingsJobKey));
    q.AddTrigger(opts =>
        opts.ForJob(holdingsJobKey)
            .WithIdentity("HoldingsWeeklySyncTrigger", "MarketDataIngest")
            .WithCronSchedule("0 0 8 ? * SAT *")
    );

    // Pilot Backfill Job (Manual/On-Demand execution)
    var backfillJobKey = new JobKey("PilotAssetBackfillJob", "Maintenance");
    q.AddJob<PilotAssetBackfillJob>(opts => opts.WithIdentity(backfillJobKey).StoreDurably());

    // Portfolio daily snapshots — Mon-Fri at 23:30 UTC (20:30 BRT), after market/FX syncs.
    var portfolioSnapshotJobKey = new JobKey("PortfolioSnapshotDailyJob", "Portfolio");
    q.AddJob<PortfolioSnapshotDailyJob>(opts => opts.WithIdentity(portfolioSnapshotJobKey));
    q.AddTrigger(opts =>
        opts.ForJob(portfolioSnapshotJobKey)
            .WithIdentity("PortfolioSnapshotDailyTrigger", "Portfolio")
            .WithCronSchedule("0 30 23 ? * MON-FRI *")
    );

    // Fixed-income accrual — Mon-Fri at 23:10 UTC, before the snapshot job.
    var accrualJobKey = new JobKey("PortfolioAccrualDailyJob", "Portfolio");
    q.AddJob<PortfolioAccrualDailyJob>(opts => opts.WithIdentity(accrualJobKey));
    q.AddTrigger(opts =>
        opts.ForJob(accrualJobKey)
            .WithIdentity("PortfolioAccrualDailyTrigger", "Portfolio")
            .WithCronSchedule("0 10 23 ? * MON-FRI *")
    );
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Sync catch-up bootstrap: one-shot IHostedService registered after Quartz so
// the scheduler is configured. Runs at Worker startup (before host.Run blocks),
// closes any gap left by downtime/restarts (RAMJobStore loses missed triggers).
// Idempotent upserts + per-day catch-up loop (oldest first).
builder.Services.AddHostedService<SyncBootstrapService>();

var host = builder.Build();

// CLI Backfill execution handling:
//   dotnet run -- --backfill WRLD11            (fallback chain Brapi → Yahoo → TV)
//   dotnet run -- -b IVVB11 --provider yahoo   (explicit provider: yahoo|tv|infomoney)
//   dotnet run -- -b TICK1,TICK2 --days 365    (rolling window override, else per-ticker start dates)
var backfillIndex = Array.FindIndex(
    args,
    a =>
        a.Equals("--backfill", StringComparison.OrdinalIgnoreCase)
        || a.Equals("-b", StringComparison.OrdinalIgnoreCase)
);

if (backfillIndex >= 0)
{
    var targetArg =
        backfillIndex + 1 < args.Length && !args[backfillIndex + 1].StartsWith('-')
            ? args[backfillIndex + 1]
            : "WRLD11";

    string? providerArg = null;
    var providerIndex = Array.FindIndex(
        args,
        a =>
            a.Equals("--provider", StringComparison.OrdinalIgnoreCase)
            || a.Equals("-p", StringComparison.OrdinalIgnoreCase)
    );
    if (
        providerIndex >= 0
        && providerIndex + 1 < args.Length
        && !args[providerIndex + 1].StartsWith('-')
    )
    {
        providerArg = args[providerIndex + 1];
    }

    // Optional rolling window (--days N): overrides the per-ticker start-date switch,
    // e.g. a 1-year refresh of the whole catalog without re-fetching full history.
    int? daysWindow = null;
    var daysIndex = Array.FindIndex(
        args,
        a => a.Equals("--days", StringComparison.OrdinalIgnoreCase)
    );
    if (
        daysIndex >= 0
        && daysIndex + 1 < args.Length
        && int.TryParse(args[daysIndex + 1], out var parsedDays)
        && parsedDays > 0
    )
    {
        daysWindow = parsedDays;
    }

    using var scope = host.Services.CreateScope();
    var backfillService = scope.ServiceProvider.GetRequiredService<IAssetBackfillService>();

    // Single-writer gate: the CLI shares the same advisory lock as the daily-close
    // job and the boot catch-up (AdvisoryLockExtensions.BackfillSyncLockId), so a
    // manual backfill can never race a scheduled sync into double rate-limit hits.
    var backfillDbContext = scope.ServiceProvider.GetRequiredService<IndexDeskDbContext>();
    var backfillLock = await backfillDbContext.TryAcquireAdvisoryLockAsync(
        AdvisoryLockExtensions.BackfillSyncLockId
    );
    if (backfillLock is null)
    {
        Console.WriteLine(
            "[IndexDesk.Worker:CLI] Another sync holds the advisory lock (daily job / catch-up bootstrap / other CLI). Aborting."
        );
        return;
    }

    await using (backfillLock)
    {
        if (providerArg is not null && !SidecarProviderDirectory.TryNormalize(providerArg, out _))
        {
            Console.WriteLine(
                $"[IndexDesk.Worker:CLI] Unknown provider '{providerArg}'. Supported: {SidecarProviderDirectory.SupportedNames}"
            );
            return;
        }

        Console.WriteLine(
            $"[IndexDesk.Worker:CLI] Running on-demand historical backfill for '{targetArg}'"
                + (providerArg is null ? "" : $" via {providerArg}")
                + (daysWindow is { } window ? $", rolling {window}d window" : "")
                + "..."
        );

        var tickers = targetArg.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        foreach (var ticker in tickers)
        {
            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var startDate = daysWindow is { } days
                ? endDate.AddDays(-days)
                : ticker.ToUpperInvariant() switch
                {
                    "MXRF11" => new DateOnly(2015, 1, 1),
                    "VWRA11" => new DateOnly(2021, 1, 1),
                    "GOLD11" => new DateOnly(2020, 1, 1),
                    "WRLD11" => new DateOnly(2021, 1, 1),
                    // Benchmark indices: full depth for IBOV; IFIX is forward-only (Yahoo
                    // exposes no history) so the window starts around "now".
                    "IBOV" => new DateOnly(2015, 1, 1),
                    "IFIX" => endDate.AddDays(-7),
                    _ => new DateOnly(2021, 1, 1),
                };

            var result = await backfillService.BackfillAssetAsync(
                ticker,
                startDate,
                endDate,
                preferredProvider: providerArg
            );
            if (result.IsSuccess)
            {
                var s = result.Value;
                Console.WriteLine(
                    $"[IndexDesk.Worker:CLI] SUCCESS for {s.Ticker}: {s.QuotesIngested} quotes, {s.DividendsIngested} dividends via {s.SourceProvider} in {s.ElapsedMilliseconds}ms."
                );
            }
            else
            {
                Console.WriteLine(
                    $"[IndexDesk.Worker:CLI] FAILED for {ticker}: {result.Error.Message}"
                );
            }
        }
    }

    return;
}

host.Run();

public sealed class WorkerInMemoryCacheFallback : ICacheService
{
    private readonly Dictionary<string, object> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var val) && val is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default
    )
    {
        if (value is not null)
            _cache[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await GetAsync<T>(key, cancellationToken);
        if (existing is not null)
            return existing;
        var created = await factory(cancellationToken);
        if (created is not null)
            await SetAsync(key, created, expiration, cancellationToken);
        return created;
    }
}
