namespace QuraEx.BuildingBlocks.Messaging;

/// <summary>Base for all cross-service integration events published via the transactional outbox.
/// Consumers receive this contract; they must not depend on the publishing service's domain types.</summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = string.Empty;
}
