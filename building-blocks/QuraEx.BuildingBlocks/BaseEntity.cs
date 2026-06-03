namespace QuraEx.BuildingBlocks;

/// <summary>Root base for every domain entity. Id uses UUIDv7 for index locality (Guid.CreateVersion7, .NET 10).</summary>
public abstract class BaseEntity
{
    private readonly List<DomainEvent> _domainEvents = [];

    protected BaseEntity() { }

    public Guid Id { get; protected init; } = Guid.CreateVersion7();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void SetUpdated() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>Aggregate roots own the lifecycle of their child entities and are the transactional boundary.</summary>
public abstract class AggregateRoot : BaseEntity
{
    public Guid CreatedBy { get; protected set; }
    public Guid UpdatedBy { get; protected set; }

    protected AggregateRoot() { }
}

/// <summary>Opt-in marker for soft-delete. EF global query filter is applied ONLY to entities implementing this interface.
/// Infra tables (outbox/processed/snapshot) must NOT implement this.</summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; }
    void SoftDelete();
}

/// <summary>Mixin for aggregates that support soft-delete.</summary>
public abstract class SoftDeletableAggregate : AggregateRoot, ISoftDeletable
{
    public DateTime? DeletedAt { get; private set; }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        SetUpdated();
    }
}
