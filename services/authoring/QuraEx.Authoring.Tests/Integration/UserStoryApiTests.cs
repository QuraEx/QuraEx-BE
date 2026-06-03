using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using QuraEx.Authoring.Contracts;
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

        var harness = factory.Services.GetRequiredService<ITestHarness>();
        var published = await harness.Published
            .SelectAsync<StoryChangedEvent>(x => x.Context.Message.StoryId == body.Id)
            .FirstOrDefault();

        published.Should().NotBeNull();
        published!.Context.Message.ProjectId.Should().Be(projectId);
        published.Context.Message.Title.Should().Be(body.Title);
        published.Context.Message.ChangeType.Should().Be("CREATED");
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

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUserStory_ReturnsUpdatedBody()
    {
        Authorize();
        var projectId = Guid.NewGuid();

        var createResp = await _client.PostAsJsonAsync("/api/user-stories", new
        {
            projectId,
            title = "Original title",
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<UserStoryDto>();

        var updateResp = await _client.PutAsJsonAsync($"/api/user-stories/{created!.Id}", new
        {
            title = "Updated title",
            asA = "qa",
            iWantTo = "edit the story",
            soThat = "requirements stay current",
            description = "Updated description",
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<UserStoryDto>();
        updated.Should().NotBeNull();
        updated!.Id.Should().Be(created.Id);
        updated.Title.Should().Be("Updated title");
        updated.AsA.Should().Be("qa");
    }

    [Fact]
    public async Task DeleteUserStory_RemovesStoryFromList()
    {
        Authorize();
        var projectId = Guid.NewGuid();

        var createResp = await _client.PostAsJsonAsync("/api/user-stories", new
        {
            projectId,
            title = "Story to delete",
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<UserStoryDto>();

        var deleteResp = await _client.DeleteAsync($"/api/user-stories/{created!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResp = await _client.GetAsync($"/api/user-stories?projectId={projectId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await getResp.Content.ReadFromJsonAsync<List<UserStoryDto>>();
        list.Should().NotContain(s => s.Id == created.Id);
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
