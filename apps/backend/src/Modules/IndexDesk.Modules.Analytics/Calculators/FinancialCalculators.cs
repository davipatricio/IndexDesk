namespace IndexDesk.Modules.Analytics.Calculators;

public static class FinancialCalculators
{
    /// <summary>
    /// Calculates real yield using the Fisher Equation: (1 + Nominal) = (1 + Real) * (1 + Inflation)
    /// Real = ((1 + Nominal) / (1 + Inflation)) - 1
    /// </summary>
    public static decimal CalculateRealYield(decimal nominalRate, decimal inflationRate)
    {
        if (inflationRate <= -1.0m)
            throw new ArgumentOutOfRangeException(
                nameof(inflationRate),
                "Inflation rate cannot be -100% or lower."
            );

        var nominalDecimal = nominalRate > 1.0m ? nominalRate / 100m : nominalRate;
        var inflationDecimal = inflationRate > 1.0m ? inflationRate / 100m : inflationRate;

        var realYield = ((1m + nominalDecimal) / (1m + inflationDecimal)) - 1m;
        return Math.Round(realYield * 100m, 4);
    }

    /// <summary>
    /// Calculates Maximum Drawdown from an equity curve series.
    /// </summary>
    public static decimal CalculateMaxDrawdown(IReadOnlyList<decimal> equityCurve)
    {
        if (equityCurve == null || equityCurve.Count == 0)
            return 0m;

        var peak = equityCurve[0];
        var maxDrawdown = 0m;

        foreach (var value in equityCurve)
        {
            if (value > peak)
                peak = value;

            if (peak > 0)
            {
                var drawdown = (value - peak) / peak;
                if (drawdown < maxDrawdown)
                    maxDrawdown = drawdown;
            }
        }

        return Math.Round(maxDrawdown * 100m, 2);
    }

    /// <summary>
    /// Calculates annualized Sharpe Ratio assuming a risk-free rate (CDI/Selic).
    /// </summary>
    public static decimal CalculateSharpeRatio(
        decimal annualizedReturn,
        decimal riskFreeRate,
        decimal annualizedVolatility
    )
    {
        if (annualizedVolatility <= 0m)
            return 0m;

        var returnDec = annualizedReturn > 1m ? annualizedReturn / 100m : annualizedReturn;
        var riskFreeDec = riskFreeRate > 1m ? riskFreeRate / 100m : riskFreeRate;
        var volDec = annualizedVolatility > 1m ? annualizedVolatility / 100m : annualizedVolatility;

        var sharpe = (returnDec - riskFreeDec) / volDec;
        return Math.Round(sharpe, 2);
    }
}
