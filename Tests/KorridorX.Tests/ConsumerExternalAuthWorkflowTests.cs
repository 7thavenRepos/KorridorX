using System.IdentityModel.Tokens.Jwt;
using System.Net;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Auth;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;

namespace KorridorX.Tests;

// These disposable-database cases test orchestration with a deterministic provider
// verifier. Signature/JWKS validation is covered separately with real RSA tokens.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ConsumerExternalAuthWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    private const string Password = "ReleaseCandidate!123";
    private WebApplicationFactory<Program> Factory() => fixture.Factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConsumerExternalAuth:Google:Enabled"] = "true",
            ["ConsumerExternalAuth:Google:ClientIds:0"] = "owned.apps.googleusercontent.com"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExternalProviderTokenVerifier>();
            services.AddSingleton<IExternalProviderTokenVerifier, WorkflowVerifier>();
        });
    });

    private static async Task<AuthResponseDto> Account(HttpClient client, IServiceProvider services)
    {
        var (_, logged) = await client.RegisterConfirmAndLoginAsync(services, new RegisterRequestDto("External", "Consumer",
            $"external-{Guid.NewGuid():N}@example.test", Password, null, "US", UserType.Consumer));
        client.UseBearerToken(logged.AccessToken); return logged;
    }

    private static async Task<ConsumerExternalChallengeDto> Challenge(HttpClient client, string path)
    {
        var response = await client.PostJsonAsync(path, new ConsumerExternalChallengeRequestDto("google"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ConsumerExternalChallengeDto>((await response.ReadApiResponseAsync<ConsumerExternalChallengeDto>()).Data);
    }
    private static ConsumerExternalLinkRequestDto LinkProof(ConsumerExternalChallengeDto challenge, string subject, string password = Password) =>
        new("google", challenge.ChallengeToken, subject + "|" + challenge.Nonce, password, null, null);

    [DatabaseIntegrationFact]
    public async Task Linking_requires_current_password_and_preserves_the_account_on_failure()
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        await Account(client, factory.Services); var proof = await Challenge(client, "/api/auth/external/links/challenge");
        var response = await client.PostJsonAsync("/api/auth/external/links", LinkProof(proof, Guid.NewGuid().ToString("N"), "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var links = await client.GetAsync("/api/auth/external/links");
        Assert.Empty((await links.ReadApiResponseAsync<List<ConsumerExternalLinkedProviderDto>>()).Data!);
    }

    [DatabaseIntegrationFact]
    public async Task Successful_link_revokes_sessions_then_provider_signin_and_refresh_keep_federated_method()
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        var account = await Account(client, factory.Services); var subject = Guid.NewGuid().ToString("N");
        var link = await Challenge(client, "/api/auth/external/links/challenge");
        var linked = await client.PostJsonAsync("/api/auth/external/links", LinkProof(link, subject));
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.True((await linked.ReadApiResponseAsync<ConsumerExternalLinkResultDto>()).Data!.SignInRequired);
        var oldSession = await client.PostJsonAsync("/api/auth/refresh-token", new RefreshTokenRequestDto(account.RefreshToken, "old", "old"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        var login = await Challenge(client, "/api/auth/external/challenge");
        var request = new ConsumerExternalLoginRequestDto("google", login.ChallengeToken, subject + "|" + login.Nonce, "external-device", "External device");
        var response = await client.PostJsonAsync("/api/auth/external/login", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authenticated = (await response.ReadApiResponseAsync<LoginResultDto>()).Data!.Authentication!;
        Assert.Equal(account.UserId, authenticated.UserId);
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(authenticated.AccessToken).Claims, x => x.Type == "amr" && x.Value == "federated");
        Assert.DoesNotContain(new JwtSecurityTokenHandler().ReadJwtToken(authenticated.AccessToken).Claims, x => x.Type == "amr" && x.Value == "pwd");
        var repeated = await client.PostJsonAsync("/api/auth/external/login", request);
        Assert.Equal(HttpStatusCode.Unauthorized, repeated.StatusCode);
        var refresh = await client.PostJsonAsync("/api/auth/refresh-token", new RefreshTokenRequestDto(authenticated.RefreshToken, "external-device", "External device"));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = (await refresh.ReadApiResponseAsync<AuthResponseDto>()).Data!;
        Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(refreshed.AccessToken).Claims, x => x.Type == "amr" && x.Value == "federated");
    }

    [DatabaseIntegrationFact]
    public async Task Unlinked_identity_does_not_create_or_email_merge_an_account()
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        await Account(client, factory.Services);
        var challenge = await Challenge(client, "/api/auth/external/challenge");
        var response = await client.PostJsonAsync("/api/auth/external/login", new ConsumerExternalLoginRequestDto("google", challenge.ChallengeToken,
            "unlinked-" + Guid.NewGuid().ToString("N") + "|" + challenge.Nonce, null, null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var value = await response.ReadApiResponseAsync<object>();
        Assert.Equal("EXTERNAL_ACCOUNT_LINK_REQUIRED", value.Error!.Code);
    }

    [DatabaseIntegrationFact]
    public async Task Linked_provider_does_not_bypass_enabled_MFA()
    {
        using var factory = Factory(); using var client = factory.CreateClient();
        var account = await Account(client, factory.Services); var subject = Guid.NewGuid().ToString("N");
        var link = await Challenge(client, "/api/auth/external/links/challenge");
        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync("/api/auth/external/links", LinkProof(link, subject))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(account.UserId.ToString()))!;
            Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);
            Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
        }
        var proof = await Challenge(client, "/api/auth/external/challenge");
        var response = await client.PostJsonAsync("/api/auth/external/login", new ConsumerExternalLoginRequestDto("google", proof.ChallengeToken,
            subject + "|" + proof.Nonce, null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.ReadApiResponseAsync<LoginResultDto>()).Data!;
        Assert.Equal(LoginStatuses.MfaRequired, result.Status); Assert.Null(result.Authentication); Assert.NotNull(result.Challenge);
    }

    public sealed class WorkflowVerifier : IExternalProviderTokenVerifier
    {
        public Task<VerifiedExternalIdentity> VerifyAsync(string provider, string idToken, string nonce, CancellationToken ct)
        {
            var parts = idToken.Split('|');
            if (parts.Length != 2 || parts[1] != nonce) throw ExternalIdentityPolicy.InvalidToken();
            return Task.FromResult(new VerifiedExternalIdentity(provider, parts[0]));
        }
    }
}
