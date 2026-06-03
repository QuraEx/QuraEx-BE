using MediatR;

namespace QuraEx.BuildingBlocks;

/// <summary>Base for all in-process domain events dispatched via MediatR after the transaction commits.</summary>
public abstract record DomainEvent : INotification
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
