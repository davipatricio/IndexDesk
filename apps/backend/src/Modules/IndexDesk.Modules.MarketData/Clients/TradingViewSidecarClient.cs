using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

/// <summary>
/// TradingView OHLCV via the Python sidecar (<c>sidecar tv history ...</c>), symbols
/// prefixed with <c>BMFBOVESPA:</c>. The session cookie (optional — anonymous works
/// for B3 data, fewer bars) comes from <c>Providers:TradingView:Cookie</c> and is
/// passed to the child process only; it is never logged. Auth failures surface as
/// <c>TradingView.AuthFailed</c>, distinct from the generic sidecar errors. Process
/// timeout is enforced by the runner (120 s default); Timeout/FetchFailed get exactly
/// one retry and the per-provider breaker short-circuits persistent failures.
/// </summary>
public class TradingViewSidecarClient : IMarketDataClient
{
    private const int MinBars = 5;
    private const int MaxBars = 5000; // tv-scraper hard limit per call

    private readonly SidecarProcessRunner _runner;
    private readonly ProviderResilience _resilience;
    private readonly string? _cookie;
    private readonly ILogger<TradingViewSidecarClient> _logger;

    public string ProviderName => "TradingView";
    public int Priority => 3; // TradingView slot in the chain (Brapi → Yahoo → TV)

    public TradingViewSidecarClient(
        SidecarProcessRunner runner,
        IConfiguration configuration,
        ILogger<TradingViewSidecarClient> logger,
        ProviderResilience resilience
    )
    {
        _runner = runner;
        _logger = logger;
        _cookie = configuration["Providers:TradingView:Cookie"];
        _resilience = resilience;
    }

    public bool SupportsTicker(string ticker)
    {
        // TV serves B3 and global symbols (EXCHANGE:TICKER pairs pass through).
        return !string.IsNullOrWhiteSpace(ticker);
    }

    /// <summary>CLI arguments for a history call (exposed for tests).</summary>
    internal IReadOnlyList<string> HistoryArguments(
        string ticker,
        DateOnly startDate,
        DateOnly endDate
    )
    {
        var args = new List<string>
        {
            "tv",
            "history",
            "--symbol",
            ToTvSymbol(ticker),
            "--interval",
            "1d",
            "--bars",
            EstimateBars(startDate, endDate)
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(_cookie))
        {
            // Secret: forwarded to the child process, never logged.
            args.Add("--cookie");
            args.Add(_cookie);
        }

        return args;
    }

    /// <summary>Bare B3 tickers get the default exchange prefix.</summary>
    internal static string ToTvSymbol(string ticker)
    {
        var trimmed = ticker.Trim().ToUpperInvariant();
        return trimmed.Contains(':') ? trimmed : $"BMFBOVESPA:{trimmed}";
    }

    /// <summary>
    /// Calendar days inflate to roughly 252 trading days per 365, plus slack for
    /// holidays; the result is clamped to what tv-scraper accepts per call.
    /// </summary>
    internal static int EstimateBars(DateOnly startDate, DateOnly endDate)
    {
        var days = Math.Max(1, endDate.DayNumber - startDate.DayNumber + 1);
        var bars = (int)Math.Ceiling(days * 252m / 365m) + MinBars;
        return Math.Clamp(bars, MinBars, MaxBars);
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
                "[TradingView] Fetching {Symbol} candles ({StartDate} to {EndDate})...",
                ToTvSymbol(ticker),
                startDate,
                endDate
            );

            return await _resilience.ExecuteAsync(
                ProviderName,
                $"history for {ticker}",
                async () =>
                {
                    var run = await _runner
                        .RunAsync(
                            HistoryArguments(ticker, startDate, endDate),
                            cancellationToken: cancellationToken
                        )
                        .ConfigureAwait(false);
                    if (
                        run.SpawnFailed
                        || run.TimedOut
                        || run.ExitCode != SidecarProcessRunner.ExitOk
                    )
                    {
                        var error = IsAuthFailure(run)
                            ? Error.Failure(
                                "TradingView.AuthFailed",
                                $"TradingView rejected the session (auth/cookie failure){Describe(run)}"
                            )
                            : _runner.MapFailure(run, ProviderName);
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(error);
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
                                "TradingView.NoData",
                                $"No data returned by the TradingView sidecar for {ticker}"
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
            _logger.LogError(ex, "[TradingView] Error fetching quotes for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Failure("TradingView.Exception", ex.Message)
            );
        }
    }

    public Task<Result<IReadOnlyList<NormalizedDividend>>> GetDividendsAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        // TradingView exposes no corporate-events feed in this integration;
        // proventos come from Brapi/Yahoo/InfoMoney instead.
        _logger.LogDebug(
            "[TradingView] Dividends not supported for {Ticker}; returning empty.",
            ticker
        );
        return Task.FromResult(
            Result<IReadOnlyList<NormalizedDividend>>.Success(Array.Empty<NormalizedDividend>())
        );
    }

    private static bool IsAuthFailure(SidecarRunResult run) =>
        run.ExitCode == SidecarProcessRunner.ExitFetch
        && (
            run.StderrTail?.Contains("authentication", StringComparison.OrdinalIgnoreCase) == true
            || run.StderrTail?.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) == true
            || run.StderrTail?.Contains("cookie", StringComparison.OrdinalIgnoreCase) == true
            || run.StderrTail?.Contains("login", StringComparison.OrdinalIgnoreCase) == true
        );

    private static string Describe(SidecarRunResult run)
    {
        var tail = run.StderrTail?.Trim() ?? string.Empty;
        if (tail.Length == 0)
        {
            return string.Empty;
        }

        return tail.Length <= 300 ? $": {tail}" : $": {tail[..300]}…";
    }
}
