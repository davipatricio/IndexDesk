using FluentAssertions;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.Analytics.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.Calculators;

public class FinancialCalculatorsTests
{
    [Theory]
    [InlineData(12.0, 4.0, 7.6923)]
    [InlineData(10.0, 5.0, 4.7619)]
    [InlineData(15.0, 3.0, 11.6505)]
    [InlineData(6.0, 6.0, 0.0)]
    public void CalculateRealYield_ShouldReturnExpectedFisherYield(
        decimal nominal,
        decimal inflation,
        decimal expectedRealYield
    )
    {
        // Act
        var result = FinancialCalculators.CalculateRealYield(nominal, inflation);

        // Assert
        result.Should().Be(expectedRealYield);
    }

    [Fact]
    public void CalculateMaxDrawdown_ShouldReturnCorrectPercentage()
    {
        // Arrange
        var equityCurve = new List<decimal> { 100m, 120m, 110m, 90m, 130m, 125m };

        // Peak was 120, lowest after peak was 90 => drawdown = (90-120)/120 = -30/120 = -25%
        // Act
        var maxDrawdown = FinancialCalculators.CalculateMaxDrawdown(equityCurve);

        // Assert
        maxDrawdown.Should().Be(-25.00m);
    }

    [Fact]
    public void CalculateSharpeRatio_ShouldReturnExpectedMetric()
    {
        // Arrange: return 14%, risk-free 10%, volatility 8% => (14 - 10) / 8 = 0.50
        // Act
        var sharpe = FinancialCalculators.CalculateSharpeRatio(14m, 10m, 8m);

        // Assert
        sharpe.Should().Be(0.50m);
    }

    [Fact]
    public void Result_Success_ShouldHaveNoErrors()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void ResultT_Failure_ShouldReturnError()
    {
        // Arrange
        var error = Error.NotFound("Asset", "IVVB11");

        // Act
        var result = Result<string>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Asset.NotFound");
    }
}
