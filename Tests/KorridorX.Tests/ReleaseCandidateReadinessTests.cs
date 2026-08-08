using System.Net;
using KorridorX.Tests.Infrastructure;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ReleaseCandidateReadinessTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ReleaseCandidateReadinessTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Readiness_endpoint_is_healthy_after_migrations_are_applied()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
}
