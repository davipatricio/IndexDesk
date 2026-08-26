using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Calculators;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Owner-controlled sharing. Tokens are 32 random bytes base64url; only the SHA-256 hex hash is
/// persisted (ShareTokenHash + ShareExpiresAt), so the clear value leaves the API exactly once.
/// Public valuation mirrors PortfolioService but synthetic cash stays at cost — the daily RF
/// accrual precision is not needed on the public page. Cross-user access returns NotFound.
/// </summary>
public sealed class PortfolioSharingService(IndexDeskDbContext db) : IPortfolioSharingService
{
    private const int MaxPortfoliosPerUser = 3;
    private const string AnonymousIdentity = "Investidor X";
    private const int MaxSlugLength = 60;

    // ---------- dono ----------

    public async Task<Result<PortfolioVisibilityDto>> UpdateVisibilityAsync(
        Guid userId,
        Guid portfolioId,
        string visibility,
        int? expiresInDays,
        CancellationToken ct
    )
    {
        var portfolio = await OwnsAsync(userId, portfolioId, ct);
        if (portfolio is null)
            return Result<PortfolioVisibilityDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var mode = (visibility ?? string.Empty).Trim().ToLowerInvariant();
        if (mode is not ("private" or "public" or "link"))
            return Result<PortfolioVisibilityDto>.Failure(
                Error.Validation(
                    "VisibilityInvalid",
                    "Visibilidade inválida. Use private, public ou link."
                )
            );
        if (expiresInDays is < 1)
            return Result<PortfolioVisibilityDto>.Failure(
                Error.Validation("ExpiryInvalid", "Expiração do link deve ser de pelo menos 1 dia.")
            );

        portfolio.Visibility = mode;
        switch (mode)
        {
            case "private":
                // Revoga tudo: sem página pública e sem link restrito.
                portfolio.Slug = null;
                portfolio.ShareTokenHash = null;
                portfolio.ShareExpiresAt = null;
                break;

            case "public":
                // Página aberta não usa token; garante apenas o slug.
                portfolio.ShareTokenHash = null;
                portfolio.ShareExpiresAt = null;
                portfolio.Slug ??= BuildSlug(portfolio.Title, portfolio.Id);
                break;

            case "link":
                portfolio.Slug ??= BuildSlug(portfolio.Title, portfolio.Id);
                if (portfolio.ShareTokenHash is null)
                    AssignShareToken(portfolio); // claro descartado; Regenerate devolve o valor
                if (expiresInDays.HasValue)
                    portfolio.ShareExpiresAt = DateTime.UtcNow.AddDays(expiresInDays.Value);
                break;
        }

        portfolio.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<PortfolioVisibilityDto>.Success(
            new PortfolioVisibilityDto(
                portfolio.Id,
                portfolio.Visibility,
                portfolio.Slug,
                portfolio.ShareTokenHash is not null,
                portfolio.ShareExpiresAt
            )
        );
    }

    public async Task<Result<ShareLinkDto>> RegenerateShareLinkAsync(
        Guid userId,
        Guid portfolioId,
        int expiresInDays,
        CancellationToken ct
    )
    {
        var portfolio = await OwnsAsync(userId, portfolioId, ct);
        if (portfolio is null)
            return Result<ShareLinkDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        if (expiresInDays < 1)
            return Result<ShareLinkDto>.Failure(
                Error.Validation("ExpiryInvalid", "Expiração do link deve ser de pelo menos 1 dia.")
            );

        portfolio.Visibility = "link";
        portfolio.Slug ??= BuildSlug(portfolio.Title, portfolio.Id);
        var clearToken = AssignShareToken(portfolio); // invalida qualquer link anterior
        portfolio.ShareExpiresAt = DateTime.UtcNow.AddDays(expiresInDays);
        portfolio.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Result<ShareLinkDto>.Success(
            new ShareLinkDto(portfolio.Id, portfolio.Slug, clearToken, portfolio.ShareExpiresAt)
        );
    }

    public async Task<Result> RevokeShareLinkAsync(Guid userId, Guid portfolioId, CancellationToken ct)
    {
        var portfolio = await OwnsAsync(userId, portfolioId, ct);
        if (portfolio is null)
            return Result.Failure(Error.NotFound("Portfolio", portfolioId.ToString()));

        // Revogar = limpar token/expiração: nenhum link restrito volta a funcionar.
        portfolio.ShareTokenHash = null;
        portfolio.ShareExpiresAt = null;
        portfolio.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ---------- público ----------

    public async Task<Result<PublicPortfolioDto>> GetPublicBySlugAsync(
        string slug,
        string? shareToken,
        CancellationToken ct
    )
    {
        var portfolio = await ResolveAccessibleAsync(slug, shareToken, ct);
        if (portfolio is null)
            return Result<PublicPortfolioDto>.Failure(PublicNotFound(slug));

        return await BuildPublicDtoAsync(portfolio, ct);
    }

    public async Task<Result<Guid>> ClonePublicAsync(
        string sourceSlug,
        Guid newUserId,
        CancellationToken ct
    )
    {
        var source = await ResolveAccessibleAsync(sourceSlug, shareToken: null, ct);
        if (source is null)
            return Result<Guid>.Failure(PublicNotFound(sourceSlug));

        var transactions = await db
            .PortfolioTransactions.Where(t =>
                t.PortfolioId == source.Id && t.ReversedByTransactionId == null
            )
            .ToListAsync(ct);
        var projection = PositionProjector.Project(transactions);
        if (projection.IsFailure)
            return Result<Guid>.Failure(projection.Error);

        var valued = await ValuePositionsAsync(projection.Value.Positions, ct);
        var total = valued.Sum(v => v.CurrentValue);
        if (total <= 0)
            return Result<Guid>.Failure(
                new Error("Portfolio.Empty", "Carteira pública sem posições para importar.")
            );

        var count = await db.Portfolios.CountAsync(p => p.UserId == newUserId, ct);
        if (count >= MaxPortfoliosPerUser)
            return Result<Guid>.Failure(
                new Error(
                    "Portfolio.LimitReached",
                    $"Limite de {MaxPortfoliosPerUser} carteiras atingido."
                )
            );

        var clone = new PortfolioEntity
        {
            UserId = newUserId,
            Title = $"{source.Title} (cópia)",
            Description = source.Description,
            RiskProfile = source.RiskProfile,
        };

        // BUY sintético por posição: unitPrice = 1 e quantidade = fração do peso × 10000 →
        // notional de R$ 10.000,00 distribuído exatamente conforme os pesos originais.
        foreach (var position in valued.Where(v => v.WeightPercent > 0))
        {
            var quantity = decimal.Round(position.CurrentValue / total * 10000m, 8);
            clone.Transactions.Add(
                new PortfolioTransactionEntity
                {
                    PortfolioId = clone.Id,
                    AssetId = position.AssetId,
                    SyntheticIndexCode = position.AssetId is null
                        ? position.SyntheticIndexCode
                        : null,
                    Type = "BUY",
                    Broker = "Importado",
                    Quantity = quantity,
                    UnitPrice = 1m,
                    GrossAmount = quantity,
                    Fees = 0m,
                    Currency = "BRL",
                    TradeDate = DateOnly.FromDateTime(DateTime.UtcNow),
                }
            );
        }

        db.Portfolios.Add(clone);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(clone.Id);
    }

    // ---------- acesso público (falha fechada como NotFound) ----------

    /// <summary>Resolve o slug aplicando as regras de visibilidade/link; null = NotFound.</summary>
    private async Task<PortfolioEntity?> ResolveAccessibleAsync(
        string slug,
        string? shareToken,
        CancellationToken ct
    )
    {
        var normalized = (slug ?? string.Empty).Trim().ToLowerInvariant();
        var portfolio = await db.Portfolios.FirstOrDefaultAsync(p => p.Slug == normalized, ct);
        if (portfolio is null || portfolio.Visibility == "private")
            return null;

        if (portfolio.Visibility == "link")
        {
            if (string.IsNullOrWhiteSpace(shareToken) || portfolio.ShareTokenHash is null)
                return null;
            if (!TokenMatches(shareToken, portfolio.ShareTokenHash))
                return null;
            if (portfolio.ShareExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
                return null;
        }

        return portfolio;
    }

    private static Error PublicNotFound(string slug) =>
        new("Portfolio.NotFound", $"Carteira pública '{slug}' não encontrada ou link expirado.");

    private async Task<Result<PublicPortfolioDto>> BuildPublicDtoAsync(
        PortfolioEntity portfolio,
        CancellationToken ct
    )
    {
        var transactions = await db
            .PortfolioTransactions.Where(t =>
                t.PortfolioId == portfolio.Id && t.ReversedByTransactionId == null
            )
            .ToListAsync(ct);
        var projection = PositionProjector.Project(transactions);
        if (projection.IsFailure)
            return Result<PublicPortfolioDto>.Failure(projection.Error);

        var valued = await ValuePositionsAsync(projection.Value.Positions, ct);
        var totalValue = valued.Sum(v => v.CurrentValue);
        var totalInvested = valued.Sum(v => v.InvestedAmount);
        var hideMoney = portfolio.PublicValuesMode != "full_values";

        var allocations = valued
            .GroupBy(v => v.AssetClass)
            .Select(g => new PublicAllocationClassDto(
                g.Key,
                totalValue > 0 ? decimal.Round(g.Sum(x => x.CurrentValue) / totalValue * 100m, 2) : 0m
            ))
            .OrderByDescending(a => a.Percent)
            .ToList();

        var dto = new PublicPortfolioDto(
            portfolio.Title,
            portfolio.Description,
            portfolio.RiskProfile,
            portfolio.DisplayIdentity ?? AnonymousIdentity,
            portfolio.PublicValuesMode,
            allocations,
            totalInvested > 0 ? decimal.Round((totalValue / totalInvested - 1m) * 100m, 2) : 0m,
            await PeriodReturnPercentAsync(portfolio.Id, ct),
            valued
                .Where(v => v.WeightPercent > 0)
                .OrderByDescending(v => v.WeightPercent)
                .Select(v => new PublicPositionDto(
                    v.Ticker,
                    v.Name,
                    v.WeightPercent,
                    v.ReturnPercent,
                    hideMoney ? null : v.CurrentValue,
                    hideMoney ? null : v.InvestedAmount
                ))
                .ToList(),
            portfolio.CreatedAt
        );

        return Result<PublicPortfolioDto>.Success(dto);
    }

    // ---------- valuation local-first (lean) ----------

    private sealed record ValuedPosition(
        Guid? AssetId,
        string? SyntheticIndexCode,
        string Ticker,
        string Name,
        string AssetClass,
        decimal CurrentValue,
        decimal InvestedAmount
    )
    {
        public decimal WeightPercent { get; init; }
        public decimal ReturnPercent { get; init; }
    }

    /// <summary>
    /// Último close local por ativo (ajustado por FX quando estrangeiro), custo como fallback.
    /// Caixa sintético/RF fica ao custo na página pública (sem accrual diário).
    /// </summary>
    private async Task<List<ValuedPosition>> ValuePositionsAsync(
        IReadOnlyList<ProjectedPosition> projected,
        CancellationToken ct
    )
    {
        var assetIds = projected
            .Where(p => p.AssetId is not null)
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

        var fx = new Dictionary<string, decimal>();
        foreach (var currency in assets.Values.Select(a => a.Currency).Distinct())
        {
            if (currency == "BRL")
                continue;
            var rate = await db
                .FxRates.Where(f => f.Pair == $"{currency}-BRL")
                .OrderByDescending(f => f.Date)
                .Select(f => (decimal?)f.Bid)
                .FirstOrDefaultAsync(ct);
            if (rate is not null)
                fx[currency] = rate.Value;
        }

        var rows = new List<ValuedPosition>(projected.Count);
        foreach (var p in projected)
        {
            var invested = p.InvestedAmount;
            decimal current;
            string ticker;
            string name;
            string assetClass;

            if (p.AssetId is not null && assets.TryGetValue(p.AssetId.Value, out var asset))
            {
                var factor = asset.Currency == "BRL" ? 1m : fx.GetValueOrDefault(asset.Currency, 0m);
                current =
                    factor > 0 && closes.TryGetValue(asset.Id, out var close)
                        ? p.Quantity * close * factor
                        : invested;
                ticker = asset.Ticker;
                name = asset.Name;
                assetClass = asset.AssetType;
            }
            else
            {
                current = invested;
                ticker = p.SyntheticIndexCode;
                name = "Caixa sintético";
                assetClass = "Caixa sintético";
            }

            rows.Add(
                new ValuedPosition(
                    p.AssetId,
                    p.SyntheticIndexCode,
                    ticker,
                    name,
                    assetClass,
                    current,
                    invested
                )
                {
                    ReturnPercent =
                        invested > 0 ? decimal.Round((current / invested - 1m) * 100m, 2) : 0m,
                }
            );
        }

        var total = rows.Sum(r => r.CurrentValue);
        return rows
            .Select(r => r with
            {
                WeightPercent = total > 0 ? decimal.Round(r.CurrentValue / total * 100m, 4) : 0m,
            })
            .ToList();
    }

    /// <summary>Retorno dos últimos ~30 dias via snapshots locais; null sem série suficiente.</summary>
    private async Task<decimal?> PeriodReturnPercentAsync(Guid portfolioId, CancellationToken ct)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var snapshots = await db
            .PortfolioDailySnapshots.Where(s => s.PortfolioId == portfolioId && s.SnapshotDate >= cutoff)
            .OrderBy(s => s.SnapshotDate)
            .Select(s => new ValueTuple<DateOnly, decimal>(s.SnapshotDate, s.TotalValue))
            .ToListAsync(ct);

        if (snapshots.Count < 2 || snapshots[0].Item2 <= 0)
            return null;

        return decimal.Round((snapshots[^1].Item2 / snapshots[0].Item2 - 1m) * 100m, 2);
    }

    // ---------- helpers ----------

    private async Task<PortfolioEntity?> OwnsAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    ) =>
        await db.Portfolios.FirstOrDefaultAsync(p => p.Id == portfolioId && p.UserId == userId, ct);

    /// <summary>Gera token claro de 32 bytes (base64url) e persiste apenas o hash SHA-256 hex.</summary>
    private static string AssignShareToken(PortfolioEntity portfolio)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        portfolio.ShareTokenHash = HashToken(token);
        return token;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Comparação em tempo constante entre o hash apresentado e o armazenado.</summary>
    private static bool TokenMatches(string presentedToken, string storedHash)
    {
        var presentedHash = HashToken(presentedToken.Trim());
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presentedHash),
            Encoding.UTF8.GetBytes(storedHash)
        );
    }

    /// <summary>
    /// kebab-case do título (acentos removidos, lowercase, espaços → '-', '-' colapsados)
    /// + sufixo estável "-{id:N[..6]}"; unicidade garantida pelo índice único parcial em Slug.
    /// </summary>
    internal static string BuildSlug(string title, Guid id)
    {
        var decomposed = title.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue; // remove acento (deixa a letra base)
            if (char.IsWhiteSpace(ch))
                sb.Append('-');
            else if (char.IsAsciiLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
            // demais símbolos/pontuação caem fora
        }

        var collapsed = new StringBuilder(sb.Length);
        var previousHyphen = false;
        foreach (var ch in sb.ToString())
        {
            var hyphen = ch == '-';
            if (hyphen && previousHyphen)
                continue;
            collapsed.Append(ch);
            previousHyphen = hyphen;
        }

        var kebab = collapsed.ToString().Trim('-');
        if (kebab.Length == 0)
            kebab = "carteira";
        if (kebab.Length > MaxSlugLength)
            kebab = kebab[..MaxSlugLength].TrimEnd('-');

        return $"{kebab}-{id.ToString("N")[..6]}";
    }
}
