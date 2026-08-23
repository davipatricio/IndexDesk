using ClosedXML.Excel;

namespace IndexDesk.Modules.MarketData.Ingestion.Holdings;

/// <summary>
/// State Street SPDR holdings XLSX (<c>holdings-daily-us-en-{ticker}.xlsx</c> —
/// predictable per-ticker URL). Parsed with ClosedXML; header row is located by
/// scanning for a "Ticker"/"Weight" pair so cosmetic sheet changes stay tolerated.
/// </summary>
public static class SpdrHoldingsParser
{
    private static readonly Dictionary<string, string> ColumnMap = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["ticker"] = "ticker",
        ["symbol"] = "ticker",
        ["name"] = "name",
        ["weight"] = "weight",
        ["weight (%)"] = "weight",
        ["sector"] = "sector",
        ["location"] = "country",
        ["country"] = "country",
    };

    public static IReadOnlyList<ParsedHolding> ParseXlsx(Stream xlsxStream)
    {
        using var workbook = new XLWorkbook(xlsxStream);

        foreach (var worksheet in workbook.Worksheets)
        {
            var headerRow = FindHeaderRow(worksheet);
            if (headerRow is null)
            {
                continue;
            }

            return ReadRows(worksheet, headerRow.Value);
        }

        throw new HoldingsLayoutException(
            $"No holdings header row found in any of the {workbook.Worksheets.Count} sheet(s)."
        );
    }

    private static int? FindHeaderRow(IXLWorksheet worksheet)
    {
        var lastRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 0, 60);
        for (var r = 1; r <= lastRow; r++)
        {
            var cells = worksheet
                .Row(r)
                .CellsUsed()
                .Select(c => c.GetString().Trim())
                .Where(s => s.Length > 0)
                .ToList();
            if (
                cells.Any(c => ColumnMap.ContainsKey(c) && ColumnMap[c] == "ticker")
                && cells.Any(c => ColumnMap.ContainsKey(c) && ColumnMap[c] == "weight")
            )
            {
                return r;
            }
        }

        return null;
    }

    private static IReadOnlyList<ParsedHolding> ReadRows(IXLWorksheet worksheet, int headerRow)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in worksheet.Row(headerRow).CellsUsed())
        {
            if (ColumnMap.TryGetValue(cell.GetString().Trim(), out var canonical))
            {
                columns[canonical] = cell.Address.ColumnNumber;
            }
        }

        if (!columns.TryGetValue("weight", out var weightCol))
        {
            throw new HoldingsLayoutException("SPDR XLSX has no Weight column.");
        }

        var holdings = new List<ParsedHolding>(512);
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            // Footer/summary lines ("Total 100") carry a weight but no identity.
            if (
                !columns.TryGetValue("ticker", out var tCol)
                || worksheet.Row(r).Cell(tCol).IsEmpty()
            )
            {
                continue;
            }

            var weightCell = worksheet.Row(r).Cell(weightCol);
            if (weightCell.IsEmpty())
            {
                continue;
            }

            var rawWeight =
                weightCell.DataType == XLDataType.Number
                    ? weightCell
                        .GetDouble()
                        .ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : weightCell.GetString();
            if (!ISharesHoldingsParser.TryParseWeight(rawWeight, out var weight))
            {
                continue;
            }

            var tickerCell = worksheet.Row(r).Cell(tCol).GetString().Trim();

            holdings.Add(
                new ParsedHolding(
                    Ticker: string.IsNullOrEmpty(tickerCell) ? null : tickerCell.ToUpperInvariant(),
                    Name: columns.TryGetValue("name", out var nCol)
                        ? worksheet.Row(r).Cell(nCol).GetString().Trim()
                        : tickerCell,
                    WeightPercentage: weight,
                    Sector: columns.TryGetValue("sector", out var sCol)
                        ? worksheet.Row(r).Cell(sCol).GetString().Trim()
                        : null,
                    Country: columns.TryGetValue("country", out var cCol)
                        ? worksheet.Row(r).Cell(cCol).GetString().Trim()
                        : null
                )
            );
        }

        return holdings;
    }
}
