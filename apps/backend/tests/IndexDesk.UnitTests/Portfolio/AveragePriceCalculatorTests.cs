using IndexDesk.Modules.Portfolio.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.Portfolio;

public class AveragePriceCalculatorTests
{
    [Fact]
    public void First_buy_includes_fees_in_average()
    {
        var average = AveragePriceCalculator.NewBuyAverage(0, 0, 100, 10m, 5m);

        Assert.Equal(10.05m, average); // (100×10 + 5) / 100
    }

    [Fact]
    public void Second_buy_is_weighted_with_fees()
    {
        // 100 @ 10.05 depois compra 100 @ 20 sem taxas => média 15.025
        var average = AveragePriceCalculator.NewBuyAverage(100, 10.05m, 100, 20m, 0m);

        Assert.Equal(15.025m, average);
    }

    [Fact]
    public void Sell_realizes_pnl_net_of_fees_and_keeps_average()
    {
        var result = AveragePriceCalculator.Sell(
            existingQuantity: 200,
            existingAveragePrice: 15m,
            sellQuantity: 50,
            unitPrice: 20m,
            fees: 7m
        );

        Assert.Equal(((20m - 15m) * 50) - 7m, result.RealizedPnl);
        Assert.Equal(150m, result.RemainingQuantity);
        Assert.Equal(15m, result.RemainingAveragePrice);
    }

    [Fact]
    public void Sell_total_leaves_zero_position()
    {
        var result = AveragePriceCalculator.Sell(100, 12.34567891m, 100, 13m, 0m);

        Assert.Equal(0m, result.RemainingQuantity);
        Assert.Equal(100 * (13m - 12.34567891m), result.RealizedPnl);
    }

    [Fact]
    public void Sell_beyond_position_throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AveragePriceCalculator.Sell(10, 5m, 11, 6m, 0m)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_quantity_throws(decimal quantity)
    {
        Assert.Throws<ArgumentException>(() =>
            AveragePriceCalculator.NewBuyAverage(0, 0, quantity, 10m, 0m)
        );
        Assert.Throws<ArgumentException>(() =>
            AveragePriceCalculator.Sell(10, 5m, quantity, 6m, 0m)
        );
    }
}
