using IndexDesk.Modules.Portfolio.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.Portfolio;

public class FixedIncomeAccrualCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 1, 5);

    private static List<(DateOnly, decimal)> DailyRates(params decimal[] rates)
    {
        var list = new List<(DateOnly, decimal)>();
        var d = Start.AddDays(1);
        foreach (var r in rates)
        {
            while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                d = d.AddDays(1);
            list.Add((d, r));
            d = d.AddDays(1);
        }
        return list;
    }

    [Fact]
    public void CdiAt100Percent_CompoundsEachDailyRate()
    {
        var result = FixedIncomeAccrualCalculator.Accrue(
            new("CDI_PERCENT", 100m, 10_000m, Start, End, DailyRates(0.01m, 0.01m, 0.01m))
        );

        Assert.Equal(1.00030003m, result.Factor); // (1,0001)^3
        Assert.Equal(10003.00m, result.AccruedValue);
    }

    [Fact]
    public void CdiAt50Percent_UsesFractionOfEachRate()
    {
        var full = FixedIncomeAccrualCalculator.Accrue(
            new("CDI_PERCENT", 100m, 10_000m, Start, End, DailyRates(0.02m))
        );
        var half = FixedIncomeAccrualCalculator.Accrue(
            new("CDI_PERCENT", 50m, 10_000m, Start, End, DailyRates(0.02m))
        );

        // 50% do CDI: fator ≈ raiz do fator cheio menos ajuste — comparação de monotonia
        Assert.True(half.Factor < full.Factor);
        Assert.Equal(10000m + 2m * 0.5m * 1m, half.AccruedValue, precision: 0); // ~10001
    }

    [Fact]
    public void CdiPlus_PositiveSpread_BeatsPlainCdi()
    {
        var plain = FixedIncomeAccrualCalculator.Accrue(
            new("CDI_PLUS", 0m, 10_000m, Start, End, DailyRates(0.01m, 0.01m))
        );
        var plus = FixedIncomeAccrualCalculator.Accrue(
            new("CDI_PLUS", 2m, 10_000m, Start, End, DailyRates(0.01m, 0.01m))
        );

        Assert.True(plus.Factor > plain.Factor);
    }

    [Fact]
    public void Prefixed_OneYear_AtRate_EqualsAnnualRate()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2027, 1, 1);

        var result = FixedIncomeAccrualCalculator.Accrue(
            new("PREFIXED", 10m, 10_000m, start, end, [])
        );

        // ~261 dias úteis / 252 ⇒ pouco acima de 10%
        Assert.InRange(result.AccruedValue, 11030m, 11120m);
    }

    [Fact]
    public void Selic_UsesRatesDirectly()
    {
        var result = FixedIncomeAccrualCalculator.Accrue(
            new("SELIC", 0m, 1_000m, Start, End, DailyRates(0.03m))
        );

        Assert.Equal(1000.30m, result.AccruedValue);
    }

    [Fact]
    public void IpcaPlus_AccumulatesMonthlyInflationTimesSpread()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 3, 1);
        var monthly = new List<(DateOnly, decimal)>
        {
            (new DateOnly(2026, 1, 31), 0.5m),
            (new DateOnly(2026, 2, 27), 0.4m),
        };

        var result = FixedIncomeAccrualCalculator.Accrue(
            new("IPCA_PLUS", 6m, 10_000m, start, end, monthly)
        );

        // IPCA acumulado ~0,901% × spread 6% a.a. pró-rata ~40 dias úteis (~0,98%) ⇒ ~1,89%
        Assert.InRange(result.AccruedValue, 10170m, 10205m);
    }

    [Fact]
    public void Recomputing_SameWindow_IsIdempotent()
    {
        var input = new FixedIncomeAccrualCalculator.Input(
            "CDI_PERCENT",
            95m,
            50_000m,
            Start,
            End,
            DailyRates(0.01m, 0.01m)
        );

        var first = FixedIncomeAccrualCalculator.Accrue(input);
        var second = FixedIncomeAccrualCalculator.Accrue(input);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("CDI_PERCENT")]
    [InlineData("PREFIXED")]
    public void InvalidRange_Throws(string indexer)
    {
        Assert.Throws<ArgumentException>(() =>
            FixedIncomeAccrualCalculator.Accrue(new(indexer, 10m, 1000m, End, Start, []))
        );
    }
}
