using MediatR;
using Microsoft.EntityFrameworkCore;
using QuraEx.Authoring.Infrastructure;
using QuraEx.BuildingBlocks;

namespace QuraEx.Authoring.Api.Features.UserStories;

public sealed record GetUserStoriesQuery(Guid ProjectId) : IRequest<Result<IReadOnlyList<UserStoryResponse>>>;

public sealed class GetUserStoriesHandler(AppDbContext db)
    : IRequestHandler<GetUserStoriesQuery, Result<IReadOnlyList<UserStoryResponse>>>
{
    public async Task<Result<IReadOnlyList<UserStoryResponse>>> Handle(
        GetUserStoriesQuery query,
        CancellationToken ct)
    {
        var stories = await db.UserStories
            .AsNoTracking()
            .Where(s => s.ProjectId == query.ProjectId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => UserStoryResponse.From(s))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<UserStoryResponse>>(stories);
    }
}
