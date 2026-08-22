using System.Diagnostics;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion;

public class AssetBackfillService : IAssetBackfillService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly IFallbackMarketDataService _fallbackService;
    private readonly ILogger<AssetBackfillService> _logger;

    public AssetBackfillService(
        IndexDeskDbContext dbContext,
        IFallbackMarketDataService fallbackService,
        ILogger<AssetBackfillService> logger
    )
    {
        _dbContext = dbContext;
        _fallbackService = fallbackService;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<BackfillExecutionSummary>>> BackfillPilotAssetsAsync(
        DateOnly? defaultStartDate = null,
        CancellationToken cancellationToken = default
    )
    {
        var pilotTickers = new[] { "MXRF11", "VWRA11", "GOLD11" };
        var summaries = new List<BackfillExecutionSummary>(pilotTickers.Length);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var ticker in pilotTickers)
        {
            // Tailored start dates per asset
            var startDate =
                defaultStartDate
                ?? ticker switch
                {
                    "MXRF11" => new DateOnly(2015, 1, 1),
                    "VWRA11" => new DateOnly(2021, 1, 1),
                    "GOLD11" => new DateOnly(2020, 1, 1),
                    _ => new DateOnly(2020, 1, 1),
                };

            var result = await BackfillAssetAsync(ticker, startDate, endDate, cancellationToken);
            if (result.IsSuccess)
            {
                summaries.Add(result.Value);
            }
            else
            {
                summaries.Add(
                    new BackfillExecutionSummary(
                        Ticker: ticker,
                        Status: "FAILED",
                        QuotesIngested: 0,
                        DividendsIngested: 0,
                        SourceProvider: "NONE",
                        ElapsedMilliseconds: 0,
                        ErrorMessage: result.Error.Message
                    )
                );
            }

            // Throttle between requests to avoid rate limits
            await Task.Delay(300, cancellationToken);
        }

        return Result<IReadOnlyList<BackfillExecutionSummary>>.Success(summaries);
    }

    public async Task<Result<BackfillExecutionSummary>> BackfillAssetAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    )
    {
        ticker = ticker.ToUpperInvariant();
        var sw = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "[Backfill:Start] Starting historical backfill for {Ticker} ({StartDate} to {EndDate})...",
            ticker,
            startDate,
            endDate
        );

        try
        {
            // 1. Ensure known asset metadata exists in the database. Unknown tickers
            // cannot be persisted with fabricated metadata; curate them first.
            var assetResult = await EnsureAssetExistsAsync(ticker, cancellationToken);
            if (assetResult.IsFailure)
            {
                return Result<BackfillExecutionSummary>.Failure(assetResult.Error);
            }

            var asset = assetResult.Value;

            // 2. Fetch Quotes with Multi-Provider Fallback
            var quotesResult = await _fallbackService.GetQuotesWithFallbackAsync(
                ticker,
                startDate,
                endDate,
                cancellationToken
            );
            if (quotesResult.IsFailure)
            {
                await LogJobExecutionAsync(
                    jobName: $"Backfill_{ticker}",
                    provider: "ALL",
                    status: "FAILED",
                    processed: 0,
                    updated: 0,
                    error: quotesResult.Error.Message,
                    timeMs: (int)sw.ElapsedMilliseconds,
                    startedAt: startedAt,
                    cancellationToken: cancellationToken
                );

                return Result<BackfillExecutionSummary>.Failure(quotesResult.Error);
            }

            var quotes = quotesResult.Value;
            var providerName = quotes.FirstOrDefault().SourceProvider ?? "UNKNOWN";

            // 3. Upsert Quotes into PostgreSQL/TimescaleDB
            var quotesCount = await UpsertQuotesAsync(asset.Id, quotes, cancellationToken);

            // 4. Fetch and Upsert Dividends (especially relevant for MXRF11)
            var dividendsCount = 0;
            var dividendsResult = await _fallbackService.GetDividendsWithFallbackAsync(
                ticker,
                cancellationToken
            );
            if (dividendsResult.IsSuccess && dividendsResult.Value.Count > 0)
            {
                dividendsCount = await UpsertDividendsAsync(
                    asset.Id,
                    dividendsResult.Value,
                    cancellationToken
                );
            }

            sw.Stop();
            var timeMs = (int)sw.ElapsedMilliseconds;

            await LogJobExecutionAsync(
                jobName: $"Backfill_{ticker}",
                provider: providerName,
                status: "SUCCESS",
                processed: quotes.Count,
                updated: quotesCount,
                error: null,
                timeMs: timeMs,
                startedAt: startedAt,
                cancellationToken: cancellationToken
            );

            _logger.LogInformation(
                "[Backfill:Completed] Finished backfill for {Ticker} in {ElapsedMs}ms ({Quotes} quotes, {Dividends} dividends, Provider: {Provider}).",
                ticker,
                timeMs,
                quotesCount,
                dividendsCount,
                providerName
            );

            return Result<BackfillExecutionSummary>.Success(
                new BackfillExecutionSummary(
                    Ticker: ticker,
                    Status: "SUCCESS",
                    QuotesIngested: quotesCount,
                    DividendsIngested: dividendsCount,
                    SourceProvider: providerName,
                    ElapsedMilliseconds: timeMs,
                    ErrorMessage: null
                )
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(
                ex,
                "[Backfill:Exception] Unhandled error backfilling {Ticker}.",
                ticker
            );

            await LogJobExecutionAsync(
                jobName: $"Backfill_{ticker}",
                provider: "UNKNOWN",
                status: "FAILED",
                processed: 0,
                updated: 0,
                error: ex.Message,
                timeMs: (int)sw.ElapsedMilliseconds,
                startedAt: startedAt,
                cancellationToken: cancellationToken
            );

            return Result<BackfillExecutionSummary>.Failure(
                Error.Failure("Backfill.Exception", ex.Message)
            );
        }
    }

    private async Task<Result<AssetEntity>> EnsureAssetExistsAsync(
        string ticker,
        CancellationToken cancellationToken
    )
    {
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var existing = await _dbContext.Assets.FirstOrDefaultAsync(
            a => a.Ticker == ticker,
            cancellationToken
        );
        if (existing is not null)
        {
            return Result<AssetEntity>.Success(existing);
        }

        // Ingestion must never create an asset with guessed name, type, CNPJ, or
        // other metadata. A curator/importer must create the catalog record first.
        return Result<AssetEntity>.Failure(Error.NotFound("Asset.Metadata", ticker));
    }

    private async Task<int> UpsertQuotesAsync(
        Guid assetId,
        IReadOnlyList<NormalizedQuote> quotes,
        CancellationToken cancellationToken
    )
    {
        if (quotes.Count == 0)
            return 0;

        var dates = quotes.Select(q => q.Date).ToList();
        var existingQuotes = await _dbContext
            .AssetQuotes.Where(q => q.AssetId == assetId && dates.Contains(q.Date))
            .ToDictionaryAsync(q => q.Date, cancellationToken);

        var toAdd = new List<AssetQuoteEntity>();

        foreach (var quote in quotes)
        {
            if (existingQuotes.TryGetValue(quote.Date, out var existing))
            {
                existing.Open = quote.Open;
                existing.High = quote.High;
                existing.Low = quote.Low;
                existing.Close = quote.Close;
                existing.AdjClose = quote.AdjClose;
                existing.Volume = quote.Volume;
                existing.SourceProvider = quote.SourceProvider;
            }
            else
            {
                toAdd.Add(
                    new AssetQuoteEntity
                    {
                        AssetId = assetId,
                        Date = quote.Date,
                        Open = quote.Open,
                        High = quote.High,
                        Low = quote.Low,
                        Close = quote.Close,
                        AdjClose = quote.AdjClose,
                        Volume = quote.Volume,
                        TradesCount = quote.TradesCount,
                        SourceProvider = quote.SourceProvider,
                    }
                );
            }
        }

        if (toAdd.Count > 0)
        {
            await _dbContext.AssetQuotes.AddRangeAsync(toAdd, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return quotes.Count;
    }

    private async Task<int> UpsertDividendsAsync(
        Guid assetId,
        IReadOnlyList<NormalizedDividend> dividends,
        CancellationToken cancellationToken
    )
    {
        if (dividends.Count == 0)
            return 0;

        var existingDividends = await _dbContext
            .AssetDividends.Where(d => d.AssetId == assetId)
            .ToListAsync(cancellationToken);

        var existingSet = existingDividends.ToHashSet();
        var toAdd = new List<AssetDividendEntity>();

        foreach (var div in dividends)
        {
            var alreadyExists = existingDividends.Any(e =>
                e.ComDate == div.ComDate && e.Rate == div.Rate
            );
            if (!alreadyExists)
            {
                toAdd.Add(
                    new AssetDividendEntity
                    {
                        AssetId = assetId,
                        ComDate = div.ComDate,
                        PaymentDate = div.PaymentDate,
                        Rate = div.Rate,
                        DividendType = div.DividendType,
                        Currency = div.Currency,
                        SourceProvider = div.SourceProvider,
                    }
                );
            }
        }

        if (toAdd.Count > 0)
        {
            await _dbContext.AssetDividends.AddRangeAsync(toAdd, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return toAdd.Count;
    }

    private async Task LogJobExecutionAsync(
        string jobName,
        string provider,
        string status,
        int processed,
        int updated,
        string? error,
        int timeMs,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var log = new SyncJobLogEntity
            {
                JobName = jobName,
                ProviderName = provider,
                Status = status,
                RecordsProcessed = processed,
                RecordsUpdated = updated,
                RecordsSkipped = 0,
                ErrorDetails = error,
                ExecutionTimeMs = timeMs,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow,
            };

            _dbContext.SyncJobLogs.Add(log);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[Backfill:LogFailed] Failed to write entry into sync_job_logs."
            );
        }
    }
}
