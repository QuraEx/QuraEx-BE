using System.Net;
using FluentAssertions;
using Xunit;

namespace QuraEx.Workspace.Tests.Integration;

public sealed class HealthEndpointTests(WorkspaceApiFactory factory)
    : IClassFixture<WorkspaceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
