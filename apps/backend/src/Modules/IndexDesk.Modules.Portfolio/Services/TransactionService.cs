using System.Text.Json;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Transaction creation/listing with type/quantity validation and logical amendments
/// (original row stays; the new version supersedes it — plan §7.1).
/// </summary>
public sealed class TransactionService(IndexDeskDbContext db) : ITransactionService
{
    private static readonly string[] ValidTypes =
    [
        "BUY",
        "SELL",
        "INCOME",
        "CORP_ACTION",
        "TRANSFER_IN",
        "TRANSFER_OUT",
    ];

    private static readonly string[] ValidCorpActionKinds =
    [
        "split",
        "grupamento",
        "inpc",
        "bonificacao",
        "subscricao",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<TransactionDto>> CreateAsync(
        Guid userId,
        Guid portfolioId,
        CreateTransactionRequest request,
        CancellationToken ct
    )
    {
        var portfolio = await OwnsAsync(userId, portfolioId, ct);
        if (portfolio is null)
            return Result<TransactionDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var validation = Validate(request);
        if (validation.IsFailure)
            return Result<TransactionDto>.Failure(validation.Error);

        var entity = ToEntity(portfolioId, request);
        db.PortfolioTransactions.Add(entity);
        await db.SaveChangesAsync(ct);
        return Result<TransactionDto>.Success(ToDto(entity));
    }

    public async Task<Result<PagedTransactionsDto>> ListAsync(
        Guid userId,
        Guid portfolioId,
        int page,
        int pageSize,
        CancellationToken ct
    )
    {
        if (await OwnsAsync(userId, portfolioId, ct) is null)
            return Result<PagedTransactionsDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.PortfolioTransactions.Where(t => t.PortfolioId == portfolioId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.TradeDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result<PagedTransactionsDto>.Success(
            new PagedTransactionsDto(items.Select(ToDto).ToList(), page, pageSize, total)
        );
    }

    public async Task<Result<TransactionDto>> AmendAsync(
        Guid userId,
        Guid portfolioId,
        Guid transactionId,
        CreateTransactionRequest request,
        CancellationToken ct
    )
    {
        if (await OwnsAsync(userId, portfolioId, ct) is null)
            return Result<TransactionDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var original = await db.PortfolioTransactions.FirstOrDefaultAsync(
            t => t.Id == transactionId && t.PortfolioId == portfolioId,
            ct
        );
        if (original is null || original.ReversedByTransactionId is not null)
            return Result<TransactionDto>.Failure(
                Error.NotFound("Transaction", transactionId.ToString())
            );

        var validation = Validate(request);
        if (validation.IsFailure)
            return Result<TransactionDto>.Failure(validation.Error);

        var amended = ToEntity(portfolioId, request);
        amended.IsAmendment = true;
        amended.AmendedTransactionId = original.Id;

        original.ReversedByTransactionId = amended.Id;

        db.PortfolioTransactions.Add(amended);
        await db.SaveChangesAsync(ct);
        return Result<TransactionDto>.Success(ToDto(amended));
    }

    // ---------- helpers ----------

    private async Task<PortfolioEntity?> OwnsAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    ) =>
        await db.Portfolios.FirstOrDefaultAsync(p => p.Id == portfolioId && p.UserId == userId, ct);

    private static Result Validate(CreateTransactionRequest request)
    {
        if (!ValidTypes.Contains(request.Type))
            return Result.Failure(
                Error.Validation("TypeInvalid", $"Tipo inválido: {request.Type}.")
            );

        if (request.AssetId is null && string.IsNullOrWhiteSpace(request.SyntheticIndexCode))
            return Result.Failure(
                Error.Validation("TargetRequired", "Informe o ativo ou o índice sintético.")
            );

        if (string.IsNullOrWhiteSpace(request.Broker))
            return Result.Failure(Error.Validation("BrokerRequired", "Informe a corretora."));

        switch (request.Type)
        {
            case "BUY":
            case "SELL":
            case "TRANSFER_IN":
            case "TRANSFER_OUT":
                if ((request.Quantity ?? 0) <= 0)
                {
                    // Depósito/transferência de caixa sintético pode vir só pelo valor bruto
                    // (convenção do PositionProjector: quantidade = bruto @ unitário 1).
                    var syntheticDeposit =
                        request.AssetId is null
                        && request.Type is "BUY" or "TRANSFER_IN"
                        && request.GrossAmount > 0;
                    if (!syntheticDeposit)
                        return Result.Failure(
                            Error.Validation("QuantityRequired", "Quantidade deve ser positiva.")
                        );
                }
                if ((request.UnitPrice ?? 0) <= 0 && request.Type != "TRANSFER_IN")
                    return Result.Failure(
                        Error.Validation("UnitPriceRequired", "Valor unitário deve ser positivo.")
                    );
                break;

            case "INCOME":
                if (request.GrossAmount <= 0)
                    return Result.Failure(
                        Error.Validation("GrossRequired", "Valor do rendimento deve ser positivo.")
                    );
                break;

            case "CORP_ACTION":
                if (!IsValidCorpAction(request))
                    return Result.Failure(
                        Error.Validation(
                            "CorpActionInvalid",
                            "Evento corporativo precisa de kind válido (split, grupamento, inpc, bonificacao, subscricao)."
                        )
                    );
                break;
        }

        return Result.Success();
    }

    private static bool IsValidCorpAction(CreateTransactionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CorpActionJson))
            return false;
        try
        {
            var action = JsonSerializer.Deserialize<CorpActionPayload>(
                request.CorpActionJson,
                JsonOptions
            );
            if (action?.Kind is null)
                return false;
            if (!ValidCorpActionKinds.Contains(action.Kind.ToLowerInvariant()))
                return false;
            // Subscription behaves like a buy: needs quantity + price.
            if (action.Kind.Equals("subscricao", StringComparison.OrdinalIgnoreCase))
                return (request.Quantity ?? 0) > 0 && (request.UnitPrice ?? 0) > 0;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static PortfolioTransactionEntity ToEntity(Guid portfolioId, CreateTransactionRequest r)
    {
        decimal gross = r.Type switch
        {
            // Fluxos com quantidade derivam o bruto de quantity × unit quando não informado
            // (série usa GrossAmount como fluxo externo — SELL/TRANSFER_OUT zerado distorceria TWR/MWR).
            "BUY" or "SELL" or "TRANSFER_IN" or "TRANSFER_OUT" => r.GrossAmount > 0
                ? r.GrossAmount
                : (r.Quantity ?? 0) * (r.UnitPrice ?? 0),
            _ => r.GrossAmount,
        };

        return new PortfolioTransactionEntity
        {
            PortfolioId = portfolioId,
            AssetId = r.AssetId,
            SyntheticIndexCode = r.SyntheticIndexCode,
            Type = r.Type,
            Broker = r.Broker.Trim(),
            Quantity = r.Quantity,
            UnitPrice = r.UnitPrice,
            GrossAmount = gross,
            Fees = r.Fees,
            Currency = r.Currency,
            TradeDate = r.TradeDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            MaturityDate = r.MaturityDate,
            CorpActionJson = r.CorpActionJson,
            Notes = r.Notes,
        };
    }

    private static TransactionDto ToDto(PortfolioTransactionEntity t) =>
        new(
            t.Id,
            t.PortfolioId,
            t.AssetId,
            t.SyntheticIndexCode,
            t.Type,
            t.Broker,
            t.Quantity,
            t.UnitPrice,
            t.GrossAmount,
            t.Fees,
            t.FxRate,
            t.Currency,
            t.TradeDate,
            t.MaturityDate,
            t.CorpActionJson,
            t.Notes,
            t.IsAmendment
        );

    internal sealed record CorpActionPayload(
        string Kind,
        decimal Factor = 1,
        decimal? Percent = null
    );
}
