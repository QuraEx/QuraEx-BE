using MediatR;
using Microsoft.EntityFrameworkCore;
using QuraEx.Authoring.Domain.Entities;
using QuraEx.Authoring.Infrastructure;
using QuraEx.BuildingBlocks;
using QuraEx.BuildingBlocks.Exceptions;

namespace QuraEx.Authoring.Api.Features.UserStories;

public sealed record DeleteUserStoryCommand(Guid StoryId) : IRequest<Result>;

public sealed class DeleteUserStoryHandler(AppDbContext db)
    : IRequestHandler<DeleteUserStoryCommand, Result>
{
    public async Task<Result> Handle(DeleteUserStoryCommand cmd, CancellationToken ct)
    {
        var story = await db.UserStories.FirstOrDefaultAsync(s => s.Id == cmd.StoryId, ct)
            ?? throw new NotFoundException(nameof(UserStory), cmd.StoryId);

        story.SoftDelete();
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
