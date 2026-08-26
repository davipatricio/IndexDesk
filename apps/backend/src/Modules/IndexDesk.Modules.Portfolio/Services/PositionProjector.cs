using System.Text.Json;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Calculators;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>Aggregated position for one (asset, broker) custody pair.</summary>
public sealed record ProjectedPosition(
    Guid? AssetId,
    string SyntheticIndexCode,
    string Broker,
    decimal Quantity,
    decimal AveragePrice,
    decimal RealizedPnl,
    decimal IncomeReceived
)
{
    public decimal InvestedAmount => Quantity * AveragePrice;
}

public sealed record ProjectionResult(IReadOnlyList<ProjectedPosition> Positions);

/// <summary>
/// Pure transaction-log projector. Rebuilds positions from the active (non-superseded)
/// transactions in trade-date order; deterministic and re-runnable. Market valuation is applied
/// afterwards by the caller — this class only knows quantities and cost bases.
/// </summary>
public static class PositionProjector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Result<ProjectionResult> Project(
        IReadOnlyList<PortfolioTransactionEntity> transactions
    )
    {
        // Key: asset id when present, otherwise "synthetic:{code}".
        var state = new Dictionary<string, MutablePosition>();
        var order = transactions
            .Where(t => t.ReversedByTransactionId == null) // superseded amendments never project
            .OrderBy(t => t.TradeDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id);

        foreach (var tx in order)
        {
            var key = KeyOf(tx);
            if (!state.TryGetValue(key, out var position))
            {
                position = new MutablePosition(
                    tx.AssetId,
                    tx.SyntheticIndexCode ?? string.Empty,
                    tx.Broker
                );
                state[key] = position;
            }

            switch (tx.Type)
            {
                case "BUY":
                    // Synthetic cash deposit (CDI/Selic) carries no quantity: gross amount acts as
                    // the "quantity" at unit price 1 so the average-price math stays valid.
                    var buyQuantity =
                        tx.AssetId is null && tx.Quantity is null
                            ? tx.GrossAmount
                            : tx.Quantity ?? 0;
                    position.Quantity += buyQuantity;
                    position.AveragePrice = AveragePriceCalculator.NewBuyAverage(
                        position.Quantity - buyQuantity,
                        position.AveragePrice,
                        buyQuantity,
                        tx.UnitPrice ?? 1m,
                        tx.Fees
                    );
                    break;

                case "SELL":
                    try
                    {
                        var sell = AveragePriceCalculator.Sell(
                            position.Quantity,
                            position.AveragePrice,
                            tx.Quantity ?? 0,
                            tx.UnitPrice ?? 0,
                            tx.Fees
                        );
                        position.Quantity = sell.RemainingQuantity;
                        position.AveragePrice = sell.RemainingAveragePrice;
                        position.RealizedPnl += sell.RealizedPnl;
                    }
                    catch (InvalidOperationException)
                    {
                        return Result<ProjectionResult>.Failure(
                            new Error(
                                "Portfolio.TransactionInvalid",
                                $"Venda maior que a posição disponível na data {tx.TradeDate:yyyy-MM-dd}."
                            )
                        );
                    }
                    break;

                case "INCOME":
                    position.IncomeReceived += tx.GrossAmount - tx.Fees;
                    break;

                case "CORP_ACTION":
                {
                    var qty = position.Quantity;
                    var avg = position.AveragePrice;
                    ApplyCorpAction(
                        tx.CorpActionJson,
                        tx.Quantity ?? 0,
                        tx.UnitPrice ?? 0,
                        tx.Fees,
                        ref qty,
                        ref avg
                    );
                    position.Quantity = qty;
                    position.AveragePrice = avg;
                    break;
                }

                case "TRANSFER_OUT":
                    // Cost basis leaves the portfolio without creating realized PnL.
                    var outQty = Math.Min(tx.Quantity ?? 0, position.Quantity);
                    position.Quantity -= outQty;
                    break;

                case "TRANSFER_IN":
                    position.Quantity += tx.Quantity ?? 0;
                    position.AveragePrice = AveragePriceCalculator.NewBuyAverage(
                        position.Quantity - (tx.Quantity ?? 0),
                        position.AveragePrice,
                        tx.Quantity ?? 0,
                        tx.UnitPrice ?? 0,
                        tx.Fees
                    );
                    break;

                default:
                    return Result<ProjectionResult>.Failure(
                        new Error(
                            "Portfolio.TransactionInvalid",
                            $"Tipo de transação desconhecido: {tx.Type}."
                        )
                    );
            }
        }

        var result = state
            .Values.Select(p => new ProjectedPosition(
                p.AssetId,
                p.SyntheticIndexCode,
                p.Broker,
                p.Quantity,
                p.AveragePrice,
                p.RealizedPnl,
                p.IncomeReceived
            ))
            .ToList();

        return Result<ProjectionResult>.Success(new ProjectionResult(result));
    }

    /// <summary>
    /// factor semantics (plan §2): split f:n → quantity × (n/f), average ÷ (n/f);
    /// grupamento/inpc → inverse; bonificacao pct → quantity × (1+pct); subscricao is a buy.
    /// </summary>
    public static void ApplyCorpAction(
        string? corpActionJson,
        decimal subscriptionQuantity,
        decimal subscriptionUnitPrice,
        decimal subscriptionFees,
        ref decimal quantity,
        ref decimal averagePrice
    )
    {
        CorpAction? action;
        try
        {
            action = string.IsNullOrWhiteSpace(corpActionJson)
                ? null
                : JsonSerializer.Deserialize<CorpAction>(corpActionJson, JsonOptions);
        }
        catch (JsonException)
        {
            action = null;
        }

        if (action is null || string.IsNullOrWhiteSpace(action.Kind))
            return;

        switch (action.Kind.ToLowerInvariant())
        {
            case "split":
                if (action.Factor > 0 && action.Factor != 1)
                {
                    quantity *= action.Factor;
                    averagePrice /= action.Factor;
                }
                break;
            case "grupamento":
            case "inpc":
                if (action.Factor > 0 && action.Factor != 1)
                {
                    quantity /= action.Factor;
                    averagePrice *= action.Factor;
                }
                break;
            case "bonificacao":
                var bonus = 1 + (action.Percent ?? 0);
                if (bonus > 0)
                {
                    quantity *= bonus;
                    averagePrice /= bonus;
                }
                break;
            case "subscricao":
                var newQuantity = quantity + subscriptionQuantity;
                averagePrice = AveragePriceCalculator.NewBuyAverage(
                    quantity,
                    averagePrice,
                    subscriptionQuantity,
                    subscriptionUnitPrice,
                    subscriptionFees
                );
                quantity = newQuantity;
                break;
        }
    }

    private static string KeyOf(PortfolioTransactionEntity tx) =>
        $"{(tx.AssetId is not null ? $"asset:{tx.AssetId.Value:N}" : $"synthetic:{tx.SyntheticIndexCode}")}:{tx.Broker}";

    private sealed class MutablePosition(Guid? assetId, string syntheticIndexCode, string broker)
    {
        public Guid? AssetId { get; } = assetId;
        public string SyntheticIndexCode { get; } = syntheticIndexCode;
        public string Broker { get; } = broker;
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal RealizedPnl { get; set; }
        public decimal IncomeReceived { get; set; }
    }

    internal sealed record CorpAction(string Kind, decimal Factor = 1, decimal? Percent = null);
}
