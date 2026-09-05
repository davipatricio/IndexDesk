using System.Diagnostics;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

/// <summary>
/// Orchestrates the manager feeds. Ticker lists and the iShares product-page mapping
/// come from configuration (<c>Providers:Holdings:*</c>); assets must be curated —
/// unknown tickers are skipped with a warning, never fabricated.
/// </summary>
public class EtfHoldingsSyncService : IEtfHoldingsSyncService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly ISharesHoldingsFeed _isharesFeed;
    private readonly SpdrHoldingsFeed _spdrFeed;
    private readonly ItNowHoldingsFeed _itNowFeed;
    private readonly InvestoHoldingsFeed _investoFeed;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EtfHoldingsSyncService> _logger;

    public EtfHoldingsSyncService(
        IndexDeskDbContext dbContext,
        ISharesHoldingsFeed isharesFeed,
        SpdrHoldingsFeed spdrFeed,
        ItNowHoldingsFeed itNowFeed,
        InvestoHoldingsFeed investoFeed,
        IConfiguration configuration,
        ILogger<EtfHoldingsSyncService> logger
    )
    {
        _dbContext = dbContext;
        _isharesFeed = isharesFeed;
        _spdrFeed = spdrFeed;
        _itNowFeed = itNowFeed;
        _investoFeed = investoFeed;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<HoldingsSyncSummary>> SyncWeeklyAsync(
        CancellationToken cancellationToken = default
    )
    {
        var sw = Stopwatch.StartNew();
        await DatabaseInitializer.MigrateAsync(_dbContext, cancellationToken);

        var sources = new List<HoldingsSourceSummary>();

        foreach (var (ticker, page) in ISharesProducts())
        {
            sources.Add(await SyncISharesAsync(ticker, page, cancellationToken));
        }

        foreach (var ticker in Tickers("Providers:Holdings:Spdr:Tickers"))
        {
            sources.Add(await SyncSpdrAsync(ticker, cancellationToken));
        }

        foreach (var ticker in Tickers("Providers:Holdings:ItNow:Tickers"))
        {
            sources.Add(await SyncItNowAsync(ticker, cancellationToken));
        }

        foreach (var ticker in Tickers("Providers:Holdings:Investo:Tickers"))
        {
            sources.Add(await SyncHtmlAsync(ticker, InvestoSource, cancellationToken));
        }

        sw.Stop();
        var succeeded = sources.Count(s => s.Status == "SUCCESS");
        var attempted = sources.Count;

        return Result<HoldingsSyncSummary>.Success(
            new HoldingsSyncSummary(
                Status: attempted == 0 ? "PARTIAL_WARNING"
                    : succeeded == attempted ? "SUCCESS"
                    : succeeded > 0 ? "PARTIAL_WARNING"
                    : "FAILED",
                SourcesAttempted: attempted,
                SourcesSucceeded: succeeded,
                TotalRowsUpserted: sources.Sum(s => s.HoldingsUpserted),
                ElapsedMilliseconds: sw.ElapsedMilliseconds,
                Sources: sources
            )
        );
    }

    // ---- iShares ---------------------------------------------------------------

    private IReadOnlyList<(string Ticker, string Page)> ISharesProducts()
    {
        var section = _configuration.GetSection("Providers:Ishares:Products");
        if (!section.Exists())
        {
            _logger.LogWarning(
                "[Holdings] No Providers:Ishares:Products config; skipping iShares feed."
            );
            return [];
        }

        return section
            .GetChildren()
            .Where(child => !string.IsNullOrWhiteSpace(child.Value))
            .Select(child => (child.Key.ToUpperInvariant(), child.Value!))
            .ToList();
    }

    private async Task<HoldingsSourceSummary> SyncISharesAsync(
        string ticker,
        string productPage,
        CancellationToken cancellationToken
    )
    {
        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        try
        {
            var html = await _isharesFeed.GetProductPageHtmlAsync(
                ticker,
                productPage,
                cancellationToken
            );
            var csvUrl = ISharesHoldingsParser.ExtractAjaxCsvUrl(html, baseUrl: productPage);
            if (csvUrl is null)
            {
                throw new HoldingsLayoutException(
                    $"iShares product page for {ticker} exposes no ajax CSV link anymore."
                );
            }

            var csv = await _isharesFeed.DownloadCsvAsync(csvUrl, cancellationToken);
            var holdings = ISharesHoldingsParser.ParseCsv(csv);

            var upserted = await PersistAsync(ticker, holdings, cancellationToken);
            await LogAsync(
                ticker,
                "ISHARES_FEED",
                "SUCCESS",
                upserted,
                sw,
                startedAt,
                null,
                cancellationToken
            );
            return new HoldingsSourceSummary(
                "ISHARES_FEED",
                ticker,
                "SUCCESS",
                upserted,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Holdings] iShares feed failed for {Ticker}.", ticker);
            await LogAsync(
                ticker,
                "ISHARES_FEED",
                "FAILED",
                0,
                sw,
                startedAt,
                ex.Message,
                cancellationToken
            );
            return new HoldingsSourceSummary(
                "ISHARES_FEED",
                ticker,
                "FAILED",
                0,
                DateOnly.FromDateTime(DateTime.UtcNow),
                ex.Message
            );
        }
    }

    // ---- SPDR ------------------------------------------------------------------

    private IReadOnlyList<string> Tickers(string sectionKey) =>
        SyncUniverse.FromList(_configuration[sectionKey]);

    private async Task<HoldingsSourceSummary> SyncSpdrAsync(
        string ticker,
        CancellationToken cancellationToken
    )
    {
        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        try
        {
            await using var xlsx = await _spdrFeed.DownloadXlsxAsync(ticker, cancellationToken);
            var holdings = SpdrHoldingsParser.ParseXlsx(xlsx);

            var upserted = await PersistAsync(ticker, holdings, cancellationToken);
            await LogAsync(
                ticker,
                "SPDR_FEED",
                "SUCCESS",
                upserted,
                sw,
                startedAt,
                null,
                cancellationToken
            );
            return new HoldingsSourceSummary(
                "SPDR_FEED",
                ticker,
                "SUCCESS",
                upserted,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Holdings] SPDR feed failed for {Ticker}.", ticker);
            await LogAsync(
                ticker,
                "SPDR_FEED",
                "FAILED",
                0,
                sw,
                startedAt,
                ex.Message,
                cancellationToken
            );
            return new HoldingsSourceSummary(
                "SPDR_FEED",
                ticker,
                "FAILED",
                0,
                DateOnly.FromDateTime(DateTime.UtcNow),
                ex.Message
            );
        }
    }

    // ---- HTML compositions (It Now / Investo) ----------------------------------

    private static readonly string ItNowJsonSource = "ITNOW_JSON";
    private static readonly string ItNowSource = "ITNOW_HTML";
    private static readonly string InvestoSource = "INVESTO_HTML";

    /// <summary>
    /// It Now: structured JSON API first (the composition page embeds its own fund code
    /// as <c>fundo={ISIN}</c> in its fetch scripts; config map
    /// <c>Providers:Holdings:ItNow:FundCodes:{TICKER}</c> is the fallback), AngleSharp HTML
    /// table parser as last resort. A JSON failure degrades to the HTML path instead of
    /// failing the ticker.
    /// </summary>
    private async Task<HoldingsSourceSummary> SyncItNowAsync(
        string ticker,
        CancellationToken cancellationToken
    )
    {
        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        try
        {
            // Single page fetch doubles as fund-code discovery and HTML fallback input.
            var html = await _itNowFeed.GetCompositionHtmlAsync(ticker, cancellationToken);

            IReadOnlyList<ParsedHolding> holdings;
            string? jsonPayload = null;
            var sourceName = ItNowJsonSource;
            try
            {
                var fundCode =
                    ItNowJsonParser.TryExtractFundCode(html)
                    ?? _configuration[
                        $"Providers:Holdings:ItNow:FundCodes:{ticker.ToUpperInvariant()}"
                    ];
                if (string.IsNullOrWhiteSpace(fundCode))
                {
                    throw new HoldingsLayoutException(
                        $"It Now composition page exposes no fund code and none configured "
                            + $"for {ticker} (Providers:Holdings:ItNow:FundCodes)."
                    );
                }

                var json = await _itNowFeed.PostCompositionJsonAsync(
                    ticker,
                    fundCode,
                    cancellationToken
                );
                holdings = ItNowJsonParser.ParseJson(json);
                jsonPayload = json;
            }
            catch (Exception jsonEx) when (jsonEx is not HoldingsLayoutException)
            {
                _logger.LogWarning(
                    jsonEx,
                    "[Holdings] It Now JSON API failed for {Ticker}; falling back to the HTML table.",
                    ticker
                );
                sourceName = ItNowSource;
                holdings = HtmlCompositionParser.Parse(html);
            }

            var upserted = await PersistAsync(ticker, holdings, cancellationToken);
            var asOf =
                ItNowJsonParser.TryGetAsOfDate(jsonPayload)
                ?? DateOnly.FromDateTime(DateTime.UtcNow);
            await LogAsync(
                ticker,
                sourceName,
                "SUCCESS",
                upserted,
                sw,
                startedAt,
                null,
                cancellationToken
            );
            return new HoldingsSourceSummary(sourceName, ticker, "SUCCESS", upserted, asOf, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Holdings] {Source} failed for {Ticker}.", ItNowSource, ticker);
            await LogAsync(
                ticker,
                ItNowSource,
                "FAILED",
                0,
                sw,
                startedAt,
                ex.Message,
                cancellationToken
            );
            return new HoldingsSourceSummary(
                ItNowSource,
                ticker,
                "FAILED",
                0,
                DateOnly.FromDateTime(DateTime.UtcNow),
                ex.Message
            );
        }
    }

    private async Task<HoldingsSourceSummary> SyncHtmlAsync(
        string ticker,
        string sourceName,
        CancellationToken cancellationToken
    )
    {
        var startedAt = DateTimeOffset.UtcNow;
        var sw = Stopwatch.StartNew();
        try
        {
            var html =
                sourceName == ItNowSource
                    ? await _itNowFeed.GetCompositionHtmlAsync(ticker, cancellationToken)
                    : await _investoFeed.GetEtfHtmlAsync(ticker, cancellationToken);

            var holdings = HtmlCompositionParser.Parse(html);
            if (holdings.Count == 0)
            {
                throw new HoldingsLayoutException(
                    $"{sourceName}: composition layout changed or no rows parsed for {ticker} "
                        + "(Scrape.SelectorChanged)"
                );
            }

            var upserted = await PersistAsync(ticker, holdings, cancellationToken);
            await LogAsync(
                ticker,
                sourceName,
                "SUCCESS",
                upserted,
                sw,
                startedAt,
                null,
                cancellationToken
            );
            return new HoldingsSourceSummary(
                sourceName,
                ticker,
                "SUCCESS",
                upserted,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Holdings] {Source} failed for {Ticker}.", sourceName, ticker);
            await LogAsync(
                ticker,
                sourceName,
                "FAILED",
                0,
                sw,
                startedAt,
                ex.Message,
                cancellationToken
            );
            return new HoldingsSourceSummary(
                sourceName,
                ticker,
                "FAILED",
                0,
                DateOnly.FromDateTime(DateTime.UtcNow),
                ex.Message
            );
        }
    }

    // ---- persistence + audit ---------------------------------------------------

    /// <summary>Resolves the curated asset (skip unknown with warning) and upserts rows.</summary>
    private async Task<int> PersistAsync(
        string ticker,
        IReadOnlyList<ParsedHolding> holdings,
        CancellationToken cancellationToken
    )
    {
        if (holdings.Count == 0)
        {
            return 0;
        }

        var assetId = await _dbContext
            .Assets.Where(a => a.Ticker == ticker.Trim().ToUpperInvariant())
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (assetId is null)
        {
            _logger.LogWarning(
                "[Holdings] Skipped {Ticker}: no curated asset record (curate first).",
                ticker
            );
            return 0;
        }

        var asOfDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = holdings
            .Select(h => new EtfHoldingRow(
                EtfAssetId: assetId.Value,
                HoldingTicker: h.Ticker,
                HoldingName: string.IsNullOrWhiteSpace(h.Name) ? h.Ticker ?? "Unknown" : h.Name,
                WeightPercentage: h.WeightPercentage,
                Sector: string.IsNullOrWhiteSpace(h.Sector) ? null : h.Sector,
                Country: string.IsNullOrWhiteSpace(h.Country)
                    ? null
                    : h.Country[..3].ToUpperInvariant(),
                AsOfDate: asOfDate
            ))
            .ToList();

        return await IngestionUpserts.EtfHoldingsAsync(_dbContext, rows, cancellationToken);
    }

    private async Task LogAsync(
        string ticker,
        string providerName,
        string status,
        int upserted,
        Stopwatch sw,
        DateTimeOffset startedAt,
        string? error,
        CancellationToken cancellationToken
    )
    {
        try
        {
            _dbContext.SyncJobLogs.Add(
                new SyncJobLogEntity
                {
                    JobName = $"Holdings_{ticker}",
                    ProviderName = providerName,
                    Status = status,
                    RecordsProcessed = upserted,
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
            _logger.LogWarning(ex, "[Holdings] Failed to write sync_job_logs entry.");
        }
    }
}
