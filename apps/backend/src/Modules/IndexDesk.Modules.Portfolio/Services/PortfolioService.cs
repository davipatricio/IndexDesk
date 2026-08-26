using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Calculators;
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

        return await BuildSummaryAsync(owned, portfolioId, ct);
    }

    /// <summary>Resumo sem checagem de dono (uso interno: export). Ownership é do chamador.</summary>
    public async Task<Result<PortfolioSummaryDto>> GetSummaryInternalAsync(
        Guid portfolioId,
        CancellationToken ct
    )
    {
        var owned = await db.Portfolios.FirstOrDefaultAsync(p => p.Id == portfolioId, ct);
        if (owned is null)
            return Result<PortfolioSummaryDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        return await BuildSummaryAsync(owned, portfolioId, ct);
    }

    private async Task<Result<PortfolioSummaryDto>> BuildSummaryAsync(
        PortfolioEntity owned,
        Guid portfolioId,
        CancellationToken ct
    )
    {
        var transactions = await db
            .PortfolioTransactions.Where(t =>
                t.PortfolioId == portfolioId && t.ReversedByTransactionId == null
            )
            .ToListAsync(ct);

        var projection = PositionProjector.Project(transactions);
        if (projection.IsFailure)
            return Result<PortfolioSummaryDto>.Failure(projection.Error);

        var positions = await ValuePositionsAsync(portfolioId, projection.Value.Positions, ct);

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

    private readonly Dictionary<(string Code, DateOnly End), decimal> _accrualCache = [];

    /// <summary>Accrual local-first do caixa sintético com séries SGS já persistidas.</summary>
    private decimal AccrueSynthetic(
        BuildingBlocks.Persistence.Entities.PortfolioFixedIncomePositionEntity param,
        decimal quantity
    )
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end =
            param.MaturityDate < today && param.Liquidity == "maturity"
                ? param.MaturityDate
                : today;

        var cacheKey = (param.SyntheticIndexCode ?? string.Empty, end);
        if (_accrualCache.TryGetValue(cacheKey, out var cachedFactor))
            return decimal.Round(quantity * cachedFactor, 2);

        var sgsCode = param.Indexer.ToUpperInvariant() switch
        {
            "SELIC" => 11,
            "IPCA_PLUS" => 433,
            _ => 12, // CDI_PERCENT e CDI_PLUS usam CDI diária (SGS 12)
        };

        var rates = db
            .MacroEconomicSeries.Where(m =>
                m.SeriesCode == sgsCode && m.Date > param.StartDate && m.Date <= end
            )
            .OrderBy(m => m.Date)
            .Select(m => new ValueTuple<DateOnly, decimal>(m.Date, m.Value))
            .ToList();

        var result = FixedIncomeAccrualCalculator.Accrue(
            new FixedIncomeAccrualCalculator.Input(
                param.Indexer,
                param.IndexerRate,
                quantity,
                param.StartDate,
                end,
                rates
            )
        );
        _accrualCache[cacheKey] = result.Factor;
        return result.AccruedValue;
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
        Guid portfolioId,
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

        var fiRows = await db
            .PortfolioFixedIncomePositions.Where(f => f.PortfolioId == portfolioId)
            .ToListAsync(ct);
        var fiBySynthetic = fiRows
            .Where(f => f.SyntheticIndexCode != null)
            .GroupBy(f => f.SyntheticIndexCode!)
            .ToDictionary(g => g.Key, g => g.First());
        var fiByAsset = fiRows
            .Where(f => f.AssetId != null)
            .GroupBy(f => f.AssetId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<(ProjectedPosition Source, PositionDto Dto)>(projected.Count);
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
                // Caixa sintético: usa o accrual quando há parâmetros RF; sem parâmetros, custo.
                var param = fiBySynthetic.GetValueOrDefault(p.SyntheticIndexCode);
                if (param is not null)
                {
                    var accrued = AccrueSynthetic(param, p.Quantity);
                    currentPrice = p.Quantity > 0 ? decimal.Round(accrued / p.Quantity, 8) : 0m;
                    hasMarketPrice = currentPrice > 0;
                }
                else
                {
                    currentPrice = p.AveragePrice;
                    hasMarketPrice = true;
                }
            }

            var currentValue = hasMarketPrice ? p.Quantity * currentPrice : p.InvestedAmount;
            var ticker = p.AssetId is not null
                ? assets[p.AssetId.Value].Ticker
                : p.SyntheticIndexCode;
            var name = p.AssetId is not null ? assets[p.AssetId.Value].Name : "Caixa sintético";

            var dto = new PositionDto(
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
                p.IncomeReceived,
                null
            );
            rows.Add((p, dto));
        }

        // Contribuição: fatia do lucro total gerado pela posição
        // (não realizado + realizado + renda). Null quando não há lucro a dividir.
        var totalProfit = rows.Sum(r =>
            r.Dto.UnrealizedPnl + r.Dto.RealizedPnl + r.Dto.IncomeReceived
        );

        var result = rows.Select(r =>
            {
                var profit = r.Dto.UnrealizedPnl + r.Dto.RealizedPnl + r.Dto.IncomeReceived;
                decimal? contribution =
                    totalProfit > 0 ? decimal.Round(profit / totalProfit * 100m, 2) : null;
                return r.Dto with { ContributionPercent = contribution };
            })
            .OrderByDescending(p => p.CurrentValue)
            .ToList();

        return result;
    }
}
