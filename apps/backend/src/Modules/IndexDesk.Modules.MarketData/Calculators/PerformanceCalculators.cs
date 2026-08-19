namespace IndexDesk.Modules.MarketData.Calculators;

/// <summary>
/// Pure, side-effect-free financial computations used by the market-data query APIs.
/// Kept in a <c>Calculators/</c> folder with no I/O so they stay unit-testable.
/// </summary>
public static class PerformanceCalculators
{
    /// <summary>Compounding factor for a single period expressed as a percent (e.g. 0.055 => 0.055%).</summary>
    public static decimal FactorFromPercent(decimal percent) => 1m + percent / 100m;

    /// <summary>Percent growth from a start value to an end value. Guards against zero/negative start.</summary>
    public static decimal ReturnBetween(decimal startValue, decimal endValue)
    {
        if (startValue <= 0m)
            return 0m;
        return (endValue / startValue - 1m) * 100m;
    }

    /// <summary>
    /// Compounds a chronologically ordered list of per-period percent rates (daily or monthly)
    /// into an accumulated percent return: product(1 + r/100) - 1.
    /// </summary>
    public static decimal AccumulateRateSeries(IReadOnlyList<decimal> percentRates)
    {
        if (percentRates is null || percentRates.Count == 0)
            return 0m;

        var factor = 1m;
        foreach (var r in percentRates)
            factor *= FactorFromPercent(r);

        return (factor - 1m) * 100m;
    }

    /// <summary>
    /// Approximate total return for dividend-paying Brazilian assets (FIIs/ETFs):
    /// price return plus cash distributions received in the window, expressed relative to the
    /// start price. This mirrors the "rendimentos + valorização" view used by FII platforms.
    /// </summary>
    public static decimal TotalReturnWithDividends(
        decimal startClose,
        decimal endClose,
        IReadOnlyList<decimal> dividendsInPeriod
    )
    {
        if (startClose <= 0m)
            return 0m;

        var priceReturn = endClose / startClose - 1m;
        var dividendYield = dividendsInPeriod.Sum() / startClose;
        return (priceReturn + dividendYield) * 100m;
    }

    /// <summary>Annualizes a total percent return over a fractional number of years.</summary>
    public static decimal AnnualizedReturn(decimal totalReturnPercent, int calendarDays)
    {
        if (calendarDays <= 0)
            return 0m;

        var years = calendarDays / 365.25m;
        var factor = 1m + totalReturnPercent / 100m;
        if (factor <= 0m)
            return -100m;

        var annualized = Math.Pow((double)factor, 1.0 / (double)years) - 1.0;
        return (decimal)annualized * 100m;
    }

    /// <summary>
    /// Annualized volatility (std dev of daily log/simple returns) over 252 trading days.
    /// </summary>
    public static decimal AnnualizedVolatility(IReadOnlyList<decimal> dailyReturnsPercent)
    {
        if (dailyReturnsPercent is null || dailyReturnsPercent.Count < 2)
            return 0m;

        var mean = dailyReturnsPercent.Average();
        var sumSq = dailyReturnsPercent.Sum(r => (r - mean) * (r - mean));
        var variance = sumSq / (dailyReturnsPercent.Count - 1);
        var dailyVol = (decimal)Math.Sqrt((double)variance);
        return Math.Round(dailyVol * (decimal)Math.Sqrt(252), 2);
    }

    /// <summary>Maximum drawdown of a price series, as a negative percent.</summary>
    public static decimal MaxDrawdown(IReadOnlyList<decimal> prices)
    {
        if (prices is null || prices.Count == 0)
            return 0m;

        var peak = prices[0];
        var maxDd = 0m;
        foreach (var p in prices)
        {
            if (p > peak)
                peak = p;
            if (peak > 0m)
            {
                var dd = (p - peak) / peak * 100m;
                if (dd < maxDd)
                    maxDd = dd;
            }
        }

        return Math.Round(maxDd, 2);
    }

    /// <summary>Computes daily simple returns (percent) from a chronological price series.</summary>
    public static IReadOnlyList<decimal> DailyReturns(IReadOnlyList<decimal> prices)
    {
        var returns = new List<decimal>();
        if (prices is null || prices.Count < 2)
            return returns;

        for (var i = 1; i < prices.Count; i++)
        {
            returns.Add(ReturnBetween(prices[i - 1], prices[i]));
        }

        return returns;
    }
}
