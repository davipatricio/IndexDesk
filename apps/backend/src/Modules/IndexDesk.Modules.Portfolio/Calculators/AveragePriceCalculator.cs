namespace IndexDesk.Modules.Portfolio.Calculators;

/// <summary>
/// Pure average-price (preço médio) math, Brazil tax convention: BUY fees join the cost basis,
/// SELL reduces quantity without touching the average.
/// </summary>
public static class AveragePriceCalculator
{
    /// <summary>New weighted average after a buy; fees increase the cost basis.</summary>
    public static decimal NewBuyAverage(
        decimal existingQuantity,
        decimal existingAveragePrice,
        decimal buyQuantity,
        decimal unitPrice,
        decimal fees
    )
    {
        if (buyQuantity <= 0)
            throw new ArgumentException(
                "Quantidade de compra deve ser positiva.",
                nameof(buyQuantity)
            );

        var totalQuantity = existingQuantity + buyQuantity;
        var totalCost = existingQuantity * existingAveragePrice + buyQuantity * unitPrice + fees;
        return totalQuantity == 0 ? 0 : decimal.Round(totalCost / totalQuantity, 8);
    }

    /// <summary>
    /// Sell at the current average. Returns realized PnL net of sell fees and the remaining
    /// quantity/average pair (average never changes on a sell).
    /// </summary>
    public static SellResult Sell(
        decimal existingQuantity,
        decimal existingAveragePrice,
        decimal sellQuantity,
        decimal unitPrice,
        decimal fees
    )
    {
        if (sellQuantity <= 0)
            throw new ArgumentException(
                "Quantidade de venda deve ser positiva.",
                nameof(sellQuantity)
            );
        if (sellQuantity > existingQuantity)
            throw new InvalidOperationException("Venda maior que a posição disponível.");

        var realizedPnl = ((unitPrice - existingAveragePrice) * sellQuantity) - fees;
        return new SellResult(
            decimal.Round(realizedPnl, 8),
            existingQuantity - sellQuantity,
            existingAveragePrice
        );
    }
}

public sealed record SellResult(
    decimal RealizedPnl,
    decimal RemainingQuantity,
    decimal RemainingAveragePrice
);
