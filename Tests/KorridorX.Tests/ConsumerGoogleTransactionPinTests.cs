using System.Net;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Auth;
using KorridorX.Services.Security;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Identity;

namespace KorridorX.Tests;

// Executed only with the disposable PostgreSQL integration-test environment.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ConsumerGoogleTransactionPinTests(ReleaseCandidateDatabaseFixture fixture)
{
    private const string Password = "ReleaseCandidate!123";

    private WebApplicationFactory<Program> Factory() => fixture.Factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConsumerExternalAuth:Google:Enabled"] = "true",
            ["ConsumerExternalAuth:Google:ClientIds:0"] = "owned.apps.googleusercontent.com"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExternalProviderTokenVerifier>();
            services.AddSingleton<IExternalProviderTokenVerifier, FakeVerifier>();
        });
    });

    [DatabaseIntegrationFact]
    public async Task Linked_passwordless_consumer_can_setup_PIN_using_fresh_Google_proof_but_cannot_replay_it()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var (_, original) = await client.RegisterConfirmAndLoginAsync(factory.Services,
            new RegisterRequestDto("Google", "Pin", $"google-pin-{Guid.NewGuid():N}@example.test",
                Password, null, "CA", UserType.Consumer));
        client.UseBearerToken(original.AccessToken);
        var subject = Guid.NewGuid().ToString("N");
        var linkChallenge = await GetChallenge(client, "/api/auth/external/links/challenge");
        using (var link = await client.PostJsonAsync("/api/auth/external/links",
            new ConsumerExternalLinkRequestDto("google", linkChallenge.ChallengeToken,
                subject + "|" + linkChallenge.Nonce, Password, null, null)))
            Assert.Equal(HttpStatusCode.OK, link.StatusCode);

        // Simulate a truly social-only consumer with no local password.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(original.UserId.ToString()))!;
            Assert.True((await users.RemovePasswordAsync(user)).Succeeded);
            Assert.False(await users.HasPasswordAsync(user));
        }

        var signIn = await GetChallenge(client, "/api/auth/external/challenge");
        using var loginResponse = await client.PostJsonAsync("/api/auth/external/login",
            new ConsumerExternalLoginRequestDto("google", signIn.ChallengeToken,
                subject + "|" + signIn.Nonce, null, null));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = (await loginResponse.ReadApiResponseAsync<LoginResultDto>()).Data!;
        Assert.Equal(LoginStatuses.Authenticated, login.Status);
        client.UseBearerToken(login.Authentication!.AccessToken);

        var initial = await client.GetAsync("/api/auth/transaction-pin");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var state = (await initial.ReadApiResponseAsync<TransactionPinStatusDto>()).Data!;
        Assert.True(state.CanSetWithGoogle);
        Assert.False(state.IsConfigured);

        var challenge = await GetChallenge(client, "/api/auth/transaction-pin/google/challenge");
        var wrongIdentity = new SetGoogleTransactionPinRequestDto(challenge.ChallengeToken,
            Guid.NewGuid().ToString("N") + "|" + challenge.Nonce, "4567");
        using (var wrong = await client.PutJsonAsync("/api/auth/transaction-pin/google", wrongIdentity))
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var correct = new SetGoogleTransactionPinRequestDto(challenge.ChallengeToken,
            subject + "|" + challenge.Nonce, "4567");
        using (var good = await client.PutJsonAsync("/api/auth/transaction-pin/google", correct))
        {
            Assert.Equal(HttpStatusCode.OK, good.StatusCode);
            Assert.True((await good.ReadApiResponseAsync<TransactionPinStatusDto>()).Data!.IsConfigured);
        }
        using (var replay = await client.PutJsonAsync("/api/auth/transaction-pin/google", correct))
            Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        await verifyScope.ServiceProvider.GetRequiredService<ITransactionPinService>()
            .VerifyForTransactionAsync(original.UserId, "4567");
        // Existing passwordless accounts cannot use the password-based route.
        using var passwordRoute = await client.PutJsonAsync("/api/auth/transaction-pin",
            new SetTransactionPinRequestDto("", "1234"));
        Assert.NotEqual(HttpStatusCode.OK, passwordRoute.StatusCode);
    }

    [DatabaseIntegrationFact]
    public async Task Unlinked_consumer_cannot_issue_Google_PIN_proof()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var (_, logged) = await client.RegisterConfirmAndLoginAsync(factory.Services,
            new RegisterRequestDto("No", "Link", $"unlinked-{Guid.NewGuid():N}@example.test",
                Password, null, "US", UserType.Consumer));
        client.UseBearerToken(logged.AccessToken);
        var statusResponse = await client.GetAsync("/api/auth/transaction-pin");
        Assert.False((await statusResponse.ReadApiResponseAsync<TransactionPinStatusDto>()).Data!.CanSetWithGoogle);
        var challenge = await client.PostJsonAsync("/api/auth/transaction-pin/google/challenge", new { });
        Assert.NotEqual(HttpStatusCode.OK, challenge.StatusCode);
    }

    private static async Task<ConsumerExternalChallengeDto> GetChallenge(HttpClient client, string path)
    {
        using var response = path.Contains("external", StringComparison.Ordinal)
            ? await client.PostJsonAsync(path, new ConsumerExternalChallengeRequestDto("google"))
            : await client.PostJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ConsumerExternalChallengeDto>(
            (await response.ReadApiResponseAsync<ConsumerExternalChallengeDto>()).Data);
    }

    // Signature/JWKS enforcement is independently covered by real RSA tests.
    private sealed class FakeVerifier : IExternalProviderTokenVerifier
    {
        public Task<VerifiedExternalIdentity> VerifyAsync(string provider, string idToken,
            string nonce, CancellationToken ct)
        {
            var parts = idToken.Split('|');
            if (provider != "google" || parts.Length != 2 || parts[1] != nonce)
                throw ExternalIdentityPolicy.InvalidToken();
            return Task.FromResult(new VerifiedExternalIdentity(provider, parts[0]));
        }
    }
}
