using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace QuraEx.Authoring.Tests.Integration;

public sealed class UserStoryApiTests(AuthoringApiFactory factory)
    : IClassFixture<AuthoringApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _userId = Guid.NewGuid();

    private void Authorize() =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateTestJwt(_userId));

    [Fact]
    public async Task CreateUserStory_Returns201WithBody()
    {
        Authorize();
        var projectId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync("/api/user-stories", new
        {
            projectId,
            title = "As a user I can create stories",
            asA = "developer",
            iWantTo = "create user stories via the API",
            soThat = "the team can track requirements",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<UserStoryDto>();
        body.Should().NotBeNull();
        body!.Title.Should().Be("As a user I can create stories");
        body.ProjectId.Should().Be(projectId);
        body.Status.Should().Be("DRAFT");
    }

    [Fact]
    public async Task GetUserStories_ReturnsCreatedStory()
    {
        Authorize();
        var projectId = Guid.NewGuid();

        // Create
        var createResp = await _client.PostAsJsonAsync("/api/user-stories", new
        {
            projectId,
            title = "Story for GET test",
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<UserStoryDto>();

        // List
        var getResp = await _client.GetAsync($"/api/user-stories?projectId={projectId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await getResp.Content.ReadFromJsonAsync<List<UserStoryDto>>();
        list.Should().ContainSingle(s => s.Id == created!.Id);
    }

    [Fact]
    public async Task CreateUserStory_Without_Auth_Returns401()
    {
        // No Authorization header
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PostAsJsonAsync("/api/user-stories", new
        {
            projectId = Guid.NewGuid(),
            title = "Unauthorized attempt",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private sealed record UserStoryDto(
        Guid Id,
        Guid ProjectId,
        string Title,
        string Status,
        string? AsA,
        string? IWantTo,
        string? SoThat);
}
