using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Resilience;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Ingestion;
using IndexDesk.Modules.MarketData.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

public class BrapiClient : IMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly IApiKeyPool _apiKeyPool;
    private readonly ProviderResilience _resilience;
    private readonly ILogger<BrapiClient> _logger;

    public string ProviderName => "Brapi";
    public int Priority => 1;

    public BrapiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BrapiClient> logger,
        IApiKeyPool apiKeyPool,
        ProviderResilience resilience
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKeyPool = apiKeyPool;
        _resilience = resilience;
    }

    public bool SupportsTicker(string ticker)
    {
        // Brapi supports all B3 tickers (ETFs, BDRs, FIIs, Stocks)
        return !string.IsNullOrWhiteSpace(ticker)
            && !ticker.StartsWith('^')
            && !ticker.Contains('=')
            && !BenchmarkCatalog.IsBenchmark(ticker); // indices are served by Yahoo only
    }

    /// <summary>
    /// The single daily batch call allowed by the free-plan budget
    /// (<c>/quote/list</c> — plans/provider-sync-scrapers.md "Orçamento Brapi"):
    /// one request for the whole B3 universe after close, never bulk history.
    /// Anonymous responses may ignore the <c>tickers</c> filter (Fase 0 finding),
    /// so results are filtered client-side to the requested symbols.
    /// </summary>
    public async Task<Result<IReadOnlyList<NormalizedQuote>>> GetDailyBatchQuotesAsync(
        IReadOnlyList<string> tickers,
        DateOnly tradeDate,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await _resilience.ExecuteAsync(
                ProviderName,
                "daily batch quotes",
                async () =>
                {
                    var requested = tickers
                        .Where(SupportsTicker)
                        .Select(t => t.Trim().ToUpperInvariant())
                        .Distinct()
                        .ToList();
                    if (requested.Count == 0)
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Success(
                            Array.Empty<NormalizedQuote>()
                        );
                    }

                    var token = AcquireToken();
                    if (token is null && _apiKeyPool.HasKeys(ProviderName))
                    {
                        return PoolExhausted<IReadOnlyList<NormalizedQuote>>();
                    }

                    var url = $"quote/list?tickers={string.Join(',', requested)}";
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        // Secret: appended to the request only, never logged.
                        url += "&token=" + Uri.EscapeDataString(token);
                    }

                    _logger.LogInformation(
                        "[Brapi] Batch daily quotes for {Count} tickers in one call...",
                        requested.Count
                    );

                    var response = await SendAsync(url, cancellationToken);
                    var guarded = GuardResponse(token, response, "batch call");
                    if (guarded is not null)
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(guarded);
                    }

                    ReportToken(token, KeyResult.Success);

                    var content = await response.Content.ReadFromJsonAsync<BrapiQuoteResponse>(
                        cancellationToken: cancellationToken
                    );
                    var results = content?.Results;
                    if (results == null || results.Count == 0)
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                            Error.NotFound(
                                "Brapi.NoData",
                                "No data returned by the Brapi batch call"
                            )
                        );
                    }

                    var requestedSet = requested.ToHashSet();
                    var quotes = new List<NormalizedQuote>(results.Count);
                    foreach (
                        var item in results.Where(r =>
                            r.Symbol != null && requestedSet.Contains(r.Symbol.ToUpperInvariant())
                        )
                    )
                    {
                        var symbol = item.Symbol!.ToUpperInvariant();
                        var close = item.RegularMarketPrice;
                        if (close <= 0)
                        {
                            continue; // no usable daily close for this symbol yet
                        }

                        quotes.Add(
                            new NormalizedQuote(
                                Ticker: symbol,
                                Date: tradeDate,
                                Open: item.RegularMarketOpen > 0 ? item.RegularMarketOpen : close,
                                High: Math.Max(
                                    item.RegularMarketDayHigh > 0
                                        ? item.RegularMarketDayHigh
                                        : close,
                                    close
                                ),
                                Low: item.RegularMarketDayLow > 0
                                    ? item.RegularMarketDayLow
                                    : close,
                                Close: close,
                                AdjClose: close, // intraday snapshot carries no adjustment factors
                                Volume: item.RegularMarketVolume ?? 0,
                                TradesCount: null,
                                SourceProvider: ProviderName,
                                FetchedAtUtc: DateTimeOffset.UtcNow
                            )
                        );
                    }

                    quotes.Sort((a, b) => a.Date.CompareTo(b.Date));
                    return Result<IReadOnlyList<NormalizedQuote>>.Success(quotes);
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Brapi] Error on batch daily quotes.");
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Failure("Brapi.Exception", ex.Message)
            );
        }
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
            return await _resilience.ExecuteAsync(
                ProviderName,
                $"historical quotes for {ticker}",
                async () =>
                {
                    var range = CalculateRange(startDate, endDate);
                    var token = AcquireToken();
                    if (token is null && _apiKeyPool.HasKeys(ProviderName))
                    {
                        return PoolExhausted<IReadOnlyList<NormalizedQuote>>();
                    }

                    var url = $"quote/{ticker}?range={range}&interval=1d";
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        // Secret: appended to the request only, never logged.
                        url += "&token=" + Uri.EscapeDataString(token);
                    }

                    _logger.LogInformation(
                        "[Brapi] Fetching historical quotes for {Ticker} with range {Range}...",
                        ticker,
                        range
                    );

                    var response = await SendAsync(url, cancellationToken);
                    var guarded = GuardResponse(token, response, $"{ticker}");
                    if (guarded is not null)
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(guarded);
                    }

                    ReportToken(token, KeyResult.Success);

                    var content = await response.Content.ReadFromJsonAsync<BrapiQuoteResponse>(
                        cancellationToken: cancellationToken
                    );
                    if (content?.Results == null || content.Results.Count == 0)
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                            Error.NotFound(
                                "Brapi.NoData",
                                $"No data returned by Brapi for {ticker}"
                            )
                        );
                    }

                    var assetResult = content.Results[0];
                    var historical = assetResult.HistoricalDataPrice;
                    if (historical == null || historical.Count == 0)
                    {
                        return Result<IReadOnlyList<NormalizedQuote>>.Success(
                            Array.Empty<NormalizedQuote>()
                        );
                    }

                    var quotes = new List<NormalizedQuote>(historical.Count);
                    foreach (var item in historical)
                    {
                        var date = DateOnly.FromDateTime(
                            DateTimeOffset.FromUnixTimeSeconds(item.Date).UtcDateTime
                        );
                        if (date < startDate || date > endDate)
                        {
                            continue;
                        }

                        quotes.Add(
                            new NormalizedQuote(
                                Ticker: ticker.ToUpperInvariant(),
                                Date: date,
                                Open: item.Open,
                                High: item.High,
                                Low: item.Low,
                                Close: item.Close,
                                AdjClose: item.AdjustedClose > 0 ? item.AdjustedClose : item.Close,
                                Volume: item.Volume,
                                TradesCount: null,
                                SourceProvider: ProviderName,
                                FetchedAtUtc: DateTimeOffset.UtcNow
                            )
                        );
                    }

                    quotes.Sort((a, b) => a.Date.CompareTo(b.Date));
                    return Result<IReadOnlyList<NormalizedQuote>>.Success(quotes);
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Brapi] Error fetching quotes for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Failure("Brapi.Exception", ex.Message)
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
            return await _resilience.ExecuteAsync(
                ProviderName,
                $"dividends for {ticker}",
                async () =>
                {
                    var token = AcquireToken();
                    if (token is null && _apiKeyPool.HasKeys(ProviderName))
                    {
                        return PoolExhausted<IReadOnlyList<NormalizedDividend>>();
                    }

                    var url = $"quote/{ticker}?dividends=true";
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        // Secret: appended to the request only, never logged.
                        url += "&token=" + Uri.EscapeDataString(token);
                    }

                    var response = await SendAsync(url, cancellationToken);
                    var guarded = GuardResponse(token, response, $"{ticker} dividends");
                    if (guarded is not null)
                    {
                        return Result<IReadOnlyList<NormalizedDividend>>.Failure(guarded);
                    }

                    ReportToken(token, KeyResult.Success);

                    var content = await response.Content.ReadFromJsonAsync<BrapiQuoteResponse>(
                        cancellationToken: cancellationToken
                    );
                    var dividendsList = new List<NormalizedDividend>();

                    var cashDividends = content
                        ?.Results?.FirstOrDefault()
                        ?.DividendsData?.CashDividends;
                    if (cashDividends != null)
                    {
                        foreach (var div in cashDividends)
                        {
                            DateOnly comDate;
                            if (div.LastDatePrior.HasValue)
                            {
                                comDate = DateOnly.FromDateTime(div.LastDatePrior.Value);
                            }
                            else if (div.ApprovedOn.HasValue)
                            {
                                comDate = DateOnly.FromDateTime(div.ApprovedOn.Value);
                            }
                            else
                            {
                                continue;
                            }

                            DateOnly? payDate = div.PaymentDate.HasValue
                                ? DateOnly.FromDateTime(div.PaymentDate.Value)
                                : null;

                            dividendsList.Add(
                                new NormalizedDividend(
                                    Ticker: ticker.ToUpperInvariant(),
                                    ComDate: comDate,
                                    PaymentDate: payDate,
                                    Rate: div.Rate,
                                    DividendType: div.RelatedTo ?? div.Label ?? "Rendimento",
                                    Currency: "BRL",
                                    SourceProvider: ProviderName
                                )
                            );
                        }
                    }

                    return Result<IReadOnlyList<NormalizedDividend>>.Success(dividendsList);
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Brapi] Error fetching dividends for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                Error.Failure("Brapi.Exception", ex.Message)
            );
        }
    }

    /// <summary>
    /// Sends a GET through the resilience pipeline's failure contract: transport-level
    /// exceptions are rethrown as <see cref="ProviderCallException"/> so retry and the
    /// per-provider circuit breaker can react (raw exceptions would bypass both).
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        string url,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (Exception ex)
            when (ex is HttpRequestException or HttpIOException
                || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            )
        {
            throw ProviderResilience.AsProviderCall(ProviderName, ex);
        }
    }

    private string? AcquireToken() =>
        _apiKeyPool.HasKeys(ProviderName) ? _apiKeyPool.Acquire(ProviderName) : null;

    private void ReportToken(string? token, KeyResult result, TimeSpan? retryAfter = null)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _apiKeyPool.Report(ProviderName, token, result, retryAfter);
        }
    }

    /// <summary>Maps 429 / 401 / 403 to their pool reports and error codes. Returns null
    /// when the response may proceed; otherwise the failure to propagate.</summary>
    private Error? GuardResponse(string? token, HttpResponseMessage response, string operation)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = ParseRetryAfter(response);
            ReportToken(token, KeyResult.RateLimited, retryAfter);
            _logger.LogWarning(
                "[Brapi] Rate limit reached (HTTP 429) on {Operation}; key cooldown applied.",
                operation
            );
            return Error.Failure("Brapi.RateLimit", "Brapi API rate limit exceeded (HTTP 429)");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            ReportToken(token, KeyResult.Invalid);
            return Error.Failure(
                "Brapi.AuthFailed",
                $"Brapi rejected the credential (HTTP {(int)response.StatusCode})"
            );
        }

        if (!response.IsSuccessStatusCode)
        {
            // Other errors don't touch the key state.
            return Error.Failure("Brapi.HttpError", $"Brapi returned HTTP {response.StatusCode}");
        }

        return null;
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var until = date - DateTimeOffset.UtcNow;
            return until > TimeSpan.Zero ? until : TimeSpan.Zero;
        }

        return null;
    }

    private Result<T> PoolExhausted<T>() =>
        Result<T>.Failure(
            Error.Failure(
                "Provider.PoolExhausted",
                $"{ProviderName} has no healthy API key right now "
                    + "(all cooling down, disabled or throttled); falling through to the next provider"
            )
        );

    private static string CalculateRange(DateOnly startDate, DateOnly endDate)
    {
        var days = endDate.DayNumber - startDate.DayNumber;
        if (days <= 5)
            return "5d";
        if (days <= 30)
            return "1mo";
        if (days <= 90)
            return "3mo";
        if (days <= 180)
            return "6mo";
        if (days <= 365)
            return "1y";
        if (days <= 730)
            return "2y";
        if (days <= 1825)
            return "5y";
        return "max";
    }

    // JSON DTOs for deserialization
    public sealed class BrapiQuoteResponse
    {
        [JsonPropertyName("results")]
        public List<BrapiAssetResult>? Results { get; set; }
    }

    public sealed class BrapiAssetResult
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("historicalDataPrice")]
        public List<BrapiHistoricalPrice>? HistoricalDataPrice { get; set; }

        [JsonPropertyName("dividendsData")]
        public BrapiDividendsData? DividendsData { get; set; }

        // /quote/list snapshot fields (all optional — anonymous payloads vary)
        [JsonPropertyName("regularMarketPrice")]
        public decimal RegularMarketPrice { get; set; }

        [JsonPropertyName("regularMarketOpen")]
        public decimal RegularMarketOpen { get; set; }

        [JsonPropertyName("regularMarketDayHigh")]
        public decimal RegularMarketDayHigh { get; set; }

        [JsonPropertyName("regularMarketDayLow")]
        public decimal RegularMarketDayLow { get; set; }

        [JsonPropertyName("regularMarketVolume")]
        public decimal? RegularMarketVolume { get; set; }
    }

    public sealed class BrapiHistoricalPrice
    {
        [JsonPropertyName("date")]
        public long Date { get; set; }

        [JsonPropertyName("open")]
        public decimal Open { get; set; }

        [JsonPropertyName("high")]
        public decimal High { get; set; }

        [JsonPropertyName("low")]
        public decimal Low { get; set; }

        [JsonPropertyName("close")]
        public decimal Close { get; set; }

        [JsonPropertyName("adjustedClose")]
        public decimal AdjustedClose { get; set; }

        [JsonPropertyName("volume")]
        public decimal Volume { get; set; }
    }

    public sealed class BrapiDividendsData
    {
        [JsonPropertyName("cashDividends")]
        public List<BrapiCashDividend>? CashDividends { get; set; }
    }

    public sealed class BrapiCashDividend
    {
        [JsonPropertyName("rate")]
        public decimal Rate { get; set; }

        [JsonPropertyName("paymentDate")]
        public DateTime? PaymentDate { get; set; }

        [JsonPropertyName("approvedOn")]
        public DateTime? ApprovedOn { get; set; }

        [JsonPropertyName("lastDatePrior")]
        public DateTime? LastDatePrior { get; set; }

        [JsonPropertyName("relatedTo")]
        public string? RelatedTo { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }
    }
}
