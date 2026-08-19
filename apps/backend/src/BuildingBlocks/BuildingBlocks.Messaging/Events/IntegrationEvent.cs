namespace IndexDesk.BuildingBlocks.Messaging.Events;

public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

public sealed record QuotesUpdatedIntegrationEvent(
    string Ticker,
    DateOnly ReferenceDate,
    decimal ClosePrice
) : IntegrationEvent;

public sealed record CacheInvalidationRequestedEvent(string CacheKeyPattern, string Reason)
    : IntegrationEvent;
