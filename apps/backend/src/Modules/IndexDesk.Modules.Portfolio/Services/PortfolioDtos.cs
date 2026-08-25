namespace IndexDesk.Modules.Portfolio.Services;

// ---------- Responses ----------

public sealed record PortfolioDto(
    Guid Id,
    string Title,
    string? Description,
    string RiskProfile,
    string Visibility,
    string PublicValuesMode,
    DateTime CreatedAt
);

public sealed record PositionDto(
    Guid? AssetId,
    string Ticker,
    string Name,
    string Broker,
    decimal Quantity,
    decimal AveragePrice,
    decimal InvestedAmount,
    decimal CurrentPrice,
    decimal CurrentValue,
    bool HasMarketPrice,
    decimal UnrealizedPnl,
    decimal RealizedPnl,
    decimal IncomeReceived
);

public sealed record PortfolioSummaryDto(
    PortfolioDto Portfolio,
    decimal TotalValue,
    decimal TotalInvested,
    decimal UnrealizedPnl,
    decimal RealizedPnl,
    decimal IncomeReceived,
    IReadOnlyList<PositionDto> Positions
);

public sealed record TransactionDto(
    Guid Id,
    Guid PortfolioId,
    Guid? AssetId,
    string? SyntheticIndexCode,
    string Type,
    string Broker,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal GrossAmount,
    decimal Fees,
    decimal? FxRate,
    string Currency,
    DateOnly TradeDate,
    DateOnly? MaturityDate,
    string? CorpActionJson,
    string? Notes,
    bool IsAmendment
);

public sealed record PagedTransactionsDto(
    IReadOnlyList<TransactionDto> Items,
    int Page,
    int PageSize,
    int TotalCount
);

// ---------- Requests ----------

public sealed record CreatePortfolioRequest(
    string Title,
    string? Description = null,
    string RiskProfile = "moderado"
);

public sealed record UpdatePortfolioRequest(
    string? Title = null,
    string? Description = null,
    string? RiskProfile = null,
    string? Visibility = null,
    string? PublicValuesMode = null,
    string? DisplayIdentity = null
);

public sealed record CreateTransactionRequest(
    string Type,
    Guid? AssetId,
    string? SyntheticIndexCode,
    string Broker,
    decimal GrossAmount,
    decimal Fees = 0,
    decimal? Quantity = null,
    decimal? UnitPrice = null,
    string Currency = "BRL",
    DateOnly? TradeDate = null,
    DateOnly? MaturityDate = null,
    string? CorpActionJson = null,
    string? Notes = null,
    Guid? TargetPortfolioId = null
);
