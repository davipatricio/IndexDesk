using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;

namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

/// <summary>
/// iShares (BlackRock) holdings CSV. The old hardcoded product-id URL pattern is dead
/// (404): the ajax CSV link rotates, so it is extracted at runtime from the product
/// page HTML. Parsing is defensive — pt-BR decimal commas, '%' suffixes, metadata
/// preamble lines and renamed columns must not break the feed.
/// </summary>
public static partial class ISharesHoldingsParser
{
    [GeneratedRegex(
        "https?://[^\"'\\s<>]+?\\?fileType=csv[^\"'\\s<>]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    internal static partial Regex AjaxCsvLink();

    /// <summary>Same link when the page emits it as a quoted root-relative href — the
    /// live BOVA11/IVVB11 product pages do exactly that
    /// (<c>href="/br/products/{id}/{slug}/{hash}.ajax?fileType=csv&amp;..."</c>).</summary>
    [GeneratedRegex(
        "[\"'](/[^\"'\\s<>]+\\.ajax\\?fileType=csv[^\"'\\s<>]*)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    internal static partial Regex AjaxCsvLinkRelative();

    /// <summary>Extracts the rotating ajax CSV download link from a product page
    /// (.ajax / .jsp variants; the hash/id rotates). Handles absolute URLs and
    /// root-relative hrefs (resolved against <paramref name="baseUrl"/>, the product page
    /// URL, when it is absolute; otherwise the relative path is returned and HttpClient
    /// resolves it against its BaseAddress). Returns null when the page no longer exposes
    /// a CSV link.</summary>
    public static string? ExtractAjaxCsvUrl(string productPageHtml, string? baseUrl = null)
    {
        var html = productPageHtml ?? string.Empty;
        var match = AjaxCsvLink().Match(html);
        var url =
            match.Success ? match.Value
            : AjaxCsvLinkRelative().Match(html) is { Success: true } relative
                ? relative.Groups[1].Value
            : string.Empty;

        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        // HTML entities (&amp;) survive the attribute scrape; decode both layers.
        url = Uri.UnescapeDataString(url.Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase));

        if (
            url.StartsWith('/')
            && !string.IsNullOrWhiteSpace(baseUrl)
            && baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        )
        {
            url = new Uri(new Uri(baseUrl), url).ToString();
        }

        return url;
    }

    private static readonly Dictionary<string, string> ColumnMap = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["ticker"] = "ticker",
        ["codigo"] = "ticker",
        ["código"] = "ticker",
        ["name"] = "name",
        ["nome"] = "name",
        ["ativo"] = "name",
        ["sector"] = "sector",
        ["setor"] = "sector",
        ["weight (%)"] = "weight",
        ["weight(%)"] = "weight",
        ["weight %"] = "weight",
        ["peso (%)"] = "weight",
        ["participação (%)"] = "weight",
        ["participacao (%)"] = "weight",
        ["country"] = "country",
        ["país"] = "country",
        ["pais"] = "country",
        ["location"] = "country",
    };

    public static IReadOnlyList<ParsedHolding> ParseCsv(string csvContent)
    {
        using var reader = new StringReader(csvContent);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
            HeaderValidated = null,
        };

        using var csv = new CsvParser(reader, config);
        if (!csv.Read())
        {
            throw new HoldingsLayoutException("iShares CSV is empty.");
        }

        // Walk rows until a header-like row appears (preamble holds fund metadata).
        string[]? header = null;
        while (header is null)
        {
            var row = csv.Record;
            if (row is null)
            {
                throw new HoldingsLayoutException("iShares CSV has no header row.");
            }

            if (row.Any(cell => cell is not null && ColumnMap.ContainsKey(cell.Trim())))
            {
                header = row.Select(c => c?.Trim() ?? string.Empty).ToArray();
                break;
            }

            if (!csv.Read())
            {
                throw new HoldingsLayoutException("iShares CSV has no recognizable header row.");
            }
        }

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header!.Length; i++)
        {
            if (ColumnMap.TryGetValue(header[i], out var canonical))
            {
                columns[canonical] = i;
            }
        }

        if (!columns.ContainsKey("weight"))
        {
            throw new HoldingsLayoutException(
                $"iShares CSV weight column missing (headers: {string.Join(", ", header)})"
            );
        }

        var holdings = new List<ParsedHolding>(128);
        while (csv.Read())
        {
            var row = csv.Record;
            if (row is null || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var rawWeight = Get(row, columns, "weight");
            if (!TryParseWeight(rawWeight, out var weight))
            {
                continue; // footer/summary lines carry no numeric weight
            }

            var tickerCell = Get(row, columns, "ticker");
            holdings.Add(
                new ParsedHolding(
                    Ticker: NormalizeTicker(tickerCell),
                    Name: Get(row, columns, "name") ?? tickerCell ?? "Unknown",
                    WeightPercentage: weight,
                    Sector: Get(row, columns, "sector"),
                    Country: Get(row, columns, "country")
                )
            );
        }

        return holdings;
    }

    private static string? Get(
        string[] row,
        Dictionary<string, int> columns,
        string canonicalName
    ) =>
        columns.TryGetValue(canonicalName, out var index) && index < row.Length
            ? row[index]?.Trim()
            : null;

    /// <summary>Tolerant weight parse: "8.51%", "8,51", "0.0851", "" → false.
    /// Locale rule learned from the live feeds: a decimal COMMA means the value is
    /// already percent points ("11,00" = 11%, "1,32" = 1.32%) — pt-BR sources never
    /// publish fractions. Only a dot-only value starting "0." is treated as a fraction
    /// (legacy US files, "0.0851" = 8.51%); plain "1.32" stays 1.32%.</summary>
    public static bool TryParseWeight(string? raw, out decimal weightPercentage)
    {
        weightPercentage = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var cleaned = raw.Replace("%", string.Empty).Replace("\u00a0", " ").Trim();
        var hasCommaDecimal = cleaned.Contains(',');
        if (hasCommaDecimal)
        {
            // pt-BR: '.' thousands separators + ',' decimals ("1.234,56"), or bare
            // comma decimals ("11,00").
            cleaned = cleaned.Replace(".", string.Empty).Replace(',', '.');
        }

        if (
            !decimal.TryParse(
                cleaned,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value
            )
        )
            return false;

        // Fractions exist only in dot-decimal files ("0.0851"); anything else is
        // already percent points.
        var isFraction =
            !hasCommaDecimal && value < 1m && cleaned.StartsWith("0.", StringComparison.Ordinal);
        weightPercentage = isFraction ? decimal.Round(value * 100m, 4) : value;
        return true;
    }

    internal static string? NormalizeTicker(string? cell)
    {
        var trimmed = cell?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        // B3 tickers: 4 letters + 1-2 digits/letters (PETR4, BOVA11, GOLD11).
        return trimmed.Length is >= 5 and <= 6 && trimmed.All(char.IsLetterOrDigit)
            ? trimmed
            : trimmed;
    }
}
