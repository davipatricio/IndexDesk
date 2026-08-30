using FluentAssertions;
using IndexDesk.Modules.MarketData.Ingestion;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Pure business-day arithmetic shared by the sync catch-up bootstrap and any
/// analytics that need to count trading days. Holiday set is just a hash of dates
/// so the tests don't have to spin up a DbContext.
/// </summary>
public class BusinessDayCalculatorTests
{
    private static IReadOnlySet<DateOnly> Hol(params (int y, int m, int d)[] entries) =>
        entries.Select(e => new DateOnly(e.y, e.m, e.d)).ToHashSet();

    [Fact]
    public void PreviousBusinessDay_ReturnsItselfWhenAlreadyBusinessDay()
    {
        // 2025-08-29 is a Friday with no ANBIMA holiday.
        BusinessDayCalculator
            .PreviousBusinessDay(new DateOnly(2025, 8, 29), Hol())
            .Should()
            .Be(new DateOnly(2025, 8, 29));
    }

    [Fact]
    public void PreviousBusinessDay_SkipsWeekend()
    {
        // Saturday → Friday, no holiday set needed.
        BusinessDayCalculator
            .PreviousBusinessDay(new DateOnly(2025, 8, 30), Hol())
            .Should()
            .Be(new DateOnly(2025, 8, 29));

        // Sunday → Friday.
        BusinessDayCalculator
            .PreviousBusinessDay(new DateOnly(2025, 8, 31), Hol())
            .Should()
            .Be(new DateOnly(2025, 8, 29));
    }

    [Fact]
    public void PreviousBusinessDay_SkipsHoliday()
    {
        // Synthetic Monday holiday for determinism.
        var holidays = Hol((2025, 6, 2));
        BusinessDayCalculator
            .PreviousBusinessDay(new DateOnly(2025, 6, 2), holidays)
            .Should()
            .Be(new DateOnly(2025, 5, 30));
    }

    [Fact]
    public void PreviousBusinessDay_SkipsConsecutiveHolidaysAndWeekend()
    {
        // 2025-03-03 (Mon, Carnaval) + 2025-03-04 (Tue, Carnaval) → should land on
        // Friday 2025-02-28.
        var holidays = Hol((2025, 3, 3), (2025, 3, 4));
        BusinessDayCalculator
            .PreviousBusinessDay(new DateOnly(2025, 3, 4), holidays)
            .Should()
            .Be(new DateOnly(2025, 2, 28));
    }

    [Fact]
    public void PreviousBusinessDay_EmptyHolidaySetStillSkipsWeekends()
    {
        BusinessDayCalculator
            .PreviousBusinessDay(new DateOnly(2025, 1, 4), Hol()) // Saturday
            .Should()
            .Be(new DateOnly(2025, 1, 3));
    }

    [Fact]
    public void BusinessDaysBetween_ExcludesWeekendsAndHolidays()
    {
        // Mon 2025-08-25 → Fri 2025-08-29 with no holidays → 5 business days.
        BusinessDayCalculator
            .BusinessDaysBetween(new DateOnly(2025, 8, 25), new DateOnly(2025, 8, 29), Hol())
            .Should()
            .Equal(
                new DateOnly(2025, 8, 25),
                new DateOnly(2025, 8, 26),
                new DateOnly(2025, 8, 27),
                new DateOnly(2025, 8, 28),
                new DateOnly(2025, 8, 29)
            );
    }

    [Fact]
    public void BusinessDaysBetween_SkipsListedHolidays()
    {
        // Mon..Fri with a Wednesday holiday.
        var holidays = Hol((2025, 6, 4));
        BusinessDayCalculator
            .BusinessDaysBetween(new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6), holidays)
            .Should()
            .Equal(
                new DateOnly(2025, 6, 2),
                new DateOnly(2025, 6, 3),
                new DateOnly(2025, 6, 5),
                new DateOnly(2025, 6, 6)
            );
    }

    [Fact]
    public void BusinessDaysBetween_SpansMultipleWeeks()
    {
        // Two full weeks, no holidays: 10 business days.
        BusinessDayCalculator
            .BusinessDaysBetween(new DateOnly(2025, 8, 25), new DateOnly(2025, 9, 5), Hol())
            .Count()
            .Should()
            .Be(10);
    }

    [Fact]
    public void BusinessDaysBetween_FromAfterToReturnsEmpty()
    {
        BusinessDayCalculator
            .BusinessDaysBetween(new DateOnly(2025, 9, 1), new DateOnly(2025, 8, 30), Hol())
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void BusinessDaysBetween_SameDayReturnsSingleBusinessDay()
    {
        BusinessDayCalculator
            .BusinessDaysBetween(new DateOnly(2025, 8, 27), new DateOnly(2025, 8, 27), Hol())
            .Should()
            .Equal(new DateOnly(2025, 8, 27));
    }

    [Fact]
    public void BusinessDaysBetween_SameWeekendDayReturnsEmpty()
    {
        BusinessDayCalculator
            .BusinessDaysBetween(new DateOnly(2025, 8, 30), new DateOnly(2025, 8, 30), Hol())
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void IsBusinessDay_WeekdayWithoutHolidayReturnsTrue()
    {
        BusinessDayCalculator
            .IsBusinessDay(new DateOnly(2025, 8, 27), Hol())
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsBusinessDay_SaturdayOrSundayReturnsFalse()
    {
        BusinessDayCalculator
            .IsBusinessDay(new DateOnly(2025, 8, 30), Hol())
            .Should()
            .BeFalse();
        BusinessDayCalculator
            .IsBusinessDay(new DateOnly(2025, 8, 31), Hol())
            .Should()
            .BeFalse();
    }

    [Fact]
    public void IsBusinessDay_WeekdayWithHolidayReturnsFalse()
    {
        var holidays = Hol((2025, 6, 2));
        BusinessDayCalculator
            .IsBusinessDay(new DateOnly(2025, 6, 2), holidays)
            .Should()
            .BeFalse();
    }
}
