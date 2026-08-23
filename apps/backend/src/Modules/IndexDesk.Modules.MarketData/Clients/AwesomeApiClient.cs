using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Resilience;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

public interface IAwesomeApiClient
{
    /// <summary>Current quotes for one or more pairs in a single call
    /// (GET /json/last/USD-BRL,EUR-BRL,BTC-BRL).</summary>
    Task<Result<IReadOnlyList<NormalizedFxRate>>> GetLastAsync(
        IReadOnlyList<string> pairs,
        CancellationToken cancellationToken = default
    );

    /// <summary>Daily history for a single pair
    /// (GET /json/daily/{pair}?start_date=YYYYMMDD&amp;end_date=YYYYMMDD).</summary>
    Task<Result<IReadOnlyList<NormalizedFxRate>>> GetDailyAsync(
        string pair,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Native HttpClient client for the AwesomeAPI economy endpoint (economia.awesomeapi.com.br) —
/// no sidecar, free (optional premium token). FX contract: bid/ask, epoch-seconds
/// timestamp; bid is used as the close proxy. Tokens come from the key pool
/// (<c>Providers__AwesomeApi__Tokens__0..N</c>, legacy <c>Token</c> binds as index 0),
/// are appended as a query parameter and never logged — the pool reports outcomes by
/// key index only. 429 → RateLimited (cooldown), 401/403 → Invalid (disabled).
/// </summary>
public class AwesomeApiClient : IAwesomeApiClient
{
    public const string ProviderName = "AwesomeApi";

    private readonly HttpClient _httpClient;
    private readonly IApiKeyPool _apiKeyPool;
    private readonly ProviderResilience _resilience;
    private readonly ILogger<AwesomeApiClient> _logger;

    public AwesomeApiClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<AwesomeApiClient> logger,
        IApiKeyPool apiKeyPool,
        ProviderResilience resilience
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKeyPool = apiKeyPool;
        _resilience = resilience;
    }

    /// <summary>Pairs requested by the daily job (config-overridable).</summary>
    public static readonly IReadOnlyList<string> DefaultPairs = new[]
    {
        "USD-BRL",
        "EUR-BRL",
        "BTC-BRL",
    };

    public async Task<Result<IReadOnlyList<NormalizedFxRate>>> GetLastAsync(
        IReadOnlyList<string> pairs,
        CancellationToken cancellationToken = default
    )
    {
        if (pairs.Count == 0)
        {
            return Result<IReadOnlyList<NormalizedFxRate>>.Success(Array.Empty<NormalizedFxRate>());
        }

        var path = $"json/last/{string.Join(',', pairs.Select(p => p.Trim().ToUpperInvariant()))}";
        return await FetchAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<NormalizedFxRate>>> GetDailyAsync(
        string pair,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = pair.Trim().ToUpperInvariant();
        var path =
            $"json/daily/{normalized}"
            + $"?start_date={startDate:yyyyMMdd}&end_date={endDate:yyyyMMdd}";
        var result = await FetchAsync(path, cancellationToken).ConfigureAwait(false);

        // /json/daily returns newest-first regardless of parameter order.
        if (result.IsSuccess)
        {
            return Result<IReadOnlyList<NormalizedFxRate>>.Success(
                result.Value.OrderBy(r => r.Date).ToList()
            );
        }

        return result;
    }

    private async Task<Result<IReadOnlyList<NormalizedFxRate>>> FetchAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await _resilience.ExecuteAsync(
                ProviderName,
                $"GET {path}",
                async () =>
                {
                    var token = _apiKeyPool.HasKeys(ProviderName)
                        ? _apiKeyPool.Acquire(ProviderName)
                        : null;
                    if (token is null && _apiKeyPool.HasKeys(ProviderName))
                    {
                        return Result<IReadOnlyList<NormalizedFxRate>>.Failure(
                            Error.Failure(
                                "Provider.PoolExhausted",
                                $"{ProviderName} has no healthy token right "
                                    + "(all cooling down or disabled); falling through"
                            )
                        );
                    }

                    var url = path;
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        // Secret: appended to the request only, never logged.
                        url +=
                            (url.Contains('?') ? "&" : "?")
                            + "token="
                            + Uri.EscapeDataString(token);
                    }

                    _logger.LogInformation("[AwesomeApi] GET {Path}...", path);

                    HttpResponseMessage response;
                    try
                    {
                        response = await _httpClient
                            .GetAsync(url, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                        when (ex is HttpRequestException or HttpIOException
                            || (
                                ex is TaskCanceledException
                                && !cancellationToken.IsCancellationRequested
                            )
                        )
                    {
                        throw ProviderResilience.AsProviderCall(ProviderName, ex);
                    }

                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (token is not null)
                        {
                            _apiKeyPool.Report(
                                ProviderName,
                                token,
                                KeyResult.RateLimited,
                                ParseRetryAfter(response)
                            );
                        }

                        return Result<IReadOnlyList<NormalizedFxRate>>.Failure(
                            Error.Failure(
                                $"{ProviderName}.RateLimit",
                                "AwesomeAPI rate limit exceeded (HTTP 429)"
                            )
                        );
                    }

                    if (
                        response.StatusCode
                        is HttpStatusCode.Unauthorized
                            or HttpStatusCode.Forbidden
                    )
                    {
                        if (token is not null)
                        {
                            _apiKeyPool.Report(ProviderName, token, KeyResult.Invalid);
                        }

                        return Result<IReadOnlyList<NormalizedFxRate>>.Failure(
                            Error.Failure(
                                $"{ProviderName}.AuthFailed",
                                $"AwesomeAPI rejected the token (HTTP {(int)response.StatusCode})"
                            )
                        );
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        return Result<IReadOnlyList<NormalizedFxRate>>.Failure(
                            Error.Failure(
                                $"{ProviderName}.HttpError",
                                $"AwesomeAPI returned HTTP {(int)response.StatusCode}"
                            )
                        );
                    }

                    if (token is not null)
                    {
                        _apiKeyPool.Report(ProviderName, token, KeyResult.Success);
                    }

                    var payload = await response
                        .Content.ReadFromJsonAsync<Dictionary<string, AwesomeApiQuote>>(
                            cancellationToken: cancellationToken
                        )
                        .ConfigureAwait(false);
                    if (payload is null || payload.Count == 0)
                    {
                        return Result<IReadOnlyList<NormalizedFxRate>>.Failure(
                            Error.NotFound(
                                $"{ProviderName}.NoData",
                                "No FX data returned by AwesomeAPI"
                            )
                        );
                    }

                    var rates = new List<NormalizedFxRate>(payload.Count);
                    foreach (
                        var (key, quote) in payload
                            .Where(kv => kv.Value is not null)
                            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    )
                    {
                        rates.Add(
                            new NormalizedFxRate(
                                Pair: NormalizePair(key),
                                Date: DateOnly.FromDateTime(
                                    DateTimeOffset.FromUnixTimeSeconds(quote.Timestamp).UtcDateTime
                                ),
                                Bid: quote.Bid,
                                Ask: quote.Ask,
                                Open: quote.Open,
                                High: quote.High,
                                Low: quote.Low,
                                TimestampEpochSeconds: quote.Timestamp,
                                SourceProvider: ProviderName
                            )
                        );
                    }

                    return Result<IReadOnlyList<NormalizedFxRate>>.Success(rates);
                },
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AwesomeApi] Error fetching {Path}.", path);
            return Result<IReadOnlyList<NormalizedFxRate>>.Failure(
                Error.Failure($"{ProviderName}.Exception", ex.Message)
            );
        }
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

    /// <summary>The API keys responses by the pair with a numeric suffix on history
    /// endpoints ("USDBRL", "USDBRL1", "USDBRL2"); collapse back to the canonical form.</summary>
    internal static string NormalizePair(string apiKey)
    {
        var trimmed = apiKey.Trim().ToUpperInvariant();
        var letters = new string(trimmed.TakeWhile(char.IsLetter).ToArray());
        return letters.Length >= 6 ? $"{letters[..3]}-{letters[3..6]}" : trimmed;
    }

    public sealed class AwesomeApiQuote
    {
        [System.Text.Json.Serialization.JsonPropertyName("high")]
        public decimal High { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("low")]
        public decimal Low { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("open")]
        public decimal Open { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("bid")]
        public decimal Bid { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ask")]
        public decimal Ask { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }
    }
}
