using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

public class BrapiClient : IMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<BrapiClient> _logger;

    public string ProviderName => "Brapi";
    public int Priority => 1;

    public BrapiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BrapiClient> logger
    )
    {
        _httpClient = httpClient;
        _apiKey = configuration["Providers:Brapi:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public bool SupportsTicker(string ticker)
    {
        // Brapi supports all B3 tickers (ETFs, BDRs, FIIs, Stocks)
        return !string.IsNullOrWhiteSpace(ticker)
            && !ticker.StartsWith('^')
            && !ticker.Contains('=')
            && !BenchmarkCatalog.IsBenchmark(ticker); // indices are served by Yahoo only
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
            var range = CalculateRange(startDate, endDate);
            var url = $"quote/{ticker}?range={range}&interval=1d";
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                url += $"&token={_apiKey}";
            }

            _logger.LogInformation(
                "[Brapi] Fetching historical quotes for {Ticker} with range {Range}...",
                ticker,
                range
            );

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("[Brapi] Rate limit reached (HTTP 429) for {Ticker}.", ticker);
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.Failure("Brapi.RateLimit", "Brapi API rate limit exceeded (HTTP 429)")
                );
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[Brapi] HTTP {StatusCode} for {Ticker}.",
                    response.StatusCode,
                    ticker
                );
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.Failure("Brapi.HttpError", $"Brapi returned HTTP {response.StatusCode}")
                );
            }

            var content = await response.Content.ReadFromJsonAsync<BrapiQuoteResponse>(
                cancellationToken: cancellationToken
            );
            if (content?.Results == null || content.Results.Count == 0)
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.NotFound("Brapi.NoData", $"No data returned by Brapi for {ticker}")
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
            var url = $"quote/{ticker}?dividends=true";
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                url += $"&token={_apiKey}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                    Error.Failure("Brapi.HttpError", $"Brapi returned HTTP {response.StatusCode}")
                );
            }

            var content = await response.Content.ReadFromJsonAsync<BrapiQuoteResponse>(
                cancellationToken: cancellationToken
            );
            var dividendsList = new List<NormalizedDividend>();

            var cashDividends = content?.Results?.FirstOrDefault()?.DividendsData?.CashDividends;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Brapi] Error fetching dividends for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                Error.Failure("Brapi.Exception", ex.Message)
            );
        }
    }

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
