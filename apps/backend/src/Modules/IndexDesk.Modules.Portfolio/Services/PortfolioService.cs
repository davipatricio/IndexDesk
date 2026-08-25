using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Portfolio CRUD with the free-tier limit (3 per user) and ownership enforcement
/// (cross-user access returns NotFound, never 403, to avoid leaking existence).
/// </summary>
public sealed class PortfolioService(IndexDeskDbContext db) : IPortfolioService
{
    private const int MaxPortfoliosPerUser = 3;

    private static readonly string[] ValidRiskProfiles = ["conservador", "moderado", "arrojado"];
    private static readonly string[] ValidVisibility = ["private", "public", "link"];
    private static readonly string[] ValidValuesMode = ["percent_only", "full_values"];

    public async Task<Result<PortfolioDto>> CreateAsync(
        Guid userId,
        CreatePortfolioRequest request,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<PortfolioDto>.Failure(
                Error.Validation("TitleRequired", "Informe o título da carteira.")
            );

        var count = await db.Portfolios.CountAsync(p => p.UserId == userId, ct);
        if (count >= MaxPortfoliosPerUser)
            return Result<PortfolioDto>.Failure(
                new Error(
                    "Portfolio.LimitReached",
                    $"Limite de {MaxPortfoliosPerUser} carteiras atingido."
                )
            );

        var portfolio = new PortfolioEntity
        {
            UserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            RiskProfile = request.RiskProfile.ToLowerInvariant(),
        };

        if (!ValidRiskProfiles.Contains(portfolio.RiskProfile))
            return Result<PortfolioDto>.Failure(
                Error.Validation("RiskProfileInvalid", "Perfil de risco inválido.")
            );

        db.Portfolios.Add(portfolio);
        await db.SaveChangesAsync(ct);
        return Result<PortfolioDto>.Success(ToDto(portfolio));
    }

    public async Task<Result<IReadOnlyList<PortfolioDto>>> ListAsync(
        Guid userId,
        CancellationToken ct
    )
    {
        var portfolios = await db
            .Portfolios.Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        return Result<IReadOnlyList<PortfolioDto>>.Success(portfolios.Select(ToDto).ToList());
    }

    public async Task<Result<PortfolioSummaryDto>> GetSummaryAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    )
    {
        var owned = await OwnsAsync(userId, portfolioId, ct);
        if (owned is null)
            return Result<PortfolioSummaryDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var transactions = await db
            .PortfolioTransactions.Where(t =>
                t.PortfolioId == portfolioId && t.ReversedByTransactionId == null
            )
            .ToListAsync(ct);

        var projection = PositionProjector.Project(transactions);
        if (projection.IsFailure)
            return Result<PortfolioSummaryDto>.Failure(projection.Error);

        var positions = await ValuePositionsAsync(projection.Value.Positions, ct);

        return Result<PortfolioSummaryDto>.Success(
            new PortfolioSummaryDto(
                ToDto(owned),
                positions.Sum(p => p.CurrentValue),
                positions.Sum(p => p.InvestedAmount),
                positions.Sum(p => p.UnrealizedPnl),
                positions.Sum(p => p.RealizedPnl),
                positions.Sum(p => p.IncomeReceived),
                positions
            )
        );
    }

    public async Task<Result<PortfolioDto>> UpdateAsync(
        Guid userId,
        Guid portfolioId,
        UpdatePortfolioRequest request,
        CancellationToken ct
    )
    {
        var portfolio = await OwnsAsync(userId, portfolioId, ct);
        if (portfolio is null)
            return Result<PortfolioDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return Result<PortfolioDto>.Failure(
                    Error.Validation("TitleRequired", "Título não pode ficar vazio.")
                );
            portfolio.Title = request.Title.Trim();
        }
        if (request.Description is not null)
            portfolio.Description = request.Description.Trim();
        if (request.RiskProfile is not null)
        {
            var risk = request.RiskProfile.ToLowerInvariant();
            if (!ValidRiskProfiles.Contains(risk))
                return Result<PortfolioDto>.Failure(
                    Error.Validation("RiskProfileInvalid", "Perfil de risco inválido.")
                );
            portfolio.RiskProfile = risk;
        }
        if (request.Visibility is not null)
        {
            var visibility = request.Visibility.ToLowerInvariant();
            if (!ValidVisibility.Contains(visibility))
                return Result<PortfolioDto>.Failure(
                    Error.Validation("VisibilityInvalid", "Visibilidade inválida.")
                );
            portfolio.Visibility = visibility;
        }
        if (request.PublicValuesMode is not null)
        {
            var mode = request.PublicValuesMode.ToLowerInvariant();
            if (!ValidValuesMode.Contains(mode))
                return Result<PortfolioDto>.Failure(
                    Error.Validation("ValuesModeInvalid", "Modo de valores públicos inválido.")
                );
            portfolio.PublicValuesMode = mode;
        }
        if (request.DisplayIdentity is not null)
            portfolio.DisplayIdentity = request.DisplayIdentity.Trim();

        portfolio.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<PortfolioDto>.Success(ToDto(portfolio));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid portfolioId, CancellationToken ct)
    {
        var portfolio = await OwnsAsync(userId, portfolioId, ct);
        if (portfolio is null)
            return Result.Failure(Error.NotFound("Portfolio", portfolioId.ToString()));

        db.Portfolios.Remove(portfolio);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------- helpers ----------

    private async Task<PortfolioEntity?> OwnsAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    ) =>
        await db.Portfolios.FirstOrDefaultAsync(p => p.Id == portfolioId && p.UserId == userId, ct);

    private static PortfolioDto ToDto(PortfolioEntity p) =>
        new(
            p.Id,
            p.Title,
            p.Description,
            p.RiskProfile,
            p.Visibility,
            p.PublicValuesMode,
            p.CreatedAt
        );

    /// <summary>
    /// Local-first valuation: latest close of asset_quotes; foreign-currency assets multiply by the
    /// latest fx_rates close for "{CURRENCY}-BRL". Positions without any market price keep
    /// HasMarketPrice=false and are valued at cost so totals stay meaningful.
    /// </summary>
    private async Task<List<PositionDto>> ValuePositionsAsync(
        IReadOnlyList<ProjectedPosition> projected,
        CancellationToken ct
    )
    {
        var assetIds = projected
            .Where(p => p.AssetId is not null && p.Quantity > 0)
            .Select(p => p.AssetId!.Value)
            .Distinct()
            .ToList();

        var assets = await db
            .Assets.Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var closes = new Dictionary<Guid, decimal>();
        foreach (var id in assetIds)
        {
            var last = await db
                .AssetQuotes.Where(q => q.AssetId == id)
                .OrderByDescending(q => q.Date)
                .Select(q => (decimal?)q.Close)
                .FirstOrDefaultAsync(ct);
            if (last is not null)
                closes[id] = last.Value;
        }

        var fxNeeded = assets
            .Values.Where(a => a.Currency != "BRL")
            .Select(a => $"{a.Currency}-BRL")
            .Distinct()
            .ToList();
        var fx = new Dictionary<string, decimal>();
        foreach (var pair in fxNeeded)
        {
            var rate = await db
                .FxRates.Where(f => f.Pair == pair)
                .OrderByDescending(f => f.Date)
                .Select(f => (decimal?)f.Bid)
                .FirstOrDefaultAsync(ct);
            if (rate is not null)
                fx[pair] = rate.Value;
        }

        var result = new List<PositionDto>(projected.Count);
        foreach (var p in projected)
        {
            decimal currentPrice = 0;
            var hasMarketPrice = false;

            if (p.AssetId is not null && closes.TryGetValue(p.AssetId.Value, out var close))
            {
                var currency = assets[p.AssetId.Value].Currency;
                var factor = currency == "BRL" ? 1m : fx.GetValueOrDefault($"{currency}-BRL", 0m);
                if (factor > 0)
                {
                    currentPrice = close * factor;
                    hasMarketPrice = true;
                }
            }
            else if (p.AssetId is null && p.Quantity > 0)
            {
                // Synthetic cash (CDI/Selic): accrual engine arrives in M-P3; value at cost meanwhile.
                currentPrice = p.AveragePrice;
                hasMarketPrice = true;
            }

            var currentValue = hasMarketPrice ? p.Quantity * currentPrice : p.InvestedAmount;
            var ticker = p.AssetId is not null
                ? assets[p.AssetId.Value].Ticker
                : p.SyntheticIndexCode;
            var name = p.AssetId is not null ? assets[p.AssetId.Value].Name : "Caixa sintético";

            result.Add(
                new PositionDto(
                    p.AssetId,
                    ticker,
                    name,
                    p.Broker,
                    p.Quantity,
                    p.AveragePrice,
                    p.InvestedAmount,
                    currentPrice,
                    currentValue,
                    hasMarketPrice,
                    hasMarketPrice ? currentValue - p.InvestedAmount : 0,
                    p.RealizedPnl,
                    p.IncomeReceived
                )
            );
        }

        return result.OrderByDescending(p => p.CurrentValue).ToList();
    }
}
