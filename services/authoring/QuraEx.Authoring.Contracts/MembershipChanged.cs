using QuraEx.BuildingBlocks.Messaging;

namespace QuraEx.Authoring.Contracts;

/// <summary>Published by Workspace when a project member is added, updated, or removed.
/// Authoring consumes this to keep membership_snapshot up to date for authz checks.</summary>
public sealed record MembershipChangedEvent : IntegrationEvent
{
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }
    public string Role { get; init; } = string.Empty; // EDITOR | VIEWER | REMOVED
    public string ChangeType { get; init; } = string.Empty; // ADDED | UPDATED | REMOVED
}
