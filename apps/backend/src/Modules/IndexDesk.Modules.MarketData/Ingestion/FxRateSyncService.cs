using System.Diagnostics;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Persists AwesomeAPI FX closes into <c>fx_rates</c> (bid = close proxy, ask completes
/// the spread). Runs right after the market-data job (22:05 UTC) as its own thin Quartz
/// job so a provider outage never couples with the OHLCV chain.
/// </summary>
public class FxRateSyncService : IFxRateSyncService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly IAwesomeApiClient _awesomeApiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FxRateSyncService> _logger;

    public FxRateSyncService(
        IndexDeskDbContext dbContext,
        IAwesomeApiClient awesomeApiClient,
        IConfiguration configuration,
        ILogger<FxRateSyncService> logger
    )
    {
        _dbContext = dbContext;
        _awesomeApiClient = awesomeApiClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<FxSyncSummary>> SyncLatestAsync(
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var pairs = SyncUniverse.FromList(_configuration[SyncUniverse.AwesomeApiPairsConfigKey]);
        if (pairs.Count == 0)
        {
            pairs = AwesomeApiClient.DefaultPairs.Select(p => p).ToList();
        }

        var result = await _awesomeApiClient.GetLastAsync(pairs, cancellationToken);
        if (result.IsFailure)
        {
            await LogAsync(
                "FAILED",
                pairs.Count,
                0,
                result.Error.Message,
                sw,
                startedAt,
                cancellationToken
            );
            return Result<FxSyncSummary>.Failure(result.Error);
        }

        var persisted = await IngestionUpserts.FxRatesAsync(
            _dbContext,
            result.Value,
            cancellationToken
        );

        var status =
            persisted >= pairs.Count ? "SUCCESS"
            : persisted > 0 ? "PARTIAL_WARNING"
            : "FAILED";
        string? error = persisted == 0 ? "AwesomeAPI returned no usable pair" : null;

        await LogAsync(status, pairs.Count, persisted, error, sw, startedAt, cancellationToken);

        return Result<FxSyncSummary>.Success(
            new FxSyncSummary(
                Status: status,
                PairsRequested: pairs.Count,
                PairsPersisted: result.Value.Select(r => r.Pair).Distinct().Count(),
                RecordsUpserted: persisted,
                ElapsedMilliseconds: sw.ElapsedMilliseconds,
                Error: error
            )
        );
    }

    public async Task<Result<FxSyncSummary>> SyncRangeAsync(
        string pair,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var result = await _awesomeApiClient.GetDailyAsync(
            pair,
            startDate,
            endDate,
            cancellationToken
        );
        if (result.IsFailure)
        {
            await LogAsync("FAILED", 1, 0, result.Error.Message, sw, startedAt, cancellationToken);
            return Result<FxSyncSummary>.Failure(result.Error);
        }

        var persisted = await IngestionUpserts.FxRatesAsync(
            _dbContext,
            result.Value,
            cancellationToken
        );
        var status = persisted > 0 ? "SUCCESS" : "FAILED";
        string? error = persisted == 0 ? $"No daily history for {pair}" : null;

        await LogAsync(status, 1, persisted, error, sw, startedAt, cancellationToken);

        return Result<FxSyncSummary>.Success(
            new FxSyncSummary(
                Status: status,
                PairsRequested: 1,
                PairsPersisted: persisted > 0 ? 1 : 0,
                RecordsUpserted: persisted,
                ElapsedMilliseconds: sw.ElapsedMilliseconds,
                Error: error
            )
        );
    }

    private async Task LogAsync(
        string status,
        int requested,
        int upserted,
        string? error,
        Stopwatch sw,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken
    )
    {
        try
        {
            _dbContext.SyncJobLogs.Add(
                new SyncJobLogEntity
                {
                    JobName = "FxRates_Daily",
                    ProviderName = AwesomeApiClient.ProviderName,
                    Status = status,
                    RecordsProcessed = requested,
                    RecordsUpdated = upserted,
                    RecordsSkipped = 0,
                    ErrorDetails = error,
                    ExecutionTimeMs = (int)sw.ElapsedMilliseconds,
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow,
                }
            );
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Fx] Failed to write sync_job_logs entry.");
        }
    }
}
