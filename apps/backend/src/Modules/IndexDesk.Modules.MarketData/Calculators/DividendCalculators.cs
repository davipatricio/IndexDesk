namespace IndexDesk.Modules.MarketData.Calculators;

/// <summary>
/// Pure helpers for the asset dividend sheet ("quanto o ativo pagou" panel).
/// </summary>
public static class DividendCalculators
{
    /// <summary>Sums the per-quote rates of events whose reference date falls in the trailing 12 months before <paramref name="asOf"/>.</summary>
    public static decimal SumLast12Months(
        IReadOnlyList<(DateOnly Date, decimal Rate)> events,
        DateOnly asOf
    )
    {
        var from = asOf.AddYears(-1);
        decimal total = 0m;
        foreach (var (date, rate) in events)
        {
            if (date >= from && date <= asOf)
                total += rate;
        }

        return Math.Round(total, 6);
    }

    /// <summary>
    /// Trailing-12-months dividend yield: last-year cash per quote divided by the
    /// current price, in percent. Returns null when there is no usable price.
    /// </summary>
    public static decimal? YieldPercent(decimal last12MonthsTotal, decimal? currentPrice)
    {
        if (currentPrice is not { } price || price <= 0m)
            return null;
        return Math.Round(last12MonthsTotal / price * 100m, 2);
    }
}
