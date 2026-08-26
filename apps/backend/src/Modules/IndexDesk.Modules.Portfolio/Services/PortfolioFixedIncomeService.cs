using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Calculators;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Fixed-income parameter management (attach/list/detach), maturity timeline and the
/// daily accrual pass used by PortfolioAccrualDailyJob. All rates come from the local
/// macro_economic_series table — never from an external API at request time.
/// </summary>
public sealed class PortfolioFixedIncomeService(IndexDeskDbContext db)
    : IPortfolioFixedIncomeService
{
    private static readonly string[] ValidIndexers =
    [
        "CDI_PERCENT",
        "CDI_PLUS",
        "SELIC",
        "IPCA_PLUS",
        "PREFIXED",
    ];

    public async Task<Result<FixedIncomeParamDto>> AttachAsync(
        Guid userId,
        Guid portfolioId,
        AttachFixedIncomeRequest request,
        CancellationToken ct
    )
    {
        var owned = await db.Portfolios.FirstOrDefaultAsync(
            p => p.Id == portfolioId && p.UserId == userId,
            ct
        );
        if (owned is null)
            return Result<FixedIncomeParamDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        if (!ValidIndexers.Contains(request.Indexer.ToUpperInvariant()))
            return Result<FixedIncomeParamDto>.Failure(
                Error.Validation("IndexerInvalid", $"Indexador inválido: {request.Indexer}.")
            );
        if (request.AssetId is null && string.IsNullOrWhiteSpace(request.SyntheticIndexCode))
            return Result<FixedIncomeParamDto>.Failure(
                Error.Validation("TargetRequired", "Informe o ativo ou o índice sintético.")
            );
        if (request.MaturityDate <= request.StartDate)
            return Result<FixedIncomeParamDto>.Failure(
                Error.Validation("MaturityInvalid", "Vencimento deve ser posterior ao início.")
            );

        var existing = await db.PortfolioFixedIncomePositions.FirstOrDefaultAsync(
            f =>
                f.PortfolioId == portfolioId
                && f.AssetId == request.AssetId
                && f.SyntheticIndexCode == request.SyntheticIndexCode,
            ct
        );

        if (existing is null)
        {
            existing = new PortfolioFixedIncomePositionEntity
            {
                PortfolioId = portfolioId,
                AssetId = request.AssetId,
                SyntheticIndexCode = request.SyntheticIndexCode?.ToUpperInvariant(),
            };
            db.PortfolioFixedIncomePositions.Add(existing);
        }

        existing.Indexer = request.Indexer.ToUpperInvariant();
        existing.IndexerRate = request.IndexerRate;
        existing.Principal = request.Principal;
        existing.StartDate = request.StartDate;
        existing.MaturityDate = request.MaturityDate;
        existing.Liquidity = request.Liquidity;
        existing.TaxRegime = request.TaxRegime;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<FixedIncomeParamDto>.Success(ToDto(existing));
    }

    public async Task<Result<IReadOnlyList<FixedIncomeParamDto>>> ListAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    )
    {
        if (
            await db.Portfolios.FirstOrDefaultAsync(
                p => p.Id == portfolioId && p.UserId == userId,
                ct
            )
            is null
        )
            return Result<IReadOnlyList<FixedIncomeParamDto>>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var rows = await db
            .PortfolioFixedIncomePositions.Where(f => f.PortfolioId == portfolioId)
            .OrderBy(f => f.MaturityDate)
            .ToListAsync(ct);
        return Result<IReadOnlyList<FixedIncomeParamDto>>.Success(rows.Select(ToDto).ToList());
    }

    public async Task<Result> DetachAsync(
        Guid userId,
        Guid portfolioId,
        Guid fixedIncomeId,
        CancellationToken ct
    )
    {
        if (
            await db.Portfolios.FirstOrDefaultAsync(
                p => p.Id == portfolioId && p.UserId == userId,
                ct
            )
            is null
        )
            return Result.Failure(Error.NotFound("Portfolio", portfolioId.ToString()));

        var row = await db.PortfolioFixedIncomePositions.FirstOrDefaultAsync(
            f => f.Id == fixedIncomeId && f.PortfolioId == portfolioId,
            ct
        );
        if (row is null)
            return Result.Failure(Error.NotFound("FixedIncome", fixedIncomeId.ToString()));

        db.PortfolioFixedIncomePositions.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<TimelineItemDto>>> GetTimelineAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    )
    {
        if (
            await db.Portfolios.FirstOrDefaultAsync(
                p => p.Id == portfolioId && p.UserId == userId,
                ct
            )
            is null
        )
            return Result<IReadOnlyList<TimelineItemDto>>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowStart = today.AddDays(-7); // venceu há pouco continua relevante

        var assetLabels = await (
            from t in db.PortfolioTransactions.Where(t =>
                t.PortfolioId == portfolioId
                && t.MaturityDate != null
                && t.MaturityDate >= windowStart
                && t.Type == "BUY"
            )
            join a in db.Assets on t.AssetId equals a.Id into assets
            from a in assets.DefaultIfEmpty()
            select new
            {
                Date = t.MaturityDate!.Value,
                Label = a != null ? a.Ticker : (t.SyntheticIndexCode ?? "RF"),
                Amount = t.GrossAmount,
            }
        ).ToListAsync(ct);

        var items = assetLabels
            .Select(x => new TimelineItemDto(x.Date, x.Label, "maturity", x.Amount))
            .ToList();

        var params_ = await db
            .PortfolioFixedIncomePositions.Where(f =>
                f.PortfolioId == portfolioId && f.MaturityDate >= windowStart
            )
            .ToListAsync(ct);
        foreach (var p in params_)
        {
            items.Add(
                new TimelineItemDto(
                    p.MaturityDate,
                    p.SyntheticIndexCode ?? "RF",
                    p.Liquidity == "maturity" ? "maturity" : "liquidity",
                    p.AccruedValue > 0 ? p.AccruedValue : p.Principal
                )
            );
        }

        return Result<IReadOnlyList<TimelineItemDto>>.Success(items.OrderBy(i => i.Date).ToList());
    }

    public async Task<int> AccrueAllAsync(CancellationToken ct)
    {
        var rows = await db.PortfolioFixedIncomePositions.ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var touched = 0;

        // Agrupa códigos SGS necessários para carregar as séries uma vez só.
        foreach (var row in rows)
        {
            var end =
                row.MaturityDate < today && row.Liquidity == "maturity" ? row.MaturityDate : today;
            if (end <= row.StartDate || row.LastAccrualDate == end)
                continue; // nada a fazer: janela vazia ou já accrualado hoje (idempotência)

            var sgsCode = SgsCodeFor(row.Indexer);
            var rates = await LoadRatesAsync(sgsCode, row.StartDate, end, ct);

            var result = FixedIncomeAccrualCalculator.Accrue(
                new FixedIncomeAccrualCalculator.Input(
                    row.Indexer,
                    row.IndexerRate,
                    row.Principal,
                    row.StartDate,
                    end,
                    rates
                )
            );

            row.AccruedValue = result.AccruedValue;
            row.LastAccrualDate = end;
            row.UpdatedAt = DateTime.UtcNow;
            touched++;
        }

        if (touched > 0)
            await db.SaveChangesAsync(ct);
        return touched;
    }

    // ---------- helpers ----------

    private async Task<List<(DateOnly, decimal)>> LoadRatesAsync(
        int sgsCode,
        DateOnly start,
        DateOnly end,
        CancellationToken ct
    ) =>
        await db
            .MacroEconomicSeries.Where(m =>
                m.SeriesCode == sgsCode && m.Date > start && m.Date <= end
            )
            .OrderBy(m => m.Date)
            .Select(m => new ValueTuple<DateOnly, decimal>(m.Date, m.Value))
            .ToListAsync(ct);

    private static int SgsCodeFor(string indexer) =>
        indexer.ToUpperInvariant() switch
        {
            "SELIC" => 11,
            "IPCA_PLUS" => 433,
            _ => 12, // CDI diária
        };

    private static FixedIncomeParamDto ToDto(PortfolioFixedIncomePositionEntity f) =>
        new(
            f.Id,
            f.AssetId,
            f.SyntheticIndexCode,
            f.Indexer,
            f.IndexerRate,
            f.Principal,
            f.StartDate,
            f.MaturityDate,
            f.Liquidity,
            f.TaxRegime,
            f.AccruedValue,
            f.LastAccrualDate
        );
}
