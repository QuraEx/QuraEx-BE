using QuraEx.BuildingBlocks.Messaging;

namespace QuraEx.Authoring.Contracts;

/// <summary>Published by Authoring via transactional outbox when a story is created, updated, or deleted.
/// Consumed by TestArtifact (story_snapshot) and other read-model services.</summary>
public sealed record StoryChangedEvent : IntegrationEvent
{
    public Guid StoryId { get; init; }
    public Guid ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty; // CREATED | UPDATED | DELETED
}
