using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Services;
using Xunit;

namespace IndexDesk.UnitTests.Portfolio;

public class PositionProjectorTests
{
    private static readonly Guid PortfolioId = Guid.NewGuid();
    private static readonly Guid AssetA = Guid.NewGuid();
    private static readonly Guid AssetB = Guid.NewGuid();

    private static PortfolioTransactionEntity Tx(
        string type,
        Guid? assetId,
        decimal? quantity = null,
        decimal? unitPrice = null,
        decimal grossAmount = 0,
        decimal fees = 0,
        DateOnly? date = null,
        string broker = "XP",
        string? corpActionJson = null,
        string? synthetic = null
    ) =>
        new()
        {
            PortfolioId = PortfolioId,
            AssetId = assetId,
            SyntheticIndexCode = synthetic,
            Type = type,
            Broker = broker,
            Quantity = quantity,
            UnitPrice = unitPrice,
            GrossAmount = grossAmount,
            Fees = fees,
            TradeDate = date ?? new DateOnly(2026, 1, 1),
            CorpActionJson = corpActionJson,
            CreatedAt = DateTime.UtcNow,
        };

    [Fact]
    public void Buy_creates_position_with_fees_in_average()
    {
        var result = PositionProjector.Project([Tx("BUY", AssetA, 100, 10m, fees: 5m)]);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value!.Positions);
        Assert.Equal(AssetA, position.AssetId);
        Assert.Equal(100m, position.Quantity);
        Assert.Equal(10.05m, position.AveragePrice);
        Assert.Equal(1005m, position.InvestedAmount);
        Assert.Equal("XP", position.Broker);
    }

    [Fact]
    public void Retroactive_transactions_project_in_trade_date_order()
    {
        var result = PositionProjector.Project([
            // Lista fora de ordem de data: venda em janeiro, compra em dezembro do ano anterior.
            Tx("SELL", AssetA, 40, 12m, date: new DateOnly(2026, 1, 10)),
            Tx("BUY", AssetA, 100, 10m, date: new DateOnly(2025, 12, 1)),
        ]);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value!.Positions);
        Assert.Equal(60m, position.Quantity);
        Assert.Equal(10m, position.AveragePrice);
        Assert.Equal((12m - 10m) * 40m, position.RealizedPnl);
    }

    [Fact]
    public void Sell_beyond_position_fails_with_clear_error()
    {
        var result = PositionProjector.Project([
            Tx("SELL", AssetA, 10, 9m),
            Tx("BUY", AssetA, 5, 8m),
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal("Portfolio.TransactionInvalid", result.Error.Code);
    }

    [Fact]
    public void Income_accumulates_net_of_fees_per_custody()
    {
        var result = PositionProjector.Project([
            Tx("INCOME", AssetA, grossAmount: 100m, fees: 2m),
            Tx("INCOME", AssetA, grossAmount: 50m, broker: "Nu"),
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Positions.Count);
        var xp = result.Value.Positions.Single(p => p.Broker == "XP");
        Assert.Equal(98m, xp.IncomeReceived);
        Assert.Equal(50m, result.Value.Positions.Single(p => p.Broker == "Nu").IncomeReceived);
    }

    [Theory]
    [InlineData("split", "2", null, 200, 5)]
    [InlineData("grupamento", "2", null, 50, 20)]
    public void Split_and_grupamento_adjust_quantity_and_average(
        string kind,
        string factor,
        string? percent,
        decimal expectedQuantity,
        decimal expectedAverage
    )
    {
        var json =
            $"{{\"kind\":\"{kind}\",\"factor\":{factor}{(percent is null ? "" : $",\"percent\":{percent}")}}}";

        var result = PositionProjector.Project([
            Tx("BUY", AssetA, 100, 10m),
            Tx("CORP_ACTION", AssetA, corpActionJson: json, date: new DateOnly(2026, 2, 1)),
        ]);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value!.Positions);
        Assert.Equal(expectedQuantity, position.Quantity);
        Assert.Equal(expectedAverage, position.AveragePrice);
    }

    [Fact]
    public void Bonificacao_dilutes_average_by_percent()
    {
        var result = PositionProjector.Project([
            Tx("BUY", AssetB, 100, 10m),
            Tx("CORP_ACTION", AssetB, corpActionJson: "{\"kind\":\"bonificacao\",\"percent\":0.1}"),
        ]);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value!.Positions);
        Assert.Equal(110m, position.Quantity);
        Assert.Equal(1000m / 110m, position.AveragePrice);
    }

    [Fact]
    public void Subscricao_mixes_cost_like_a_buy()
    {
        var result = PositionProjector.Project([
            Tx("BUY", AssetB, 100, 10m),
            Tx(
                "CORP_ACTION",
                AssetB,
                quantity: 50,
                unitPrice: 4m,
                corpActionJson: "{\"kind\":\"subscricao\"}"
            ),
        ]);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value!.Positions);
        Assert.Equal(150m, position.Quantity);
        Assert.Equal((1000m + 200m) / 150m, position.AveragePrice);
    }

    [Fact]
    public void Transfers_move_cost_without_fake_gains()
    {
        var outResult = PositionProjector.Project([
            Tx("BUY", AssetA, 100, 10m, broker: "XP"),
            Tx("TRANSFER_OUT", AssetA, 40, 10m, broker: "XP"),
        ]);
        var inResult = PositionProjector.Project([
            Tx("TRANSFER_IN", AssetA, 40, 10m, broker: "Nu"),
        ]);

        Assert.True(outResult.IsSuccess);
        var xpPosition = Assert.Single(outResult.Value!.Positions);
        Assert.Equal(60m, xpPosition.Quantity);
        Assert.Equal(600m, xpPosition.InvestedAmount);
        Assert.Equal(0m, xpPosition.RealizedPnl); // transferência não gera ganho falso

        Assert.True(inResult.IsSuccess);
        var nuPosition = Assert.Single(inResult.Value!.Positions);
        Assert.Equal(400m, nuPosition.InvestedAmount); // 40 × R$10
    }

    [Fact]
    public void Superseded_amendments_are_ignored()
    {
        var original = Tx("BUY", AssetA, 100, 10m);
        var amended = Tx("BUY", AssetA, 80, 11m);
        amended.IsAmendment = true;
        amended.AmendedTransactionId = original.Id;
        original.ReversedByTransactionId = amended.Id;

        var result = PositionProjector.Project([original, amended]);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value!.Positions);
        Assert.Equal(80m, position.Quantity);
        Assert.Equal(11m, position.AveragePrice);
    }

    [Fact]
    public void Same_asset_across_brokers_stays_grouped_by_custody()
    {
        var result = PositionProjector.Project([
            Tx("BUY", AssetA, 10, 10m, broker: "XP"),
            Tx("BUY", AssetA, 5, 12m, broker: "Nu"),
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Positions.Count);
        Assert.All(result.Value.Positions, p => Assert.True(p.Quantity is 10m or 5m));
    }

    [Fact]
    public void Synthetic_cash_keeps_gross_amount_as_value()
    {
        var result = PositionProjector.Project([
            Tx("BUY", null, synthetic: "CDI", grossAmount: 5000m),
        ]);

        Assert.True(result.IsSuccess);
        var position = Assert.Single(result.Value!.Positions);
        Assert.Null(position.AssetId);
        Assert.Equal("CDI", position.SyntheticIndexCode);
        Assert.Equal(5000m, position.InvestedAmount);
    }
}
