namespace QuraEx.BuildingBlocks.Persistence;

/// <summary>Transactional outbox row written atomically with the business entity in the same DB transaction.
/// Relay = MassTransit Transactional Outbox (AddEntityFrameworkOutbox + UseBusOutbox).
/// seq orders delivery; attempt_count and last_error track poison-message diagnostics.</summary>
public class OutboxMessage
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>Gets bigserial — relay ordering guarantee; assigned by Postgres on insert.</summary>
    public long Seq { get; private set; }

    public string Type { get; private init; } = string.Empty;
    public string Payload { get; private init; } = string.Empty;
    public DateTime OccurredAt { get; private init; } = DateTime.UtcNow;

    /// <summary>Gets null until published by the relay.</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Gets incremented by the relay on each delivery attempt.</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Gets last relay error message — nullable; for poison-message diagnostics only.</summary>
    public string? LastError { get; private set; }

    protected OutboxMessage() { }

    public static OutboxMessage Create(string type, string payload) =>
        new() { Type = type, Payload = payload };

    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;

    public void RecordFailure(string error)
    {
        AttemptCount++;
        LastError = error;
    }
}
