using MassTransit;
using MediatR;
using QuraEx.Authoring.Contracts;
using QuraEx.Authoring.Domain.DomainEvents;

namespace QuraEx.Authoring.Infrastructure.DomainEventHandlers;

public sealed class UserStoryCreatedEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<UserStoryCreatedEvent>
{
    public async Task Handle(UserStoryCreatedEvent notification, CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish(new StoryChangedEvent
        {
            StoryId = notification.StoryId,
            ProjectId = notification.ProjectId,
            Title = notification.Title,
            ChangeType = "CREATED",
        }, cancellationToken);
    }
}
