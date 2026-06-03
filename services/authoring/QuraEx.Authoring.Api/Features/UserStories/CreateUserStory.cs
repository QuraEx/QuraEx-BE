using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using QuraEx.Authoring.Domain.Entities;
using QuraEx.Authoring.Infrastructure;
using QuraEx.BuildingBlocks;
using QuraEx.BuildingBlocks.Auth;

namespace QuraEx.Authoring.Api.Features.UserStories;

// ── Command ─────────────────────────────────────────────────────────────────
public sealed record CreateUserStoryCommand(
    Guid ProjectId,
    string Title,
    string? AsA,
    string? IWantTo,
    string? SoThat,
    string? Description) : IRequest<Result<UserStoryResponse>>;

// ── Validator ────────────────────────────────────────────────────────────────
public sealed class CreateUserStoryValidator : AbstractValidator<CreateUserStoryCommand>
{
    public CreateUserStoryValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────
public sealed class CreateUserStoryHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateUserStoryCommand, Result<UserStoryResponse>>
{
    public async Task<Result<UserStoryResponse>> Handle(
        CreateUserStoryCommand cmd,
        CancellationToken ct)
    {
        var story = UserStory.Create(
            cmd.ProjectId, cmd.Title, currentUser.UserId,
            cmd.AsA, cmd.IWantTo, cmd.SoThat, cmd.Description);

        db.UserStories.Add(story);
        await db.SaveChangesAsync(ct);

        return Result.Success(UserStoryResponse.From(story));
    }
}

// ── Endpoint (registered in UserStoryEndpoints.cs) ───────────────────────────
