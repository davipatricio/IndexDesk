using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Resilience;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

/// <summary>
/// Yahoo Finance quotes/dividends via the Python sidecar (<c>sidecar yf ...</c>).
/// The sidecar inherits yfinance's curl_cffi TLS stack, which survives where plain
/// HttpClient gets blocked; symbol normalization (.SA suffix, benchmarks) happens
/// inside the Python process. Process timeout is enforced by the runner (120 s
/// default); Timeout/FetchFailed get exactly one retry and the per-provider circuit
/// breaker short-circuits when the source keeps failing.
/// </summary>
public class YfinanceSidecarClient : IMarketDataClient
{
    private readonly SidecarProcessRunner _runner;
    private readonly ProviderResilience _resilience;
    private readonly ILogger<YfinanceSidecarClient> _logger;

    public string ProviderName => "YahooSidecar";
    public int Priority => 2; // Yahoo slot in the chain (see plans/provider-sync-scrapers.md)

    public YfinanceSidecarClient(
        SidecarProcessRunner runner,
        ILogger<YfinanceSidecarClient> logger,
        ProviderResilience resilience
    )
    {
        _runner = runner;
        _logger = logger;
        _resilience = resilience;
    }

    public bool SupportsTicker(string ticker)
    {
        // Yahoo serves B3 (via .SA) and global tickers (^BVSP, GC=F, USDBRL=X).
        return !string.IsNullOrWhiteSpace(ticker);
    }

    /// <summary>CLI arguments for a historical-quotes call (exposed for tests).</summary>
    internal static IReadOnlyList<string> QuoteArguments(
        string ticker,
        DateOnly startDate,
        DateOnly endDate
    )
    {
        var symbol = Normalize(ticker);
        return new[]
        {
            "yf",
            "quotes",
            "--symbol",
            symbol,
            "--start",
            startDate.ToString("yyyy-MM-dd"),
            "--end",
            endDate.ToString("yyyy-MM-dd"),
        };
    }

    /// <summary>CLI arguments for a dividends call (exposed for tests).</summary>
    internal static IReadOnlyList<string> DividendArguments(string ticker) =>
        new[] { "yf", "dividends", "--symbol", Normalize(ticker) };

    private static string Normalize(string ticker)
    {
        // Bare B3 tickers are mapped to .SA inside the sidecar as well; normalizing
        // here keeps the log lines and the spawned argv canonical.
        var trimmed = ticker.Trim();
        if (
            trimmed.EndsWith(".SA", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith('^')
            || trimmed.Contains('=')
            || trimmed.Contains('.')
        )
        {
            return trimmed.ToUpperInvariant();
        }

        return $"{trimmed.ToUpperInvariant()}.SA";
    }

    public async Task<Result<IReadOnlyList<NormalizedQuote>>> GetHistoricalQuotesAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation(
                "[YahooSidecar] Fetching historical quotes for {Symbol} ({StartDate} to {EndDate})...",
                Normalize(ticker),
                startDate,
                endDate
            );

            return await _resilience.ExecuteAsync(
                ProviderName,
                $"historical quotes for {ticker}",
                async () =>
                {
                    var run = await _runner
                        .RunAsync(
                            QuoteArguments(ticker, startDate, endDate),
                            cancellationToken: cancellationToken
                        )
                        .ConfigureAwait(false);
                    if (
                        run.SpawnFailed
                        || run.TimedOut
                        || run.ExitCode != SidecarProcessRunner.ExitOk
                    )
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                            _runner.MapFailure(run, ProviderName)
                        );
                    }

                    var mapped = SidecarNdjson.MapQuotes(
                        run.StdoutLines,
                        ticker,
                        startDate,
                        endDate,
                        ProviderName
                    );
                    if (mapped.IsFailure)
                    {
                        return mapped;
                    }

                    if (mapped.Value.Count == 0)
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                            Error.NotFound(
                                "YahooSidecar.NoData",
                                $"No data returned by the Yahoo sidecar for {ticker}"
                            )
                        );
                    }

                    return mapped;
                },
                cancellationToken,
                ProviderMode.Sidecar
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YahooSidecar] Error fetching quotes for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Failure("YahooSidecar.Exception", ex.Message)
            );
        }
    }

    public async Task<Result<IReadOnlyList<NormalizedDividend>>> GetDividendsAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation(
                "[YahooSidecar] Fetching dividends for {Symbol}...",
                Normalize(ticker)
            );

            return await _resilience.ExecuteAsync(
                ProviderName,
                $"dividends for {ticker}",
                async () =>
                {
                    var run = await _runner
                        .RunAsync(DividendArguments(ticker), cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    if (
                        run.SpawnFailed
                        || run.TimedOut
                        || run.ExitCode != SidecarProcessRunner.ExitOk
                    )
                    {
                        return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                            _runner.MapFailure(run, ProviderName)
                        );
                    }

                    // Empty output is a valid dividend series (zero lines), never an error.
                    return SidecarNdjson.MapDividends(run.StdoutLines, ticker, ProviderName);
                },
                cancellationToken,
                ProviderMode.Sidecar
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YahooSidecar] Error fetching dividends for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                Error.Failure("YahooSidecar.Exception", ex.Message)
            );
        }
    }
}
