using System.Diagnostics;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion;

public class MacroEconomicSyncService : IMacroEconomicSyncService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly IBcbSeriesClient _bcbClient;
    private readonly ILogger<MacroEconomicSyncService> _logger;

    public MacroEconomicSyncService(
        IndexDeskDbContext dbContext,
        IBcbSeriesClient bcbClient,
        ILogger<MacroEconomicSyncService> logger
    )
    {
        _dbContext = dbContext;
        _bcbClient = bcbClient;
        _logger = logger;
    }

    public async Task<Result<MacroSyncSummary>> SyncAllAsync(
        DateOnly? startDate = null,
        CancellationToken cancellationToken = default
    )
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var sw = Stopwatch.StartNew();
        var start = startDate ?? new DateOnly(2015, 1, 1);
        var end = DateOnly.FromDateTime(DateTime.UtcNow);

        var series = new (int Code, string Name)[]
        {
            (_bcbClient.CdiSeriesCode, "CDI"),
            (_bcbClient.SelicSeriesCode, "Selic"),
            (_bcbClient.IpcaSeriesCode, "IPCA"),
        };

        var failed = new List<string>();
        var upserted = 0;

        foreach (var (code, name) in series)
        {
            var result = await _bcbClient.GetSeriesAsync(code, start, end, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogError(
                    "[BCB:SyncFailed] Failed to sync series {Name} ({Code}).",
                    name,
                    code
                );
                failed.Add(name);
                continue;
            }

            var count = await UpsertSeriesAsync(code, result.Value, cancellationToken);
            upserted += count;
            _logger.LogInformation(
                "[BCB:Sync] Synced {Count} points for series {Name}.",
                count,
                name
            );
        }

        sw.Stop();
        var summary = new MacroSyncSummary(
            Status: failed.Count == 0 ? "SUCCESS" : "PARTIAL_WARNING",
            SeriesSynced: series.Length - failed.Count,
            RecordsUpserted: upserted,
            ElapsedMilliseconds: sw.ElapsedMilliseconds,
            FailedSeries: failed
        );

        _logger.LogInformation(
            "[BCB:Sync] Macro sync completed ({Series} series, {Records} records).",
            summary.SeriesSynced,
            upserted
        );
        return Result<MacroSyncSummary>.Success(summary);
    }

    private async Task<int> UpsertSeriesAsync(
        int seriesCode,
        IReadOnlyList<BcbSeriesPoint> points,
        CancellationToken cancellationToken
    )
    {
        if (points.Count == 0)
            return 0;

        var dates = points.Select(p => p.Date).ToList();
        var existing = await _dbContext
            .MacroEconomicSeries.Where(s => s.SeriesCode == seriesCode && dates.Contains(s.Date))
            .ToDictionaryAsync(s => s.Date, cancellationToken);

        var toAdd = new List<MacroEconomicSeriesEntity>();
        foreach (var point in points)
        {
            if (existing.ContainsKey(point.Date))
                continue;
            toAdd.Add(
                new MacroEconomicSeriesEntity
                {
                    SeriesCode = seriesCode,
                    Date = point.Date,
                    Value = point.Value,
                }
            );
        }

        if (toAdd.Count > 0)
        {
            _dbContext.MacroEconomicSeries.AddRange(toAdd);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return toAdd.Count;
    }
}
