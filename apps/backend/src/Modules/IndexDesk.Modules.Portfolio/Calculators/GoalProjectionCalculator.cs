namespace IndexDesk.Modules.Portfolio.Calculators;

/// <summary>Pure math behind goal projections. No I/O — inputs come pre-built.</summary>
public static class GoalProjectionCalculator
{
    /// <summary>Hard cap for iterative projections (100 anos em meses).</summary>
    public const int MaxMonths = 1200;

    /// <summary>
    /// Months to reach <paramref name="targetValue"/> from <paramref name="currentValue"/> at a
    /// constant monthly return (run-rate). Null when the return is not positive or there is no
    /// base to compound from; 0 when the target is already met. Rounds up to the whole month.
    /// </summary>
    public static int? RunRateMonths(
        decimal currentValue,
        decimal targetValue,
        decimal recentMonthlyReturnPct
    )
    {
        if (recentMonthlyReturnPct <= 0m || currentValue <= 0m)
            return null;
        if (targetValue <= currentValue)
            return 0;

        var monthlyFactor = (double)(1m + recentMonthlyReturnPct / 100m);
        var months = Math.Log((double)targetValue / (double)currentValue, monthlyFactor);
        return (int)Math.Ceiling(months);
    }

    /// <summary>
    /// Iterative month-by-month simulation of principal plus end-of-month contribution:
    /// value = value × (1 + annualRatePct/1200) + monthlyContribution. Caps at MaxMonths and
    /// returns null when the target is never reached; 0 when it is already met.
    /// </summary>
    public static int? CompoundMonths(
        decimal principal,
        decimal monthlyContribution,
        decimal annualRatePct,
        decimal targetValue
    )
    {
        if (targetValue <= principal)
            return 0;

        var monthlyRate = annualRatePct / 100m / 12m;
        if (monthlyRate <= -1m)
            return null; // capital evapora; projeção perde o sentido
        if (monthlyRate == 0m && monthlyContribution <= 0m)
            return null; // nada faz o saldo crescer

        var value = principal;
        for (var month = 1; month <= MaxMonths; month++)
        {
            value = value * (1m + monthlyRate) + monthlyContribution;
            if (value >= targetValue)
                return month;
        }

        return null;
    }
}
