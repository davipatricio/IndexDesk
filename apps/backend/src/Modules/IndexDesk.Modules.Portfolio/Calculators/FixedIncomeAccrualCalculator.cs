namespace IndexDesk.Modules.Portfolio.Calculators;

/// <summary>Resultado do accrual: fator acumulado e valor corrigido.</summary>
public sealed record AccrualResult(decimal Factor, decimal AccruedValue);

/// <summary>
/// Pure fixed-income accrual following local-first conventions (BCB SGS series already
/// persisted): CDI/Selic rows are DAILY percent rates, IPCA rows are MONTHLY percent rates.
/// Business-day counting approximates B3 calendar with weekdays only (documented limitation).
/// Re-running over the same window yields the exact same value — idempotent by construction.
/// </summary>
public static class FixedIncomeAccrualCalculator
{
    private const int BusinessBase = 252;

    public sealed record Input(
        string Indexer,
        decimal IndexerRate,
        decimal Principal,
        DateOnly StartDate,
        DateOnly EndDate,
        IReadOnlyList<(DateOnly Date, decimal Rate)> IndexRates
    );

    public static AccrualResult Accrue(Input input)
    {
        Validate(input);
        return input.Indexer.ToUpperInvariant() switch
        {
            "CDI_PERCENT" => AccrueCdiPercent(input),
            "CDI_PLUS" => AccrueCdiPlusOrSelic(input, plusSpread: true),
            "SELIC" => AccrueCdiPlusOrSelic(input, plusSpread: false),
            "PREFIXED" => AccruePrefixed(input),
            "IPCA_PLUS" => AccrueIpcaPlus(input),
            _ => throw new ArgumentException($"Indexador desconhecido: {input.Indexer}."),
        };
    }

    /// <summary>Ex.: 90% do CDI — cada taxa diária entra multiplicada pela fração.</summary>
    private static AccrualResult AccrueCdiPercent(Input input)
    {
        var fraction = input.IndexerRate / 100m;
        var factor = 1m;
        foreach (var (_, rate) in input.IndexRates)
            factor *= 1m + rate * fraction / 100m;
        return Finalize(factor, input.Principal);
    }

    /// <summary>CDI integral ou CDI + spread anual convertido para base diária 252.</summary>
    private static AccrualResult AccrueCdiPlusOrSelic(Input input, bool plusSpread)
    {
        var dailySpreadPercent = plusSpread ? AnnualToDailyPercent(input.IndexerRate) : 0m;
        var factor = 1m;
        foreach (var (_, rate) in input.IndexRates)
            factor *= 1m + (rate + dailySpreadPercent) / 100m;
        return Finalize(factor, input.Principal);
    }

    /// <summary>Prefixado: juros compostos sobre dias úteis com base 252.</summary>
    private static AccrualResult AccruePrefixed(Input input)
    {
        var businessDays = CountBusinessDays(input.StartDate, input.EndDate);
        var factor = (decimal)
            Math.Pow((double)(1m + input.IndexerRate / 100m), (double)businessDays / BusinessBase);
        return Finalize((decimal)factor, input.Principal);
    }

    /**
     * IPCA + spread anual: correção pelo IPCA acumulado no período (taxas mensais informadas)
     * vezes o crescimento do spread em dias úteis base 252.
     */
    private static AccrualResult AccrueIpcaPlus(Input input)
    {
        var ipcaFactor = 1m;
        foreach (var (_, rate) in input.IndexRates)
            ipcaFactor *= 1m + rate / 100m;

        var businessDays = CountBusinessDays(input.StartDate, input.EndDate);
        var spreadFactor = (decimal)
            Math.Pow((double)(1m + input.IndexerRate / 100m), (double)businessDays / BusinessBase);

        return Finalize(ipcaFactor * spreadFactor, input.Principal);
    }

    /// <summary>((1 + anual%)^(1/252) − 1) × 100.</summary>
    private static decimal AnnualToDailyPercent(decimal annualPercent) =>
        ((decimal)Math.Pow((double)(1m + annualPercent / 100m), 1.0 / BusinessBase) - 1m) * 100m;

    private static int CountBusinessDays(DateOnly start, DateOnly end)
    {
        if (end <= start)
            return 0;
        var count = 0;
        for (var d = start.AddDays(1); d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    private static AccrualResult Finalize(decimal factor, decimal principal) =>
        new(decimal.Round(factor, 8), decimal.Round(principal * factor, 2));

    private static void Validate(Input input)
    {
        if (string.IsNullOrWhiteSpace(input.Indexer))
            throw new ArgumentException("Indexador obrigatório.");
        if (input.EndDate < input.StartDate)
            throw new ArgumentException("Data final anterior à inicial.");
        if (input.Principal < 0)
            throw new ArgumentException("Principal negativo.");
    }
}
