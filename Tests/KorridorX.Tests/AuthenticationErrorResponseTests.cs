using System.Net;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Tests.Infrastructure;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class AuthenticationErrorResponseTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public AuthenticationErrorResponseTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Wrong_password_returns_swipfx_style_invalid_credentials_envelope()
    {
        using var client = _fixture.CreateClient();

        var email = $"invalid-credentials-{Guid.NewGuid():N}@example.test";
        const string password = "ReleaseCandidate!123";

        await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Invalid",
                "Credentials",
                email,
                password,
                null,
                "CA",
                UserType.Consumer));

        using var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequestDto(
                email,
                "DefinitelyWrong!456",
                "rc-device",
                "RC Test Device"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var envelope = await response.ReadApiResponseAsync<object>();

        Assert.False(envelope.Success);
        Assert.Equal("Invalid email or password.", envelope.Message);
        Assert.NotNull(envelope.Error);
        Assert.Equal("INVALID_CREDENTIALS", envelope.Error!.Code);
        Assert.Equal("Invalid email or password.", envelope.Error.Message);
        Assert.NotNull(envelope.Error.Details);
    }
}
