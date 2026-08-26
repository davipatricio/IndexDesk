namespace IndexDesk.Modules.Portfolio.Calculators;

/// <summary>One valuation point of the portfolio wealth series.</summary>
public sealed record WealthPoint(DateOnly Date, decimal Value, decimal ExternalFlow);

/// <summary>Pure math behind the performance tab. No I/O — inputs come pre-built.</summary>
public static class PerformanceEngine
{
    /// <summary>
    /// Daily-linked TWR over the wealth points: each period return neutralizes the
    /// external flows credited to that day (r_i = V_i / (V_{i-1} + F_i) - 1). The first
    /// point is the baseline — its own level (not the gap between cost and market) is not
    /// counted as return, so a single-flow portfolio matches the simple period return.
    /// </summary>
    public static decimal TimeWeightedReturnPercent(IReadOnlyList<WealthPoint> points)
    {
        if (points.Count < 2)
            return 0m;

        var factor = 1m;
        for (var i = 1; i < points.Count; i++)
        {
            var baseValue = points[i - 1].Value + points[i].ExternalFlow;
            if (baseValue > 0)
                factor *= 1m + (points[i].Value / baseValue - 1m);
        }

        return (factor - 1m) * 100m;
    }

    /// <summary>
    /// Annualized money-weighted return (XIRR) for dated flows plus the terminal value
    /// (entered as a positive amount on the end date). Negative amounts = capital in.
    /// Returns null when there is no sign change to solve (e.g. only one outflow).
    /// </summary>
    public static decimal? MoneyWeightedReturnAnnualPercent(
        IReadOnlyList<(DateOnly Date, decimal Amount)> flows,
        DateOnly endDate,
        decimal terminalValue
    )
    {
        if (terminalValue <= 0 && flows.All(f => f.Amount >= 0))
            return null;

        var signed = new List<(double Years, double Amount)>();
        foreach (var (date, amount) in flows)
        {
            if (amount == 0)
                continue;
            // Convenção XIRR: capital investido = negativo na data do fluxo.
            signed.Add((YearsBetween(date, endDate), (double)amount));
        }
        if (terminalValue != 0)
            signed.Add((0d, (double)terminalValue));

        var hasNegative = signed.Any(f => f.Amount < 0);
        var hasPositive = signed.Any(f => f.Amount > 0);
        if (!hasNegative || !hasPositive || signed.Count < 2)
            return null;

        // Forward-compounding formulation: each flow grows to the end date,
        // f(r) = Σ A_i (1+r)^(t_i) + terminalValue, with t_i = years until the end date.
        const double guess = 0.1;
        var rate = guess;
        for (var i = 0; i < 100; i++)
        {
            var f = Npv(signed, rate);
            if (Math.Abs(f) < 0.0005)
                break;
            var df = NpvDerivative(signed, rate);
            if (Math.Abs(df) < 1e-12)
                break;
            var next = rate - f / df;
            if (next <= -0.999999)
                next = (rate - 0.999999) / 2;
            rate = next;
        }

        if (Math.Abs(Npv(signed, rate)) >= 0.01)
        {
            double low = -0.9999,
                high = 10;
            for (var i = 0; i < 200; i++)
            {
                var mid = (low + high) / 2;
                if (Npv(signed, low) * Npv(signed, mid) <= 0)
                    high = mid;
                else
                    low = mid;
            }
            rate = (low + high) / 2;
        }

        return (decimal)(rate * 100);
    }

    public sealed record RiskMetrics(
        decimal VolatilityPercentAnnualized,
        decimal MaxDrawdownPercent,
        decimal SharpeRatio
    );

    /// <summary>
    /// Risk metrics from an ordered wealth series. Optional daily risk-free factors
    /// (1+i) aligned to the returns enable a CDI-excess Sharpe; without them rf = 0.
    /// </summary>
    public static RiskMetrics ComputeRisk(
        IReadOnlyList<WealthPoint> points,
        IReadOnlyList<decimal>? dailyRiskFreeFactors = null
    )
    {
        if (points.Count < 3)
            return new RiskMetrics(0m, 0m, 0m);

        var returns = new List<double>(points.Count - 1);
        for (var i = 1; i < points.Count; i++)
        {
            if (points[i - 1].Value > 0)
                returns.Add((double)(points[i].Value / points[i - 1].Value - 1m));
        }
        if (returns.Count < 2)
            return new RiskMetrics(0m, 0m, 0m);

        var mean = returns.Average();
        var variance = returns.Sum(r => (r - mean) * (r - mean)) / (returns.Count - 1);
        var std = Math.Sqrt(variance);
        var volatility = std * Math.Sqrt(252) * 100;

        // Max drawdown over the raw values.
        decimal peak = points[0].Value;
        var maxDrawdown = 0m;
        foreach (var p in points)
        {
            if (p.Value > peak)
                peak = p.Value;
            if (peak > 0)
            {
                var dd = (peak - p.Value) / peak * 100;
                if (dd > maxDrawdown)
                    maxDrawdown = dd;
            }
        }

        // Sharpe: annualized mean excess / annualized deviation of excess.
        double sumExcess = 0;
        var excess = new List<double>(returns.Count);
        for (var i = 0; i < returns.Count; i++)
        {
            var rf =
                dailyRiskFreeFactors is not null && i < dailyRiskFreeFactors.Count
                    ? (double)dailyRiskFreeFactors[i] - 1
                    : 0d;
            var e = returns[i] - rf;
            excess.Add(e);
            sumExcess += e;
        }
        var excessMean = sumExcess / excess.Count;
        var excessVar = excess.Sum(e => (e - excessMean) * (e - excessMean)) / (excess.Count - 1);
        var excessStd = Math.Sqrt(excessVar);
        var sharpe = excessStd > 0 ? (decimal)(excessMean / excessStd * Math.Sqrt(252)) : 0m;

        return new RiskMetrics((decimal)volatility, maxDrawdown, sharpe);
    }

    private static double Npv(List<(double Years, double Amount)> flows, double rate)
    {
        double total = 0;
        foreach (var (years, amount) in flows)
            total += amount * Math.Pow(1 + rate, years);
        return total;
    }

    private static double NpvDerivative(List<(double Years, double Amount)> flows, double rate)
    {
        double total = 0;
        foreach (var (years, amount) in flows)
            total += years * amount * Math.Pow(1 + rate, years - 1);
        return total;
    }

    private static double YearsBetween(DateOnly from, DateOnly to) =>
        (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).TotalDays / 365.0;

    private static decimal ReturnPercent(decimal @base, decimal value) =>
        @base > 0 ? (value / @base - 1m) * 100m : 0m;
}
