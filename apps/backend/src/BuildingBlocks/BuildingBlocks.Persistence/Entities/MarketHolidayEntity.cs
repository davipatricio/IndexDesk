namespace IndexDesk.BuildingBlocks.Persistence.Entities;

/// <summary>
/// Non-business day marker (MODELS.md §market_holidays). Used by
/// <c>BusinessDayCalculator</c> to compute accurate "latest business day" ranges
/// for sync catch-up and to back-calculate business-day returns (252 base).
/// PK is the date itself; description is a short label (e.g. "Carnaval",
/// "Tiradentes", "Confraternização Universal"). Populated by an ANBIMA seed job
/// (FND-013) or the operator manually; an empty table still yields correct results
/// because <c>BusinessDayCalculator</c> falls back to weekday-only filtering.
/// </summary>
public class MarketHolidayEntity
{
    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Exchange { get; set; } = "B3";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
