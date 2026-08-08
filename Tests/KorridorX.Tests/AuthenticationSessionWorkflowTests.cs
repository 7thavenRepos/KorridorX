using System.Net;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Tests.Infrastructure;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class AuthenticationSessionWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public AuthenticationSessionWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Refresh_token_rotation_detects_reuse_and_revokes_active_sessions()
    {
        using var client = _fixture.CreateClient();
        var email = $"session-{Guid.NewGuid():N}@example.test";

        var registerResponse = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequestDto(
                "Session",
                "Tester",
                email,
                "ReleaseCandidate!123",
                null,
                "US",
                UserType.Consumer));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerEnvelope = await registerResponse.ReadApiResponseAsync<AuthResponseDto>();
        var registered = Assert.IsType<AuthResponseDto>(registerEnvelope.Data);

        var refreshResponse = await client.PostJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequestDto(
                registered.RefreshToken,
                "rc-device",
                "RC Test Device"));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshEnvelope = await refreshResponse.ReadApiResponseAsync<AuthResponseDto>();
        var refreshed = Assert.IsType<AuthResponseDto>(refreshEnvelope.Data);
        Assert.NotEqual(registered.RefreshToken, refreshed.RefreshToken);

        var reuseResponse = await client.PostJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequestDto(
                registered.RefreshToken,
                "rc-device",
                "RC Test Device"));

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        client.UseBearerToken(refreshed.AccessToken);
        var sessionsResponse = await client.GetAsync("/api/auth/sessions");
        Assert.Equal(HttpStatusCode.OK, sessionsResponse.StatusCode);
        var sessionsEnvelope = await sessionsResponse.ReadApiResponseAsync<List<UserSessionDto>>();
        Assert.NotNull(sessionsEnvelope.Data);
        Assert.DoesNotContain(sessionsEnvelope.Data!, x => x.IsActive);
    }
}
