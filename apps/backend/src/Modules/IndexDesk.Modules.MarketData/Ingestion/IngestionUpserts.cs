using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Domain;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Shared idempotent upserts for ingestion services (quotes, dividends, FX, holdings).
/// Re-running the same day updates in place — never duplicates.
/// </summary>
public static class IngestionUpserts
{
    public static async Task<int> QuotesAsync(
        IndexDeskDbContext dbContext,
        Guid assetId,
        IReadOnlyList<NormalizedQuote> quotes,
        CancellationToken cancellationToken
    )
    {
        if (quotes.Count == 0)
            return 0;

        var dates = quotes.Select(q => q.Date).ToList();
        var existingQuotes = await dbContext
            .AssetQuotes.Where(q => q.AssetId == assetId && dates.Contains(q.Date))
            .ToDictionaryAsync(q => q.Date, cancellationToken);

        var toAdd = new List<AssetQuoteEntity>();

        foreach (var quote in quotes)
        {
            if (existingQuotes.TryGetValue(quote.Date, out var existing))
            {
                existing.Open = quote.Open;
                existing.High = quote.High;
                existing.Low = quote.Low;
                existing.Close = quote.Close;
                existing.AdjClose = quote.AdjClose;
                existing.Volume = quote.Volume;
                existing.SourceProvider = quote.SourceProvider;
            }
            else
            {
                toAdd.Add(
                    new AssetQuoteEntity
                    {
                        AssetId = assetId,
                        Date = quote.Date,
                        Open = quote.Open,
                        High = quote.High,
                        Low = quote.Low,
                        Close = quote.Close,
                        AdjClose = quote.AdjClose,
                        Volume = quote.Volume,
                        TradesCount = quote.TradesCount,
                        SourceProvider = quote.SourceProvider,
                    }
                );
            }
        }

        if (toAdd.Count > 0)
        {
            await dbContext.AssetQuotes.AddRangeAsync(toAdd, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return quotes.Count;
    }

    public static async Task<int> DividendsAsync(
        IndexDeskDbContext dbContext,
        Guid assetId,
        IReadOnlyList<NormalizedDividend> dividends,
        CancellationToken cancellationToken
    )
    {
        if (dividends.Count == 0)
            return 0;

        var existingDividends = await dbContext
            .AssetDividends.Where(d => d.AssetId == assetId)
            .ToListAsync(cancellationToken);

        var toAdd = new List<AssetDividendEntity>();

        foreach (var div in dividends)
        {
            // Round to the column scale (numeric(14,6)) BEFORE comparing: providers
            // emit float noise (2.0307215000…) that differs from the stored rounded
            // value (2.030722) under exact decimal equality, yet collides on the
            // (AssetId, ComDate, Rate) unique index once PostgreSQL rounds on insert.
            // AwayFromZero matches PostgreSQL's numeric round; the epsilon compare
            // covers any residual representation drift in either direction.
            var rate = decimal.Round(div.Rate, 6, MidpointRounding.AwayFromZero);
            var alreadyExists = existingDividends.Any(e =>
                e.ComDate == div.ComDate && Math.Abs(e.Rate - rate) < 0.0000005m
            );
            if (!alreadyExists)
            {
                toAdd.Add(
                    new AssetDividendEntity
                    {
                        AssetId = assetId,
                        ComDate = div.ComDate,
                        PaymentDate = div.PaymentDate,
                        Rate = rate,
                        DividendType = div.DividendType,
                        Currency = div.Currency,
                        SourceProvider = div.SourceProvider,
                    }
                );
            }
        }

        if (toAdd.Count > 0)
        {
            await dbContext.AssetDividends.AddRangeAsync(toAdd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return toAdd.Count;
    }

    /// <summary>FX closes keyed by (pair, date); re-running a day refreshes bid/ask.</summary>
    public static async Task<int> FxRatesAsync(
        IndexDeskDbContext dbContext,
        IReadOnlyList<NormalizedFxRate> rates,
        CancellationToken cancellationToken
    )
    {
        if (rates.Count == 0)
            return 0;

        var pairs = rates.Select(r => r.Pair).Distinct().ToList();
        var dates = rates.Select(r => r.Date).Distinct().ToList();
        var existing = await dbContext
            .FxRates.Where(f => pairs.Contains(f.Pair) && dates.Contains(f.Date))
            .ToDictionaryAsync(f => (f.Pair, f.Date), cancellationToken);

        foreach (var rate in rates)
        {
            if (existing.TryGetValue((rate.Pair, rate.Date), out var row))
            {
                row.Bid = rate.Bid;
                row.Ask = rate.Ask;
                row.SourceProvider = rate.SourceProvider;
                continue;
            }

            dbContext.FxRates.Add(
                new FxRateEntity
                {
                    Pair = rate.Pair,
                    Date = rate.Date,
                    Bid = rate.Bid,
                    Ask = rate.Ask,
                    SourceProvider = rate.SourceProvider,
                }
            );
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return rates.Count;
    }

    /// <summary>
    /// Holdings upsert deduplicated on (etfAssetId, asOfDate, holdingTicker).
    /// Name-only rows (Investo publishes no tickers) have HoldingTicker = null —
    /// Postgres treats NULLs as distinct in unique indexes, so those fall back to a
    /// name-keyed match to keep the re-run idempotent. Weights/names refresh in place.
    /// </summary>
    public static async Task<int> EtfHoldingsAsync(
        IndexDeskDbContext dbContext,
        IReadOnlyList<EtfHoldingRow> holdings,
        CancellationToken cancellationToken
    )
    {
        if (holdings.Count == 0)
            return 0;

        var assetIds = holdings.Select(h => h.EtfAssetId).Distinct().ToList();
        var asOfDates = holdings.Select(h => h.AsOfDate).Distinct().ToList();
        var existingRows = await dbContext
            .EtfHoldings.Where(h =>
                assetIds.Contains(h.EtfAssetId) && asOfDates.Contains(h.AsOfDate)
            )
            .ToListAsync(cancellationToken);

        var byTicker = new Dictionary<(Guid, DateOnly, string), EtfHoldingEntity>();
        var byName = new Dictionary<(Guid, DateOnly, string), EtfHoldingEntity>();
        foreach (var row in existingRows)
        {
            if (row.HoldingTicker is not null)
            {
                byTicker[(row.EtfAssetId, row.AsOfDate, row.HoldingTicker)] = row;
            }
            else
            {
                byName.TryAdd(
                    (row.EtfAssetId, row.AsOfDate, NormalizeNameKey(row.HoldingName)),
                    row
                );
            }
        }

        foreach (var row in holdings)
        {
            EtfHoldingEntity? entity = null;

            if (row.HoldingTicker is not null)
            {
                byTicker.TryGetValue((row.EtfAssetId, row.AsOfDate, row.HoldingTicker), out entity);
            }
            else
            {
                byName.TryGetValue(
                    (row.EtfAssetId, row.AsOfDate, NormalizeNameKey(row.HoldingName)),
                    out entity
                );
            }

            if (entity is not null)
            {
                entity.HoldingName = row.HoldingName;
                entity.WeightPercentage = row.WeightPercentage;
                entity.Sector = row.Sector;
                entity.Country = row.Country;
                continue;
            }

            var added = new EtfHoldingEntity
            {
                EtfAssetId = row.EtfAssetId,
                HoldingTicker = row.HoldingTicker,
                HoldingName = row.HoldingName,
                WeightPercentage = row.WeightPercentage,
                Sector = row.Sector,
                Country = row.Country,
                AsOfDate = row.AsOfDate,
            };
            dbContext.EtfHoldings.Add(added);

            if (row.HoldingTicker is not null)
            {
                byTicker[(row.EtfAssetId, row.AsOfDate, row.HoldingTicker)] = added;
            }
            else
            {
                byName[(row.EtfAssetId, row.AsOfDate, NormalizeNameKey(row.HoldingName))] = added;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return holdings.Count;
    }

    private static string NormalizeNameKey(string name) => name.Trim().ToUpperInvariant();
}

/// <summary>Parsed holding line before persistence (asset resolved separately).</summary>
public readonly record struct EtfHoldingRow(
    Guid EtfAssetId,
    string? HoldingTicker,
    string HoldingName,
    decimal WeightPercentage,
    string? Sector,
    string? Country,
    DateOnly AsOfDate
);
