using FluentAssertions;
using IndexDesk.Modules.MarketData.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class PerformanceCalculatorsTests
{
    [Fact]
    public void ReturnBetween_ComputesPercentGrowth()
    {
        PerformanceCalculators.ReturnBetween(100m, 110m).Should().Be(10.0m);
    }

    [Fact]
    public void ReturnBetween_WithZeroStart_ReturnsZero()
    {
        PerformanceCalculators.ReturnBetween(0m, 110m).Should().Be(0m);
    }

    [Fact]
    public void AccumulateRateSeries_CompoundsDailyRates()
    {
        // Two daily rates of 0.05% => 1.0005 * 1.0005 - 1 = 0.100025%
        var result = PerformanceCalculators.AccumulateRateSeries(
            new List<decimal> { 0.05m, 0.05m }
        );
        result.Should().BeApproximately(0.100025m, 0.000001m);
    }

    [Fact]
    public void AccumulateRateSeries_Empty_ReturnsZero()
    {
        PerformanceCalculators.AccumulateRateSeries(new List<decimal>()).Should().Be(0m);
    }

    [Fact]
    public void TotalReturnWithDividends_IncludesCashDistributions()
    {
        // start 100, end 100 (no price move), dividends total 6 => +6%
        var result = PerformanceCalculators.TotalReturnWithDividends(
            100m,
            100m,
            new List<decimal> { 6m }
        );
        result.Should().Be(6m);
    }

    [Fact]
    public void AnnualizedReturn_OneYear_EqualsTotal()
    {
        PerformanceCalculators.AnnualizedReturn(10m, 365).Should().BeApproximately(10m, 0.1m);
    }

    [Fact]
    public void MaxDrawdown_ReturnsNegativePercent()
    {
        var result = PerformanceCalculators.MaxDrawdown(new List<decimal> { 120m, 90m, 100m });
        result.Should().Be(-25m);
    }

    [Fact]
    public void DailyReturns_ComputesConsecutivePercentChanges()
    {
        var result = PerformanceCalculators.DailyReturns(new List<decimal> { 100m, 110m, 99m });
        result.Should().HaveCount(2);
        result[0].Should().Be(10m);
        result[1].Should().BeApproximately(-10m, 0.01m);
    }

    [Fact]
    public void AnnualizedVolatility_EmptySeries_ReturnsZero()
    {
        PerformanceCalculators.AnnualizedVolatility(new List<decimal>()).Should().Be(0m);
    }
}
