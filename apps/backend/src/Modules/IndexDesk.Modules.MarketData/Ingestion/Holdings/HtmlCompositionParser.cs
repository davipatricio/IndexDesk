using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;

namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

/// <summary>
/// Tolerant HTML composition parser for the national managers that publish daily
/// portfolio tables: It Now (<c>itnow.com.br/{ticker}/composicao/</c>) and Investo
/// (<c>investoetf.com/etf/{ticker}/</c>). Layout facts learned from the live pages:
/// It Now rows carry a B3 ticker cell; Investo rows carry company names only, and
/// both sites add a country-exposure table ("País") that must NOT become holdings.
/// Strategy: classify each table by its header row, then scan data rows for an
/// optional ticker-shaped cell plus a percentage cell. Zero matches means the
/// layout really changed (Scrape.SelectorChanged).
/// </summary>
public static partial class HtmlCompositionParser
{
    [GeneratedRegex("^[A-Z]{4}[0-9]{1,2}$|^[A-Z]{3,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex TickerShape();

    [GeneratedRegex("-?[0-9]+(?:[.,][0-9]+)?\\s*%")]
    private static partial Regex PercentCell();

    [GeneratedRegex(
        "pa[íi]s|exposi[çc][ãa]o\\s+por\\s+pa[íi]s|country",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex CountryHeader();

    private static readonly HashSet<string> TickerStopWords = new(StringComparer.Ordinal)
    {
        "TOTAL",
        "CASH",
        "FUND",
        "ETF",
        "ATIVO",
        "ACAO",
        "EMPRESA",
        "SETOR",
        "TICKER",
    };

    public static IReadOnlyList<ParsedHolding> Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new HoldingsLayoutException("Composition page returned empty HTML.");
        }

        var document = new HtmlParser().ParseDocument(html);
        var holdings = new List<ParsedHolding>(256);

        foreach (var table in document.QuerySelectorAll("table"))
        {
            if (IsCountryTable(table))
            {
                continue;
            }

            foreach (var tr in table.QuerySelectorAll("tr"))
            {
                var cells = tr.QuerySelectorAll("td, th")
                    .Select(c => c.TextContent.Trim())
                    .Where(t => t.Length > 0)
                    .ToList();
                if (cells.Count < 2)
                {
                    continue;
                }

                // Ticker: any cell shaped like a B3 or US ticker (PETR4, WRLD11,
                // AAPL...). Optional — Investo publishes names only.
                string? ticker = null;
                for (var i = 0; i < cells.Count; i++)
                {
                    var candidate = cells[i].ToUpperInvariant();
                    if (!TickerStopWords.Contains(candidate) && TickerShape().IsMatch(candidate))
                    {
                        ticker = candidate;
                        break;
                    }
                }

                // Weight: any percentage-looking cell in the row.
                decimal? weight = null;
                for (var i = cells.Count - 1; i >= 0; i--)
                {
                    var match = PercentCell().Match(cells[i]);
                    if (
                        match.Success
                        && ISharesHoldingsParser.TryParseWeight(match.Value, out var parsed)
                    )
                    {
                        weight = parsed;
                        break;
                    }
                }

                if (weight is null)
                {
                    continue;
                }

                var name =
                    cells
                        .Where(c => c != ticker && LooksLikeName(c))
                        .OrderByDescending(c => c.Length)
                        .FirstOrDefault()
                    ?? ticker;

                // Label rows ("Total ... 26,64 %") carry a percentage but no asset.
                if (
                    ticker is null
                    && (
                        name is null
                        || TickerStopWords.Contains(name.ToUpperInvariant())
                        || !LooksLikeName(name)
                    )
                )
                {
                    continue;
                }

                holdings.Add(
                    new ParsedHolding(
                        Ticker: ticker,
                        Name: name,
                        WeightPercentage: weight.Value,
                        Sector: null,
                        Country: "BRA"
                    )
                );
            }
        }

        if (holdings.Count == 0)
        {
            throw new HoldingsLayoutException(
                "Composition page matched no holdings rows — layout likely changed "
                    + "(Scrape.SelectorChanged)."
            );
        }

        return holdings;
    }

    /// <summary>Country-exposure tables ("País / Peso") are metadata, not holdings.</summary>
    private static bool IsCountryTable(AngleSharp.Dom.IElement table)
    {
        var firstRow = table.QuerySelector("tr");
        if (firstRow is null)
        {
            return false;
        }

        var headerText = string.Join(
            ' ',
            firstRow.QuerySelectorAll("th, td").Select(c => c.TextContent)
        );
        return CountryHeader().IsMatch(headerText);
    }

    /// <summary>A readable company name: has letters, no '%', longer than 2 chars and
    /// not a bare number (quantity columns like "12.500" are excluded).</summary>
    private static bool LooksLikeName(string cell)
    {
        if (cell.Length <= 2 || cell.Contains('%'))
        {
            return false;
        }

        if (!cell.Any(char.IsLetter))
        {
            return false;
        }

        var digitsOnly = cell.All(c =>
            !char.IsLetter(c) && (char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c))
        );
        return !digitsOnly;
    }
}
