using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

public class YahooFinanceClient : IMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YahooFinanceClient> _logger;

    public string ProviderName => "YahooFinance";
    public int Priority => 2;

    public YahooFinanceClient(HttpClient httpClient, ILogger<YahooFinanceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public bool SupportsTicker(string ticker)
    {
        // Yahoo supports both B3 (via .SA) and global tickers (VWRA.L, ^BVSP, GC=F, USDBRL=X)
        return !string.IsNullOrWhiteSpace(ticker);
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
            var symbol = ToYahooSymbol(ticker);
            var period1 = new DateTimeOffset(
                startDate.ToDateTime(TimeOnly.MinValue),
                TimeSpan.Zero
            ).ToUnixTimeSeconds();
            var period2 = new DateTimeOffset(
                endDate.ToDateTime(TimeOnly.MaxValue),
                TimeSpan.Zero
            ).ToUnixTimeSeconds();

            var url =
                $"v8/finance/chart/{symbol}?period1={period1}&period2={period2}&interval=1d&events=div%7Csplit";

            _logger.LogInformation(
                "[YahooFinance] Fetching historical chart for {Symbol} ({Period1} to {Period2})...",
                symbol,
                period1,
                period2
            );

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(
                "User-Agent",
                "Mozilla/5.0 (compatible; IndexDesk/1.0; +https://indexdesk.com.br)"
            );

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning(
                    "[YahooFinance] Rate limit reached (HTTP 429) for {Symbol}.",
                    symbol
                );
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.Failure(
                        "YahooFinance.RateLimit",
                        "Yahoo Finance rate limit exceeded (HTTP 429)"
                    )
                );
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[YahooFinance] HTTP {StatusCode} for {Symbol}.",
                    response.StatusCode,
                    symbol
                );
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.Failure(
                        "YahooFinance.HttpError",
                        $"Yahoo Finance returned HTTP {response.StatusCode}"
                    )
                );
            }

            var content = await response.Content.ReadFromJsonAsync<YahooChartResponse>(
                cancellationToken: cancellationToken
            );
            var chartResult = content?.Chart?.Result?.FirstOrDefault();
            if (
                chartResult == null
                || chartResult.Timestamp == null
                || chartResult.Timestamp.Count == 0
            )
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.NotFound(
                        "YahooFinance.NoData",
                        $"No data returned by Yahoo Finance for {symbol}"
                    )
                );
            }

            var timestamps = chartResult.Timestamp;
            var quotesData = chartResult.Indicators?.Quote?.FirstOrDefault();
            var adjCloses = chartResult.Indicators?.Adjclose?.FirstOrDefault()?.Adjclose;

            if (quotesData == null)
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Success(
                    Array.Empty<NormalizedQuote>()
                );
            }

            var list = new List<NormalizedQuote>(timestamps.Count);
            for (var i = 0; i < timestamps.Count; i++)
            {
                var ts = timestamps[i];
                var date = DateOnly.FromDateTime(
                    DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime
                );
                if (date < startDate || date > endDate)
                {
                    continue;
                }

                var open =
                    quotesData.Open != null && quotesData.Open.Count > i
                        ? quotesData.Open[i]
                        : null;
                var high =
                    quotesData.High != null && quotesData.High.Count > i
                        ? quotesData.High[i]
                        : null;
                var low =
                    quotesData.Low != null && quotesData.Low.Count > i ? quotesData.Low[i] : null;
                var close =
                    quotesData.Close != null && quotesData.Close.Count > i
                        ? quotesData.Close[i]
                        : null;
                var vol =
                    quotesData.Volume != null && quotesData.Volume.Count > i
                        ? quotesData.Volume[i]
                        : null;
                var adjClose = adjCloses != null && adjCloses.Count > i ? adjCloses[i] : close;

                // Skip missing/null data entries (e.g. trading halt days)
                if (open == null || high == null || low == null || close == null)
                {
                    continue;
                }

                list.Add(
                    new NormalizedQuote(
                        Ticker: ticker.ToUpperInvariant(),
                        Date: date,
                        Open: open.Value,
                        High: high.Value,
                        Low: low.Value,
                        Close: close.Value,
                        AdjClose: adjClose ?? close.Value,
                        Volume: vol ?? 0m,
                        TradesCount: null,
                        SourceProvider: ProviderName,
                        FetchedAtUtc: DateTimeOffset.UtcNow
                    )
                );
            }

            list.Sort((a, b) => a.Date.CompareTo(b.Date));
            return Result<IReadOnlyList<NormalizedQuote>>.Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YahooFinance] Error fetching quotes for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Failure("YahooFinance.Exception", ex.Message)
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
            var symbol = ToYahooSymbol(ticker);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var url = $"v8/finance/chart/{symbol}?period1=0&period2={now}&interval=1d&events=div";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(
                "User-Agent",
                "Mozilla/5.0 (compatible; IndexDesk/1.0; +https://indexdesk.com.br)"
            );

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                    Error.Failure(
                        "YahooFinance.HttpError",
                        $"Yahoo Finance returned HTTP {response.StatusCode}"
                    )
                );
            }

            var content = await response.Content.ReadFromJsonAsync<YahooChartResponse>(
                cancellationToken: cancellationToken
            );
            var dividendsDict = content?.Chart?.Result?.FirstOrDefault()?.Events?.Dividends;
            var dividendsList = new List<NormalizedDividend>();

            if (dividendsDict != null)
            {
                foreach (var kvp in dividendsDict)
                {
                    var div = kvp.Value;
                    if (div?.Date == null || div.Amount == null)
                        continue;

                    var date = DateOnly.FromDateTime(
                        DateTimeOffset.FromUnixTimeSeconds(div.Date.Value).UtcDateTime
                    );
                    dividendsList.Add(
                        new NormalizedDividend(
                            Ticker: ticker.ToUpperInvariant(),
                            ComDate: date,
                            PaymentDate: date, // Yahoo often provides date of record / ex-date
                            Rate: div.Amount.Value,
                            DividendType: "Provento",
                            Currency: "BRL",
                            SourceProvider: ProviderName
                        )
                    );
                }
            }

            dividendsList.Sort((a, b) => a.ComDate.CompareTo(b.ComDate));
            return Result<IReadOnlyList<NormalizedDividend>>.Success(dividendsList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[YahooFinance] Error fetching dividends for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                Error.Failure("YahooFinance.Exception", ex.Message)
            );
        }
    }

    private static string ToYahooSymbol(string ticker)
    {
        ticker = ticker.Trim();
        if (
            ticker.EndsWith(".SA", StringComparison.OrdinalIgnoreCase)
            || ticker.StartsWith('^')
            || ticker.Contains('=')
            || ticker.Contains('.')
        )
        {
            return ticker;
        }

        return $"{ticker}.SA";
    }

    // JSON DTOs for Yahoo Chart API
    public sealed class YahooChartResponse
    {
        [JsonPropertyName("chart")]
        public YahooChartData? Chart { get; set; }
    }

    public sealed class YahooChartData
    {
        [JsonPropertyName("result")]
        public List<YahooChartResult>? Result { get; set; }
    }

    public sealed class YahooChartResult
    {
        [JsonPropertyName("timestamp")]
        public List<long>? Timestamp { get; set; }

        [JsonPropertyName("indicators")]
        public YahooIndicators? Indicators { get; set; }

        [JsonPropertyName("events")]
        public YahooEvents? Events { get; set; }
    }

    public sealed class YahooIndicators
    {
        [JsonPropertyName("quote")]
        public List<YahooQuoteData>? Quote { get; set; }

        [JsonPropertyName("adjclose")]
        public List<YahooAdjCloseData>? Adjclose { get; set; }
    }

    public sealed class YahooQuoteData
    {
        [JsonPropertyName("open")]
        public List<decimal?>? Open { get; set; }

        [JsonPropertyName("high")]
        public List<decimal?>? High { get; set; }

        [JsonPropertyName("low")]
        public List<decimal?>? Low { get; set; }

        [JsonPropertyName("close")]
        public List<decimal?>? Close { get; set; }

        [JsonPropertyName("volume")]
        public List<decimal?>? Volume { get; set; }
    }

    public sealed class YahooAdjCloseData
    {
        [JsonPropertyName("adjclose")]
        public List<decimal?>? Adjclose { get; set; }
    }

    public sealed class YahooEvents
    {
        [JsonPropertyName("dividends")]
        public Dictionary<string, YahooDividendItem>? Dividends { get; set; }
    }

    public sealed class YahooDividendItem
    {
        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("date")]
        public long? Date { get; set; }
    }
}
