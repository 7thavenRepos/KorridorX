using System.Net;
using KorridorX.Tests.Infrastructure;

namespace KorridorX.Tests;

public sealed class LivenessEndpointTests : IClassFixture<KorridorXWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LivenessEndpointTests(KorridorXWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });
    }

    [Fact]
    public async Task LiveHealth_ReturnsSuccessAndCorrelationHeader()
    {
        using var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
}
