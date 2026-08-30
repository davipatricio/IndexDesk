using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// I/O adapters for the <c>market_holidays</c> table. The math itself lives in
/// <see cref="BusinessDayCalculator"/>; these helpers just load the holiday set for
/// the window of interest and dispatch to the pure calculator. The sync catch-up
/// bootstrap (<see cref="IndexDesk.Worker.Services.SyncBootstrapService"/>) is the
/// only known consumer at the moment, but the helpers are public so Analytics can
/// later reuse them for trading-day normalization (base 252).
/// </summary>
public static class MarketHolidayQueries
{
    /// <summary>The most recent business day on or before <paramref name="date"/>,
    /// treating the B3 calendar (weekends + ANBIMA holidays) as the off-days.</summary>
    public static async Task<DateOnly> GetLatestBusinessDayOnOrBeforeAsync(
        IndexDeskDbContext db,
        DateOnly date,
        CancellationToken cancellationToken = default
    )
    {
        var holidays = await LoadHolidaysAsync(db, min: DateOnly.MinValue, max: date, cancellationToken);
        return BusinessDayCalculator.PreviousBusinessDay(date, holidays);
    }

    /// <summary>All business days in the inclusive range <c>[from, to]</c>, ordered
    /// ascending. The holiday set is loaded in a single round trip and cached in
    /// memory — the range is bounded by the sync catch-up config
    /// (<c>Sync:CatchUp:MaxBacklogDays</c>, default 30) so this stays small.</summary>
    public static async Task<List<DateOnly>> GetBusinessDaysBetweenAsync(
        IndexDeskDbContext db,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default
    )
    {
        if (from > to)
        {
            return [];
        }

        var holidays = await LoadHolidaysAsync(db, min: from, max: to, cancellationToken);
        return BusinessDayCalculator.BusinessDaysBetween(from, to, holidays);
    }

    private static async Task<HashSet<DateOnly>> LoadHolidaysAsync(
        IndexDeskDbContext db,
        DateOnly min,
        DateOnly max,
        CancellationToken cancellationToken
    )
    {
        var rows = await db
            .MarketHolidays.AsNoTracking()
            .Where(h => h.Date >= min && h.Date <= max && h.Exchange == "B3")
            .Select(h => h.Date)
            .ToListAsync(cancellationToken);

        return new HashSet<DateOnly>(rows);
    }
}
