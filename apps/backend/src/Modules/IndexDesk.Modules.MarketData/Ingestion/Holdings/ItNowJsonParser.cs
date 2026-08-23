using System.Text.Json;
using System.Text.RegularExpressions;

namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

/// <summary>
/// It Now structured holdings API (<c>/history-api-json/?type=composicoes-indices&amp;fundo={code}</c>)
/// — discovered via HAR recon (plans/provider-sync-scrapers.md, Fase 0.5 adendo). The
/// <c>fundo</c> parameter is the fund ISIN (e.g. <c>BRBOVVCTF009</c> for BOVV11), which the
/// server renders inline into the composition page's own <c>fetch</c> scripts — so it is
/// extracted from the page at runtime instead of a hardcoded ticker→code map (config map
/// stays as fallback). Response rows carry <c>nome_ticker_fundo</c> (B3 ticker),
/// <c>descricao_ticker_fundo</c> (company name), <c>porcentagem_participacao_fundo</c>
/// (weight, percent points) and <c>data_hora_posicao_carteira_fundo</c> (as-of timestamp).
/// </summary>
public static partial class ItNowJsonParser
{
    /// <summary>Fund-code candidates as embedded by the page's own API calls
    /// (<c>'fundo=BRBOVVCTF009'</c>). Other funds' ISINs appear on the same page (ticker
    /// selector) without this prefix, so the discriminator is the <c>fundo=</c> marker.</summary>
    [GeneratedRegex("fundo=([A-Z0-9]{6,})", RegexOptions.CultureInvariant)]
    private static partial Regex FundCodeCandidate();

    /// <summary>Extracts the fund code the page itself uses for its history-api-json calls.
    /// Ties are impossible in practice (every call embeds the same code); the most frequent
    /// candidate wins. Returns null when the page exposes no candidate.</summary>
    public static string? TryExtractFundCode(string compositionPageHtml)
    {
        if (string.IsNullOrWhiteSpace(compositionPageHtml))
        {
            return null;
        }

        return FundCodeCandidate()
            .Matches(compositionPageHtml)
            .Select(m => m.Groups[1].Value)
            .GroupBy(code => code, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    /// <summary>Parses the <c>composicoes-indices</c> payload: one holding per
    /// <c>dados[]</c> row carrying a fund ticker and a positive weight. Rows without a
    /// ticker (cash/reserve lines) are skipped.</summary>
    public static IReadOnlyList<ParsedHolding> ParseJson(string jsonContent)
    {
        JsonElement dados;
        try
        {
            using var document = JsonDocument.Parse(jsonContent ?? string.Empty);
            if (
                !document.RootElement.TryGetProperty("dados", out var value)
                || value.ValueKind != JsonValueKind.Array
            )
            {
                throw new HoldingsLayoutException(
                    "It Now JSON payload has no 'dados' array — contract changed."
                );
            }

            // Detach from the disposed document by re-parsing into a clone.
            dados = value.Clone();
        }
        catch (JsonException ex)
        {
            throw new HoldingsLayoutException(
                $"It Now JSON payload is not valid JSON: {ex.Message}"
            );
        }

        var holdings = new List<ParsedHolding>(128);
        foreach (var row in dados.EnumerateArray())
        {
            var ticker = OptionalString(row, "nome_ticker_fundo");
            if (string.IsNullOrWhiteSpace(ticker))
            {
                continue; // cash/reserve rows carry no fund ticker
            }

            var weight = OptionalDecimal(row, "porcentagem_participacao_fundo");
            if (weight is null or <= 0)
            {
                continue;
            }

            holdings.Add(
                new ParsedHolding(
                    Ticker: ticker.Trim().ToUpperInvariant(),
                    Name: OptionalString(row, "descricao_ticker_fundo") ?? ticker,
                    WeightPercentage: weight.Value,
                    Sector: null,
                    Country: "BRA"
                )
            );
        }

        if (holdings.Count == 0)
        {
            throw new HoldingsLayoutException(
                "It Now JSON payload matched no holdings rows — contract changed "
                    + "(Scrape.SelectorChanged)."
            );
        }

        return holdings;
    }

    /// <summary>The position date (<c>data_hora_posicao_carteira_fundo</c>) shared by all
    /// rows; null when absent/unset (unset arrives as .NET's default 0001-01-01).</summary>
    public static DateOnly? TryGetAsOfDate(string jsonContent)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent ?? string.Empty);
            if (!document.RootElement.TryGetProperty("dados", out var dados))
            {
                return null;
            }

            DateOnly? asOf = null;
            foreach (var row in dados.EnumerateArray())
            {
                if (
                    row.TryGetProperty("data_hora_posicao_carteira_fundo", out var stamp)
                    && stamp.ValueKind == JsonValueKind.String
                    && DateOnly.TryParse(stamp.GetString(), out var parsed)
                    && parsed != default
                )
                {
                    asOf = asOf is null || parsed > asOf ? parsed : asOf;
                }
            }

            return asOf;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? OptionalString(JsonElement row, string propertyName) =>
        row.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal? OptionalDecimal(JsonElement row, string propertyName)
    {
        if (!row.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }
}
