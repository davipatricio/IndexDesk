using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

/// <summary>
/// InfoMoney (XP Inc) market-data via the Python sidecar (<c>sidecar im ...</c>) —
/// SECONDARY, cross-validation source. The private API sits behind Akamai (blocks
/// non-browser TLS AND stateless Chrome-TLS calls) and Azure APIM (subscription
/// key), so fetches run in the sidecar with curl_cffi chrome impersonation plus a
/// cookie warm-up. Keys from the pool (<c>Providers__InfoMoney__SubscriptionKeys__0..N</c>)
/// are injected through the child environment variable
/// <c>INFOMONEY_SUBSCRIPTION_KEY</c> — never logged, never hardcoded. With NO key
/// configured the client still spawns the child: the sidecar discovers the public
/// frontend key on demand from the InfoMoney quote page itself (measured 26/08/2026).
///
/// Error codes: <c>Scrape.WafBlocked</c> (403 Akamai page, counts toward the breaker),
/// <c>InfoMoney.AuthFailed</c> (401 APIM → pool disables that key index), plus the
/// shared Sidecar.* codes; sidecar Timeout/FetchFailed get exactly one retry.
/// </summary>
public class InfoMoneySidecarClient : IMarketDataClient
{
    public const string SubscriptionKeyEnvVar = "INFOMONEY_SUBSCRIPTION_KEY";

    private const int MinBars = 5;

    private readonly SidecarProcessRunner _runner;
    private readonly IApiKeyPool _apiKeyPool;
    private readonly ProviderResilience _resilience;
    private readonly ILogger<InfoMoneySidecarClient> _logger;

    public string ProviderName => "InfoMoney";
    public int Priority => 4; // secondary validated source, behind Brapi → Yahoo → TV

    public InfoMoneySidecarClient(
        SidecarProcessRunner runner,
        IConfiguration configuration,
        ILogger<InfoMoneySidecarClient> logger,
        IApiKeyPool apiKeyPool,
        ProviderResilience resilience
    )
    {
        _runner = runner;
        _logger = logger;
        _apiKeyPool = apiKeyPool;
        _resilience = resilience;
    }

    public bool SupportsTicker(string ticker)
    {
        // Only bare B3 tickers exist on the InfoMoney API (MGLU3, PETR4...);
        // benchmarks (^BVSP), FX (=X) and dotted symbols have no equivalent there.
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return false;
        }

        var trimmed = ticker.Trim();
        return trimmed.All(char.IsLetterOrDigit);
    }

    /// <summary>
    /// Calendar days inflate to roughly 252 trading days per 365, plus slack; the
    /// daily endpoint paginates arbitrarily but huge pulls stay bounded like TV's.
    /// </summary>
    internal static int EstimateBars(DateOnly startDate, DateOnly endDate)
    {
        var days = Math.Max(1, endDate.DayNumber - startDate.DayNumber + 1);
        var bars = (int)Math.Ceiling(days * 252m / 365m) + MinBars;
        return Math.Clamp(bars, MinBars, 5000);
    }

    /// <summary>CLI arguments for a quotes call (exposed for tests).</summary>
    internal static IReadOnlyList<string> QuoteArguments(
        string ticker,
        DateOnly startDate,
        DateOnly endDate
    ) =>
        new[]
        {
            "im",
            "quotes",
            "--symbol",
            ticker.Trim().ToUpperInvariant(),
            "--bars",
            EstimateBars(startDate, endDate)
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    /// <summary>CLI arguments for a dividends call (exposed for tests).</summary>
    internal static IReadOnlyList<string> DividendArguments(string ticker) =>
        new[] { "im", "dividends", "--symbol", ticker.Trim().ToUpperInvariant() };

    /// <summary>Acquires a key for one attempt: null key when the provider runs keyless
    /// (legacy behavior), or a soft <c>Provider.PoolExhausted</c> failure when keys exist
    /// but all are cooling down / disabled.</summary>
    private (string? Key, Result<T>? Exhausted) AcquireKey<T>()
    {
        if (!_apiKeyPool.HasKeys(ProviderName))
        {
            return (null, null);
        }

        var key = _apiKeyPool.Acquire(ProviderName);
        if (key is not null)
        {
            return (key, null);
        }

        return (
            null,
            Result<T>.Failure(
                Error.Failure(
                    "Provider.PoolExhausted",
                    $"{ProviderName} has no healthy subscription key right now "
                        + "(all cooling down or disabled); falling through to the next source"
                )
            )
        );
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
            return await _resilience.ExecuteAsync<IReadOnlyList<NormalizedQuote>>(
                ProviderName,
                $"daily quotes for {ticker}",
                async () =>
                {
                    var (key, exhausted) = AcquireKey<IReadOnlyList<NormalizedQuote>>();
                    if (exhausted is not null)
                    {
                        return exhausted;
                    }

                    _logger.LogInformation(
                        "[InfoMoney] Fetching daily quotes for {Ticker} ({StartDate} to {EndDate})...",
                        ticker,
                        startDate,
                        endDate
                    );

                    var run = await _runner
                        .RunAsync(
                            QuoteArguments(ticker, startDate, endDate),
                            KeyEnvironment(key),
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    if (
                        run.SpawnFailed
                        || run.TimedOut
                        || run.ExitCode != SidecarProcessRunner.ExitOk
                    )
                    {
                        var error = MapFailure(run);
                        ReportByKey(error.Code, key);
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(error);
                    }

                    ReportSuccess(key);

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
                                "InfoMoney.NoData",
                                $"No data returned by the InfoMoney sidecar for {ticker}"
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
            _logger.LogError(ex, "[InfoMoney] Error fetching quotes for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Failure("InfoMoney.Exception", ex.Message)
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
            return await _resilience.ExecuteAsync<IReadOnlyList<NormalizedDividend>>(
                ProviderName,
                $"cash dividends for {ticker}",
                async () =>
                {
                    var (key, exhausted) = AcquireKey<IReadOnlyList<NormalizedDividend>>();
                    if (exhausted is not null)
                    {
                        return exhausted;
                    }

                    _logger.LogInformation(
                        "[InfoMoney] Fetching cash dividends for {Ticker}...",
                        ticker
                    );

                    var run = await _runner
                        .RunAsync(DividendArguments(ticker), KeyEnvironment(key), cancellationToken)
                        .ConfigureAwait(false);
                    if (
                        run.SpawnFailed
                        || run.TimedOut
                        || run.ExitCode != SidecarProcessRunner.ExitOk
                    )
                    {
                        var error = MapFailure(run);
                        ReportByKey(error.Code, key);
                        return Result<IReadOnlyList<NormalizedDividend>>.Failure(error);
                    }

                    ReportSuccess(key);

                    // Empty output is a valid series (zero lines), never an error.
                    return SidecarNdjson.MapDividends(run.StdoutLines, ticker, ProviderName);
                },
                cancellationToken,
                ProviderMode.Sidecar
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InfoMoney] Error fetching dividends for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                Error.Failure("InfoMoney.Exception", ex.Message)
            );
        }
    }

    /// <summary>Child environment: the pool key when one is configured; empty when
    /// keyless — the sidecar then discovers the public frontend key itself.</summary>
    private static IDictionary<string, string?> KeyEnvironment(string? key) =>
        key is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { [SubscriptionKeyEnvVar] = key };

    /// <summary>Auth rejections disable that key index in the pool (keyless attempts
    /// have no pool entry); everything else leaves the key state untouched.</summary>
    private void ReportByKey(string errorCode, string? key)
    {
        if (key is not null && errorCode.Equals("InfoMoney.AuthFailed", StringComparison.Ordinal))
        {
            _apiKeyPool.Report(ProviderName, key, KeyResult.Invalid);
        }
    }

    private void ReportSuccess(string? key)
    {
        if (key is not null)
        {
            _apiKeyPool.Report(ProviderName, key, KeyResult.Success);
        }
    }

    /// <summary>
    /// Provider-specific mapping first (Scrape.WafBlocked / Scrape.AuthFailed envelopes
    /// emitted by the sidecar), then the shared Sidecar.* taxonomy.
    /// </summary>
    private Error MapFailure(SidecarRunResult run)
    {
        switch (run.StderrErrorCode)
        {
            case "Scrape.WafBlocked":
                return Error.Failure(
                    "Scrape.WafBlocked",
                    $"Akamai blocked the InfoMoney request (TLS fingerprint rejected){Tail(run)}"
                );
            case "Scrape.AuthFailed":
                return Error.Failure(
                    "InfoMoney.AuthFailed",
                    $"InfoMoney rejected the subscription key{Tail(run)}"
                );
            default:
                return _runner.MapFailure(run, ProviderName);
        }
    }

    private static string Tail(SidecarRunResult run)
    {
        var trimmed = run.StderrTail?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.Length <= 300 ? $": {trimmed}" : $": {trimmed[..300]}…";
    }
}
