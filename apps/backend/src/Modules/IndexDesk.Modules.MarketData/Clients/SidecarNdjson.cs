using System.Text.Json;
using System.Text.Json.Serialization;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Domain;

namespace IndexDesk.Modules.MarketData.Clients;

// NDJSON contract v1 line shapes emitted by the sidecar (see
// tools/providers/sidecar/README.md). Dates arrive as ISO-8601 YYYY-MM-DD.
internal sealed record SidecarQuoteLine(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("open")] decimal Open,
    [property: JsonPropertyName("high")] decimal High,
    [property: JsonPropertyName("low")] decimal Low,
    [property: JsonPropertyName("close")] decimal Close,
    [property: JsonPropertyName("adj_close")] decimal AdjClose,
    [property: JsonPropertyName("volume")] decimal Volume
);

internal sealed record SidecarDividendLine(
    [property: JsonPropertyName("ticker")] string Ticker,
    [property: JsonPropertyName("date")] DateOnly Date,
    [property: JsonPropertyName("rate")] decimal Rate,
    [property: JsonPropertyName("type")] string Type
);

/// <summary>
/// Parses and maps sidecar stdout lines into normalized domain records. A single
/// malformed line is a protocol violation (the Python side validates before emitting),
/// so it fails the whole call with <c>Sidecar.ParseError</c>.
/// </summary>
internal static class SidecarNdjson
{
    public static Result<IReadOnlyList<NormalizedQuote>> MapQuotes(
        IReadOnlyList<string> lines,
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        string providerName
    )
    {
        var quotes = new List<NormalizedQuote>(lines.Count);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            SidecarQuoteLine? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<SidecarQuoteLine>(line, SidecarJson.Options);
            }
            catch (JsonException ex)
            {
                return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                    Error.Failure(
                        "Sidecar.ParseError",
                        $"Sidecar emitted an invalid quote line for {providerName}: {ex.Message}"
                    )
                );
            }

            if (parsed is null)
            {
                continue;
            }

            if (parsed.Date < startDate || parsed.Date > endDate)
            {
                continue; // TV/InfoMoney over-fetch bars; keep the requested window only
            }

            quotes.Add(
                new NormalizedQuote(
                    Ticker: ticker.ToUpperInvariant(),
                    Date: parsed.Date,
                    Open: parsed.Open,
                    High: parsed.High,
                    Low: parsed.Low,
                    Close: parsed.Close,
                    AdjClose: parsed.AdjClose,
                    Volume: parsed.Volume,
                    TradesCount: null,
                    SourceProvider: providerName,
                    FetchedAtUtc: DateTimeOffset.UtcNow
                )
            );
        }

        quotes.Sort((a, b) => a.Date.CompareTo(b.Date));
        return Result<IReadOnlyList<NormalizedQuote>>.Success(quotes);
    }

    public static Result<IReadOnlyList<NormalizedDividend>> MapDividends(
        IReadOnlyList<string> lines,
        string ticker,
        string providerName
    )
    {
        var dividends = new List<NormalizedDividend>(lines.Count);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            SidecarDividendLine? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<SidecarDividendLine>(line, SidecarJson.Options);
            }
            catch (JsonException ex)
            {
                return Result<IReadOnlyList<NormalizedDividend>>.Failure(
                    Error.Failure(
                        "Sidecar.ParseError",
                        $"Sidecar emitted an invalid dividend line for {providerName}: {ex.Message}"
                    )
                );
            }

            if (parsed is null)
            {
                continue;
            }

            dividends.Add(
                new NormalizedDividend(
                    Ticker: ticker.ToUpperInvariant(),
                    ComDate: parsed.Date,
                    // The contract carries a single date; downstream keeps the
                    // PaymentDate = ComDate fallback used by YahooFinanceClient.
                    PaymentDate: parsed.Date,
                    Rate: parsed.Rate,
                    DividendType: parsed.Type,
                    Currency: "BRL",
                    SourceProvider: providerName
                )
            );
        }

        dividends.Sort((a, b) => a.ComDate.CompareTo(b.ComDate));
        return Result<IReadOnlyList<NormalizedDividend>>.Success(dividends);
    }
}

internal static class SidecarJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
