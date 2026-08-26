using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Calculators;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>Projeção das colunas fiscais do log de transações (sem carregar a entidade inteira).</summary>
internal sealed record TaxTxRow(
    Guid? AssetId,
    string? SyntheticIndexCode,
    string Type,
    string Broker,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal Fees,
    decimal GrossAmount,
    DateOnly TradeDate
);

/// <summary>
/// Camada fiscal educacional da carteira (M-P4). Tudo roda sobre dados locais
/// (Postgres via IndexDeskDbContext) — nenhuma chamada externa em tempo de request.
/// A matemática fica nas calculadoras puras de <c>Calculators/TaxCalculators.cs</c>;
/// este serviço só carrega transações/cotações, classifica o ativo e monta os DTOs
/// com premissas e disclaimer obrigatórios. Preço médio reaproveita o
/// <see cref="AveragePriceCalculator"/> existente (convenção BUY soma taxas, SELL não mexe no médio).
/// </summary>
public sealed class PortfolioTaxService(IndexDeskDbContext db) : IPortfolioTaxService
{
    public async Task<Result<RedemptionTaxDto>> SimulateRedemptionAsync(
        Guid userId,
        Guid portfolioId,
        Guid assetId,
        string? broker = null,
        decimal? quantity = null,
        CancellationToken ct = default
    )
    {
        // Acesso cruzado entre usuários vira NotFound por convenção (nunca 403).
        if (
            await db.Portfolios.FirstOrDefaultAsync(
                p => p.Id == portfolioId && p.UserId == userId,
                ct
            )
            is null
        )
            return Result<RedemptionTaxDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        if (quantity is <= 0)
            return Result<RedemptionTaxDto>.Failure(
                Error.Validation("QuantidadeInvalida", "Quantidade do resgate deve ser positiva.")
            );

        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null)
            return Result<RedemptionTaxDto>.Failure(Error.NotFound("Asset", assetId.ToString()));

        var transactionsQuery = db.PortfolioTransactions.Where(t =>
            t.PortfolioId == portfolioId
            && t.AssetId == assetId
            && t.ReversedByTransactionId == null
        );
        if (!string.IsNullOrWhiteSpace(broker))
            transactionsQuery = transactionsQuery.Where(t => t.Broker == broker);

        var transactions = await transactionsQuery
            .OrderBy(t => t.TradeDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

        var walk = WalkPosition(transactions);
        if (walk.IsFailure)
            return Result<RedemptionTaxDto>.Failure(walk.Error);

        var (availableQuantity, averagePrice, firstAcquisitionDate) = walk.Value;
        if (availableQuantity <= 0)
            return Result<RedemptionTaxDto>.Failure(
                Error.NotFound("Position", $"{asset.Ticker} na carteira {portfolioId}")
            );
        if (quantity > availableQuantity)
            return Result<RedemptionTaxDto>.Failure(
                Error.Validation(
                    "QuantidadeMaiorQuePosicao",
                    $"Quantidade solicitada ({quantity}) maior que a posição disponível ({availableQuantity:N8})."
                )
            );

        var quote = await db
            .AssetQuotes.Where(q => q.AssetId == assetId)
            .OrderByDescending(q => q.Date)
            .Select(q => new { q.Date, q.Close })
            .FirstOrDefaultAsync(ct);
        if (quote is null)
            return Result<RedemptionTaxDto>.Failure(
                Error.Validation(
                    "CotacaoIndisponivel",
                    $"Sem cotação local para '{asset.Ticker}' — aguarde a sincronização do Worker."
                )
            );

        var unitPrice = quote.Close;
        var fxConverted = false;
        if (!string.Equals(asset.Currency, "BRL", StringComparison.OrdinalIgnoreCase))
        {
            var pair = $"{asset.Currency.ToUpperInvariant()}-BRL";
            var fxRate = await db
                .FxRates.Where(f => f.Pair == pair)
                .OrderByDescending(f => f.Date)
                .Select(f => (decimal?)f.Bid)
                .FirstOrDefaultAsync(ct);
            if (fxRate is null || fxRate <= 0)
                return Result<RedemptionTaxDto>.Failure(
                    Error.Validation(
                        "FxIndisponivel",
                        $"Sem câmbio local '{pair}' — aguarde a sincronização do Worker."
                    )
                );
            unitPrice = decimal.Round(unitPrice * fxRate.Value, 8);
            fxConverted = true;
        }

        var assetClass = ClassifyAssetType(asset.AssetType);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysHeld = Math.Max(0, today.DayNumber - firstAcquisitionDate.DayNumber);
        var redeemedQuantity = quantity ?? availableQuantity;

        // Come-cotas considera os rendimentos (INCOME) já creditados na posição RF.
        List<(DateOnly Date, decimal Yield)>? datedYields = null;
        DateOnly? referenceDate = null;
        if (assetClass == TaxAssetClasses.RendaFixa)
        {
            datedYields = transactions
                .Where(t => t.Type == "INCOME")
                .Select(t => (t.TradeDate, decimal.Round(t.GrossAmount - t.Fees, 2)))
                .ToList();
            referenceDate = today;
        }

        // Venda ainda não aconteceu: simulamos sem taxa de venda (taxas de compra já estão no médio).
        var breakdown = TaxCalculators.SimulateRedemption(
            new RedemptionInput(
                assetClass,
                redeemedQuantity,
                availableQuantity,
                averagePrice,
                unitPrice,
                Fees: 0m,
                daysHeld,
                referenceDate,
                datedYields
            )
        );

        var dto = new RedemptionTaxDto(
            portfolioId,
            asset.Id,
            null,
            broker?.Trim() ?? "Todas",
            asset.Ticker,
            assetClass,
            availableQuantity,
            redeemedQuantity,
            unitPrice,
            quote.Date,
            fxConverted,
            breakdown.GrossAmount,
            breakdown.CostBasis,
            breakdown.Profit,
            breakdown.IrPercent,
            breakdown.IrAmount,
            breakdown.IofAmount,
            breakdown.ComeCotasAlreadyPaid,
            breakdown.NetAmount,
            breakdown.ExemptApplied,
            breakdown.ExemptReason,
            breakdown.Premises,
            TaxCalculators.Disclaimer
        );
        return Result<RedemptionTaxDto>.Success(dto);
    }

    public async Task<Result<DarfProjectionDto>> GetProjectionAsync(
        Guid userId,
        Guid portfolioId,
        int year,
        int month,
        CancellationToken ct = default
    )
    {
        if (
            await db.Portfolios.FirstOrDefaultAsync(
                p => p.Id == portfolioId && p.UserId == userId,
                ct
            )
            is null
        )
            return Result<DarfProjectionDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        if (month is < 1 or > 12)
            return Result<DarfProjectionDto>.Failure(
                Error.Validation("MesInvalido", "Mês deve estar entre 1 e 12.")
            );
        if (year is < 1900 or > 2200)
            return Result<DarfProjectionDto>.Failure(
                Error.Validation("AnoInvalido", "Ano fora do intervalo suportado (1900–2200).")
            );

        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        // Log ativo (linhas superseded nunca projetam) até o fim do mês-alvo, em ordem de pregão.
        var rows = await db
            .PortfolioTransactions.Where(t =>
                t.PortfolioId == portfolioId
                && t.ReversedByTransactionId == null
                && t.TradeDate <= lastDay
                && (
                    t.Type == "BUY"
                    || t.Type == "SELL"
                    || t.Type == "TRANSFER_IN"
                    || t.Type == "TRANSFER_OUT"
                )
            )
            .OrderBy(t => t.TradeDate)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => new TaxTxRow(
                t.AssetId,
                t.SyntheticIndexCode,
                t.Type,
                t.Broker,
                t.Quantity,
                t.UnitPrice,
                t.Fees,
                t.GrossAmount,
                t.TradeDate
            ))
            .ToListAsync(ct);

        var assetIds = rows.Where(r => r.AssetId is not null)
            .Select(r => r.AssetId!.Value)
            .Distinct()
            .ToList();
        var assetTypesById = await db
            .Assets.Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.AssetType, ct);

        var inputs = new List<DarfPositionInput>();
        var skippedSells = 0;

        foreach (
            var positionRows in rows.GroupBy(r =>
                $"{(r.AssetId is not null ? $"asset:{r.AssetId.Value:N}" : $"synthetic:{r.SyntheticIndexCode}")}:{r.Broker}"
            )
        )
        {
            var assetClass = ClassifyPosition(positionRows.First(), assetTypesById);
            var quantity = 0m;
            var averagePrice = 0m;
            var firstAcquisition = (DateOnly?)null;

            foreach (var row in positionRows)
            {
                switch (row.Type)
                {
                    case "BUY":
                    case "TRANSFER_IN":
                        var inQuantity = EffectiveQuantity(row);
                        averagePrice = AveragePriceCalculator.NewBuyAverage(
                            quantity,
                            averagePrice,
                            inQuantity,
                            row.UnitPrice ?? 1m,
                            row.Fees
                        );
                        quantity += inQuantity;
                        firstAcquisition ??= row.TradeDate;
                        break;

                    case "TRANSFER_OUT":
                        quantity -= Math.Min(EffectiveQuantity(row), quantity);
                        break;

                    case "SELL":
                        var outQuantity = EffectiveQuantity(row);
                        try
                        {
                            var sell = AveragePriceCalculator.Sell(
                                quantity,
                                averagePrice,
                                outQuantity,
                                row.UnitPrice ?? 0m,
                                row.Fees
                            );
                            quantity = sell.RemainingQuantity;
                        }
                        catch (InvalidOperationException)
                        {
                            // Histórico inconsistente: lança fora da projeção educacional (premissa registrada).
                            skippedSells++;
                            continue;
                        }

                        if (row.TradeDate >= firstDay)
                        {
                            var unitPrice = row.UnitPrice ?? 0m;
                            inputs.Add(
                                new DarfPositionInput(
                                    assetClass,
                                    Round2(
                                        unitPrice * outQuantity
                                            - row.Fees
                                            - averagePrice * outQuantity
                                    ),
                                    Round2(unitPrice * outQuantity),
                                    Math.Max(
                                        0,
                                        row.TradeDate.DayNumber
                                            - (firstAcquisition ?? row.TradeDate).DayNumber
                                    )
                                )
                            );
                        }
                        break;
                }
            }
        }

        var projection = TaxCalculators.ProjectDarf(year, month, inputs);
        var premises = projection.Premises.ToList();
        if (inputs.Count == 0)
            premises.Add("Nenhuma venda/resgate realizado no mês — nada a recolher.");
        if (skippedSells > 0)
            premises.Add(
                $"{skippedSells} venda(s) com histórico inconsistente (maior que a posição na data) foram ignoradas."
            );

        var dto = new DarfProjectionDto(
            portfolioId,
            projection.Year,
            projection.Month,
            projection
                .Items.Select(i => new DarfProjectionItemDto(
                    i.AssetClass,
                    i.RealizedPnl,
                    i.TaxDue,
                    i.DarfCode,
                    i.DueDate
                ))
                .ToList(),
            premises,
            TaxCalculators.Disclaimer
        );
        return Result<DarfProjectionDto>.Success(dto);
    }

    // ---------- helpers ----------

    /// <summary>Sintéticos sem quantidade usam o valor bruto como "quantidade" (convenção do PositionProjector).</summary>
    private static decimal EffectiveQuantity(TaxTxRow row) =>
        row.AssetId is null && row.Quantity is null ? row.GrossAmount : row.Quantity ?? 0m;

    /// <summary>Reconstrói quantidade/preço médio da posição com o calculador existente.</summary>
    private static Result<(
        decimal Quantity,
        decimal AveragePrice,
        DateOnly FirstAcquisition
    )> WalkPosition(IReadOnlyList<PortfolioTransactionEntity> transactions)
    {
        var quantity = 0m;
        var averagePrice = 0m;
        var firstAcquisition = (DateOnly?)null;

        foreach (var tx in transactions)
        {
            switch (tx.Type)
            {
                case "BUY":
                case "TRANSFER_IN":
                    var inQuantity =
                        tx.AssetId is null && tx.Quantity is null
                            ? tx.GrossAmount
                            : tx.Quantity ?? 0m;
                    averagePrice = AveragePriceCalculator.NewBuyAverage(
                        quantity,
                        averagePrice,
                        inQuantity,
                        tx.UnitPrice ?? 1m,
                        tx.Fees
                    );
                    quantity += inQuantity;
                    if (tx.Type == "BUY")
                        firstAcquisition ??= tx.TradeDate;
                    break;

                case "SELL":
                    try
                    {
                        var sell = AveragePriceCalculator.Sell(
                            quantity,
                            averagePrice,
                            tx.Quantity ?? 0m,
                            tx.UnitPrice ?? 0m,
                            tx.Fees
                        );
                        quantity = sell.RemainingQuantity;
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Result<(
                            decimal Quantity,
                            decimal AveragePrice,
                            DateOnly FirstAcquisition
                        )>.Failure(new Error("Portfolio.TransactionInvalid", ex.Message));
                    }
                    break;

                case "TRANSFER_OUT":
                    quantity -= Math.Min(tx.Quantity ?? 0m, quantity);
                    break;

                default:
                    break; // INCOME/CORP_ACTION não alteram preço médio nesta camada fiscal
            }
        }

        var acquisition =
            firstAcquisition
            ?? transactions
                .OrderBy(t => t.TradeDate)
                .Select(t => (DateOnly?)t.TradeDate)
                .FirstOrDefault()
            ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Result<(decimal Quantity, decimal AveragePrice, DateOnly FirstAcquisition)>.Success(
            (quantity, averagePrice, acquisition)
        );
    }

    private static string ClassifyAssetType(string assetType) =>
        assetType.ToUpperInvariant() switch
        {
            "STOCK" => TaxAssetClasses.Stock,
            "ETF" => TaxAssetClasses.Etf,
            "BDR_ETF" => TaxAssetClasses.BdrEtf,
            "FII" => TaxAssetClasses.Fii,
            _ => TaxAssetClasses.Index, // INDEX e tipos futuros: renda variável (premissa registrada)
        };

    private static string ClassifyPosition(TaxTxRow row, Dictionary<Guid, string> assetTypesById)
    {
        if (row.AssetId is not null)
            return assetTypesById.TryGetValue(row.AssetId.Value, out var assetType)
                ? ClassifyAssetType(assetType)
                : TaxAssetClasses.Index;

        return (row.SyntheticIndexCode ?? string.Empty).ToUpperInvariant() switch
        {
            "LCI" or "LCA" or "CRI" or "CRA" => TaxAssetClasses.RendaFixaIsenta,
            _ => TaxAssetClasses.RendaFixa, // CDI/SELIC e códigos livres seguem a tabela regressiva
        };
    }

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
