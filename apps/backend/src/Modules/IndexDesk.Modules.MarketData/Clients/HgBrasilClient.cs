using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

public class HgBrasilClient : IMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<HgBrasilClient> _logger;

    public string ProviderName => "HGBrasil";
    public int Priority => 3;

    public HgBrasilClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HgBrasilClient> logger
    )
    {
        _httpClient = httpClient;
        _apiKey = configuration["Providers:HGBrasil:ApiKey"] ?? string.Empty;
        _logger = logger;
    }

    public bool SupportsTicker(string ticker)
    {
        return !string.IsNullOrWhiteSpace(ticker)
            && !ticker.StartsWith('^')
            && !ticker.Contains('=');
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
            var url = $"finance/stock_price?symbol={ticker.ToUpperInvariant()}";
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                url += $"&key={_apiKey}";
            }

            _logger.LogInformation("[HGBrasil] Fetching quote for {Ticker}...", ticker);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.Failure(
                        "HGBrasil.HttpError",
                        $"HGBrasil returned HTTP {response.StatusCode}"
                    )
                );
            }

            var content = await response.Content.ReadFromJsonAsync<HgResponse>(
                cancellationToken: cancellationToken
            );
            if (
                content?.Results == null
                || !content.Results.TryGetValue(ticker.ToUpperInvariant(), out var stockData)
                || stockData == null
            )
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.NotFound("HGBrasil.NoData", $"No data returned by HGBrasil for {ticker}")
                );
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today < startDate || today > endDate)
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Success(
                    Array.Empty<NormalizedQuote>()
                );
            }

            var price = stockData.Price ?? 0m;
            if (price <= 0)
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.Failure("HGBrasil.InvalidPrice", "Price is zero or negative")
                );
            }

            var quote = new NormalizedQuote(
                Ticker: ticker.ToUpperInvariant(),
                Date: today,
                Open: price,
                High: price,
                Low: price,
                Close: price,
                AdjClose: price,
                Volume: 0,
                TradesCount: null,
                SourceProvider: ProviderName,
                FetchedAtUtc: DateTimeOffset.UtcNow
            );

            return Result<IReadOnlyList<NormalizedQuote>>.Success(new[] { quote });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HGBrasil] Error fetching quote for {Ticker}.", ticker);
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.Failure("HGBrasil.Exception", ex.Message)
            );
        }
    }

    public Task<Result<IReadOnlyList<NormalizedDividend>>> GetDividendsAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        // HG Brasil stock_price does not provide dividend histories
        return Task.FromResult(
            Result<IReadOnlyList<NormalizedDividend>>.Success(Array.Empty<NormalizedDividend>())
        );
    }

    public sealed class HgResponse
    {
        [JsonPropertyName("results")]
        public Dictionary<string, HgStockItem>? Results { get; set; }
    }

    public sealed class HgStockItem
    {
        [JsonPropertyName("symbol")]
        public string? Symbol { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("change_percent")]
        public decimal? ChangePercent { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }
    }
}
