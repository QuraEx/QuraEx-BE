using MediatR;
using QuraEx.Authoring.Domain.Entities;

namespace QuraEx.Authoring.Api.Features.UserStories;

// ── Shared DTO ───────────────────────────────────────────────────────────────
public sealed record UserStoryResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? AsA,
    string? IWantTo,
    string? SoThat,
    string? Description,
    string Status,
    string? ExternalRef,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static UserStoryResponse From(UserStory s) => new(
        s.Id, s.ProjectId, s.Title, s.AsA, s.IWantTo, s.SoThat, s.Description,
        s.Status.ToString(), s.ExternalRef, s.CreatedAt, s.UpdatedAt);
}

// ── Endpoint Registration ────────────────────────────────────────────────────
public static class UserStoryEndpointsRegistration
{
    public static IEndpointRouteBuilder MapUserStoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/user-stories").RequireAuthorization();

        group.MapGet("/", async (Guid projectId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetUserStoriesQuery(projectId), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        group.MapPost("/", async (CreateUserStoryCommand cmd, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(cmd, ct);
            return result.IsSuccess
                ? Results.Created($"/api/user-stories/{result.Value!.Id}", result.Value)
                : Results.Problem(result.Error.Description);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserStoryRequest req, IMediator mediator, CancellationToken ct) =>
        {
            var cmd = new UpdateUserStoryCommand(id, req.Title, req.AsA, req.IWantTo, req.SoThat, req.Description);
            var result = await mediator.Send(cmd, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Description);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeleteUserStoryCommand(id), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Description);
        });

        return app;
    }
}

public sealed record UpdateUserStoryRequest(
    string Title, string? AsA, string? IWantTo, string? SoThat, string? Description);
