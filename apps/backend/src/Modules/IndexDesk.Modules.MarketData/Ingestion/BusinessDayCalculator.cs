namespace IndexDesk.Modules.MarketData.Ingestion;

/// <summary>
/// Pure (no I/O) business-day arithmetic used by the sync catch-up bootstrap and
/// downstream analytics. B3 follows ANBIMA's calendar: weekdays minus an explicit
/// holiday set. When the holiday set is empty the calculator still works — it just
/// falls back to weekday-only filtering, which is the safe default for any time
/// range the operator hasn't seeded yet.
///
/// Extracted from <see cref="DailyCloseSyncService"/> in Fase 4 to keep the
/// weekend/holiday decision rules unit-testable without a database. The companion
/// EF queries (<see cref="MarketHolidayQueries"/>) only do the I/O shuffle; this
/// class owns the actual math.
/// </summary>
internal static class BusinessDayCalculator
{
    /// <summary>Returns the most recent business day that is &lt;= <paramref name="date"/>.
    /// If the given date is itself a business day, it is returned as-is. Never throws
    /// for null/empty holiday sets.</summary>
    public static DateOnly PreviousBusinessDay(DateOnly date, IReadOnlySet<DateOnly> holidays)
    {
        var cursor = date;
        while (!IsBusinessDay(cursor, holidays))
        {
            cursor = cursor.AddDays(-1);
        }
        return cursor;
    }

    /// <summary>Returns all business days in the half-open range
    /// <c>[from, to]</c>. Order is ascending. When <paramref name="from"/> &gt;
    /// <paramref name="to"/> the result is empty. Both bounds are inclusive.</summary>
    public static List<DateOnly> BusinessDaysBetween(
        DateOnly from,
        DateOnly to,
        IReadOnlySet<DateOnly> holidays
    )
    {
        var result = new List<DateOnly>();
        if (from > to)
        {
            return result;
        }

        for (var cursor = from; cursor <= to; cursor = cursor.AddDays(1))
        {
            if (IsBusinessDay(cursor, holidays))
            {
                result.Add(cursor);
            }
        }
        return result;
    }

    /// <summary>Saturday/Sunday are never business days regardless of holiday set.</summary>
    public static bool IsBusinessDay(DateOnly date, IReadOnlySet<DateOnly> holidays)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        return !holidays.Contains(date);
    }
}
