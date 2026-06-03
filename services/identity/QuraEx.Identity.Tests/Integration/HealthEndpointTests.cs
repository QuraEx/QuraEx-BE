using System.Net;
using FluentAssertions;
using Xunit;

namespace QuraEx.Identity.Tests.Integration;

public sealed class HealthEndpointTests(IdentityApiFactory factory)
    : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
