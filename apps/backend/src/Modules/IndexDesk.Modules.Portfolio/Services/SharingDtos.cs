namespace IndexDesk.Modules.Portfolio.Services;

// ---------- Página pública / sharing ----------

/// <summary>Fatia de uma classe de ativos na carteira pública (% do valor total).</summary>
public sealed record PublicAllocationClassDto(string AssetClass, decimal Percent);

/// <summary>Posição pública — os campos em R$ vêm null quando o modo é percent_only.</summary>
public sealed record PublicPositionDto(
    string Ticker,
    string Name,
    decimal WeightPercent,
    decimal ReturnPercent,
    decimal? CurrentValue,
    decimal? InvestedAmount
);

/// <summary>Visão pública completa: identidade, alocação por classe, retornos e posições.</summary>
public sealed record PublicPortfolioDto(
    string Title,
    string? Description,
    string RiskProfile,
    string IdentityLabel,
    string ValuesMode,
    IReadOnlyList<PublicAllocationClassDto> AllocationPercent,
    decimal TotalReturnPercent,
    decimal? PeriodReturnPercent,
    IReadOnlyList<PublicPositionDto> Positions,
    DateTime CreatedAt
);

/// <summary>Estado da visibilidade após UpdateVisibility (nunca expõe o token claro).</summary>
public sealed record PortfolioVisibilityDto(
    Guid PortfolioId,
    string Visibility,
    string? Slug,
    bool HasShareToken,
    DateTimeOffset? ShareExpiresAt
);

/// <summary>Link gerado — o token claro aparece UMA única vez; só o hash SHA-256 é persistido.</summary>
public sealed record ShareLinkDto(
    Guid PortfolioId,
    string Slug,
    string ShareToken,
    DateTimeOffset? ShareExpiresAt
);
