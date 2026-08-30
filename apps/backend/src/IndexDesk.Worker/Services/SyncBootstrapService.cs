using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Services;
using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Worker.Services;

/// <summary>
/// One-shot <see cref="IHostedService"/> that runs at Worker boot and closes any
/// gap left by downtime: Quartz uses the default RAM job store, so a restart while
/// the 22:00 UTC daily close was running (or an outage of N business days) loses
/// the trigger — nothing re-fires it. This bootstrap detects staleness from the
/// data itself (max(<c>Date</c>) in <c>asset_quotes</c> vs the B3 business-day
/// calendar) and runs catch-up day by day, oldest first, via the same
/// <see cref="IDailyCloseSyncService"/> used by the scheduled job.
///
/// Safety properties:
/// - <b>Idempotent</b>: re-running a day only upserts (IngestionUpserts), so a crash
///   mid-catch-up resumes cleanly on next boot from the newest persisted bar.
/// - <b>Single instance</b>: a Postgres advisory lock
///   (<see cref="AdvisoryLockExtensions.BackfillSyncLockId"/>) is held for the whole
///   catch-up; CLI <c>--backfill</c> and API triggers use the same lock, so two
///   writers never race. If the lock is busy, the bootstrap waits
///   <c>Sync:CatchUp:LockTimeoutSeconds</c> then yields (the scheduled job or the
///   other writer wins; next boot retries).
/// - <b>Bounded</b>: at most <c>Sync:CatchUp:MaxBacklogDays</c> business days per
///   boot (default 30), oldest first — enough to survive long outages without
///   hammering Yahoo/TV rate limits.
/// - <b>Never blocks startup</b>: failures are logged, the host keeps booting and
///   Quartz still takes over at 22:00 UTC.
/// </summary>
public sealed class SyncBootstrapService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SyncBootstrapService> logger
) : IHostedService
{
    public const string ConfigEnabled = "Sync:CatchUp:Enabled";
    public const string ConfigMaxBacklogDays = "Sync:CatchUp:MaxBacklogDays";
    public const string ConfigLockTimeoutSeconds = "Sync:CatchUp:LockTimeoutSeconds";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue(ConfigEnabled, defaultValue: true))
        {
            logger.LogInformation(
                "[SyncBootstrap] Disabled via {ConfigKey}. Skipping catch-up check.",
                ConfigEnabled
            );
            return;
        }

        try
        {
            await RunCatchUpAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[SyncBootstrap] Startup cancelled before catch-up finished.");
        }
        catch (Exception ex)
        {
            // Catch-up is best-effort: never take the host down because the
            // bootstrap could not run (DB briefly unavailable, sidecar missing...).
            logger.LogError(
                ex,
                "[SyncBootstrap] Catch-up failed at boot. Scheduled Quartz jobs still run at 22:00 UTC."
            );
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunCatchUpAsync(CancellationToken cancellationToken)
    {
        var maxBacklogDays = Math.Max(1, configuration.GetValue(ConfigMaxBacklogDays, 30));
        var lockTimeoutSeconds = Math.Max(0, configuration.GetValue(ConfigLockTimeoutSeconds, 30));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IndexDeskDbContext>();

        var lastQuoteDate = await dbContext.AssetQuotes.MaxAsync(
            q => (DateOnly?)q.Date,
            cancellationToken
        );
        var latestBusinessDay = await MarketHolidayQueries.GetLatestBusinessDayOnOrBeforeAsync(
            dbContext,
            today,
            cancellationToken
        );

        if (lastQuoteDate is { } last && last >= latestBusinessDay)
        {
            logger.LogInformation(
                "[SyncBootstrap] Up to date (last quote {LastQuoteDate} >= latest business day {LatestBusinessDay}). Nothing to do.",
                last,
                latestBusinessDay
            );
            return;
        }

        // Empty database (fresh install): start from the oldest missing business day
        // inside the backlog window instead of failing on the null aggregate.
        var resumeFrom = lastQuoteDate is { } l ? l.AddDays(1) : today.AddDays(-maxBacklogDays);

        var missingDays = await MarketHolidayQueries.GetBusinessDaysBetweenAsync(
            dbContext,
            resumeFrom,
            latestBusinessDay,
            cancellationToken
        );

        if (missingDays.Count == 0)
        {
            logger.LogInformation(
                "[SyncBootstrap] No missing business day between {ResumeFrom} and {LatestBusinessDay}. Nothing to do.",
                resumeFrom,
                latestBusinessDay
            );
            return;
        }

        if (missingDays.Count > maxBacklogDays)
        {
            logger.LogWarning(
                "[SyncBootstrap] {Count} business days missing but MaxBacklogDays={Max}; catching up only the newest {Max}.",
                missingDays.Count,
                maxBacklogDays,
                maxBacklogDays
            );
            missingDays = missingDays.Skip(missingDays.Count - maxBacklogDays).ToList();
        }

        logger.LogInformation(
            "[SyncBootstrap] {Count} business day(s) pending ({First}..{Last}). Acquiring sync lock (wait up to {Timeout}s)...",
            missingDays.Count,
            missingDays[0],
            missingDays[^1],
            lockTimeoutSeconds
        );

        var lockAcquired = await WaitAndAcquireLockAsync(
            dbContext,
            lockTimeoutSeconds,
            cancellationToken
        );
        if (!lockAcquired || _lockHandle is null)
        {
            logger.LogWarning(
                "[SyncBootstrap] Sync lock busy for {Timeout}s (CLI/API/another instance). Yielding — next boot or the 22:00 UTC job covers the gap.",
                lockTimeoutSeconds
            );
            return;
        }

        try
        {
            var dailyClose = scope.ServiceProvider.GetRequiredService<IDailyCloseSyncService>();
            await CatchUpDaysAsync(dailyClose, missingDays, cancellationToken);
        }
        finally
        {
            await _lockHandle.DisposeAsync();
            _lockHandle = null;
        }
    }

    private AdvisoryLockHandle? _lockHandle;

    private async Task<bool> WaitAndAcquireLockAsync(
        IndexDeskDbContext dbContext,
        int timeoutSeconds,
        CancellationToken cancellationToken
    )
    {
        // pg_try_advisory_lock does not block; poll with small delays up to the
        // configured timeout so the bootstrap yields promptly to CLI/API writers
        // without hard-failing when a short backfill is in flight.
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow <= deadline)
        {
            var handle = await dbContext.TryAcquireAdvisoryLockAsync(
                AdvisoryLockExtensions.BackfillSyncLockId,
                cancellationToken
            );
            if (handle is not null)
            {
                _lockHandle = handle;
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return false;
    }

    private async Task CatchUpDaysAsync(
        IDailyCloseSyncService dailyClose,
        IReadOnlyList<DateOnly> missingDays,
        CancellationToken cancellationToken
    )
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogInformation(
            "[SyncBootstrap] Catch-up starting: {Count} day(s) to run.",
            missingDays.Count
        );

        var succeeded = 0;
        var partiallySucceeded = 0;
        var failed = 0;

        foreach (var day in missingDays)
        {
            try
            {
                // targetDate != today makes DailyCloseSyncService skip the Brapi
                // batch + dividend queue (budget rule) and run Yahoo → TV only.
                var result = await dailyClose.SyncDailyCloseAsync(
                    targetDate: day,
                    cancellationToken: cancellationToken
                );
                if (result.IsSuccess)
                {
                    if (result.Value.Status == "SUCCESS")
                    {
                        succeeded++;
                    }
                    else
                    {
                        partiallySucceeded++;
                    }

                    logger.LogInformation(
                        "[SyncBootstrap] Day {Day}: {Status} in {ElapsedMs}ms ({Universe} tickers).",
                        day,
                        result.Value.Status,
                        result.Value.ElapsedMilliseconds,
                        result.Value.UniverseSize
                    );
                }
                else
                {
                    failed++;
                    logger.LogWarning(
                        "[SyncBootstrap] Day {Day} failed: [{Code}] {Error}",
                        day,
                        result.Error.Code,
                        result.Error.Message
                    );
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(
                    "[SyncBootstrap] Catch-up cancelled at {Day}. Resumes on next boot (idempotent upserts).",
                    day
                );
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(
                    ex,
                    "[SyncBootstrap] Day {Day} threw unexpectedly; continuing with next day.",
                    day
                );
            }
        }

        sw.Stop();
        logger.LogInformation(
            "[SyncBootstrap] Catch-up done in {ElapsedMs}ms: {Succeeded} SUCCESS, {Partial} PARTIAL_WARNING, {Failed} FAILED.",
            sw.ElapsedMilliseconds,
            succeeded,
            partiallySucceeded,
            failed
        );
    }
}
