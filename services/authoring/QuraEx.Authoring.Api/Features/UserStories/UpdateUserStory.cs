using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuraEx.Authoring.Domain.Entities;
using QuraEx.Authoring.Infrastructure;
using QuraEx.BuildingBlocks;
using QuraEx.BuildingBlocks.Auth;
using QuraEx.BuildingBlocks.Exceptions;

namespace QuraEx.Authoring.Api.Features.UserStories;

public sealed record UpdateUserStoryCommand(
    Guid StoryId,
    string Title,
    string? AsA,
    string? IWantTo,
    string? SoThat,
    string? Description) : IRequest<Result<UserStoryResponse>>;

public sealed class UpdateUserStoryValidator : AbstractValidator<UpdateUserStoryCommand>
{
    public UpdateUserStoryValidator()
    {
        RuleFor(x => x.StoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
    }
}

public sealed class UpdateUserStoryHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateUserStoryCommand, Result<UserStoryResponse>>
{
    public async Task<Result<UserStoryResponse>> Handle(UpdateUserStoryCommand cmd, CancellationToken ct)
    {
        var story = await db.UserStories.FirstOrDefaultAsync(s => s.Id == cmd.StoryId, ct)
            ?? throw new NotFoundException(nameof(UserStory), cmd.StoryId);

        story.Update(cmd.Title, currentUser.UserId, cmd.AsA, cmd.IWantTo, cmd.SoThat, cmd.Description);
        await db.SaveChangesAsync(ct);

        return Result.Success(UserStoryResponse.From(story));
    }
}
