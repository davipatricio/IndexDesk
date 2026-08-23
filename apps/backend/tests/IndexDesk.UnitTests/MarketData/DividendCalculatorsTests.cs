using FluentAssertions;
using IndexDesk.Modules.MarketData.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class DividendCalculatorsTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 22);

    [Fact]
    public void SumLast12Months_OnlyCountsEventsInsideWindow()
    {
        var events = new List<(DateOnly, decimal)>
        {
            (new(2024, 6, 1), 0.50m), // outside (older than the 12m window)
            (new(2025, 8, 21), 9.99m), // one day before window start → outside
            (new(2025, 8, 22), 0.10m), // boundary start (asOf - 1y) → inside
            (new(2026, 3, 15), 0.20m), // inside
            (new(2026, 8, 22), 1.00m), // on asOf → inside
        };

        var total = DividendCalculators.SumLast12Months(events, AsOf);

        total.Should().Be(1.30m);
    }

    [Fact]
    public void SumLast12Months_EmptyEvents_ReturnsZero()
    {
        DividendCalculators.SumLast12Months([], AsOf).Should().Be(0m);
    }

    [Fact]
    public void YieldPercent_DividesTotalByCurrentPrice()
    {
        var yield = DividendCalculators.YieldPercent(1.50m, 10m);

        yield.Should().Be(15.00m);
    }

    [Fact]
    public void YieldPercent_NullOrNonPositivePrice_ReturnsNull()
    {
        DividendCalculators.YieldPercent(1.50m, null).Should().BeNull();
        DividendCalculators.YieldPercent(1.50m, 0m).Should().BeNull();
        DividendCalculators.YieldPercent(1.50m, -5m).Should().BeNull();
    }
}
