using QuraEx.BuildingBlocks;

namespace QuraEx.Authoring.Domain.DomainEvents;

public sealed record UserStoryCreatedEvent(Guid StoryId, Guid ProjectId) : DomainEvent;

public sealed record UserStoryUpdatedEvent(Guid StoryId, Guid ProjectId) : DomainEvent;

public sealed record UserStoryDeletedEvent(Guid StoryId, Guid ProjectId) : DomainEvent;
