namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>Corpo de requisição da simulação de resgate/venda (quantity null = posição total).</summary>
public sealed record SimulateRedemptionRequest(Guid AssetId, string? Broker, decimal? Quantity);

/// <summary>
/// Resultado educacional da simulação de resgate com o breakdown fiscal completo
/// (IR por prazo ou swing 15%, IOF, come-cotas abatido e isenções aplicáveis).
/// </summary>
public sealed record RedemptionTaxDto(
    Guid PortfolioId,
    Guid? AssetId,
    string? SyntheticIndexCode,
    string Broker,
    string AssetLabel,
    string AssetClass,
    decimal QuantityAvailable,
    decimal QuantityRedeemed,
    decimal UnitPrice,
    DateOnly PriceDate,
    bool FxConverted,
    decimal GrossAmount,
    decimal CostBasis,
    decimal Profit,
    decimal IrPercent,
    decimal IrAmount,
    decimal IofAmount,
    decimal ComeCotasAlreadyPaid,
    decimal NetAmount,
    bool ExemptApplied,
    string? ExemptReason,
    IReadOnlyList<string> Premises,
    string Disclaimer
);

/// <summary>Total fiscal de uma classe de ativo no mês da projeção.</summary>
public sealed record DarfProjectionItemDto(
    string AssetClass,
    decimal RealizedPnl,
    decimal TaxDue,
    string DarfCode,
    DateOnly DueDate
);

/// <summary>
/// Projeção mensal de DARF por classe de ativo (isenções PF aplicadas), com premissas
/// e disclaimer educacional. Código "6015" quando há DARF de renda variável a recolher.
/// </summary>
public sealed record DarfProjectionDto(
    Guid PortfolioId,
    int Year,
    int Month,
    IReadOnlyList<DarfProjectionItemDto> Items,
    IReadOnlyList<string> Premises,
    string Disclaimer
);
