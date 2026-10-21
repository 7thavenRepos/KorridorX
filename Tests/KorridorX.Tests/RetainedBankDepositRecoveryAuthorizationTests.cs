using System.Net;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Webhooks;
using KorridorX.Services.Auth;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class RetainedBankDepositRecoveryAuthorizationTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Authorized_operator_requests_bind_and_validate_record_bodies_without_server_errors()
    {
        using var client = fixture.CreateClient();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var name = $"recovery-operator-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = name, Email = name, EmailConfirmed = true,
            UserType = UserType.Admin, Status = UserStatus.Active, TwoFactorEnabled = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        var token = await scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessTokenAsync(user, mfaAuthenticated: true);
        client.UseBearerToken(token.Token);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receipt = new WebhookEvent { EventType = "collection.completed", ProcessingStatus = WebhookProcessingStatus.Ignored,
            RawPayloadJson = "{}", ProviderEventId = Guid.NewGuid().ToString() };
        db.WebhookEvents.Add(receipt);
        await db.SaveChangesAsync();
        var root = $"/api/admin/providers/webhooks/{receipt.Id}/bank-deposit-recovery";
        using var emptyReason = await client.PostJsonAsync(root + "/preview", new { reason = "" });
        using var emptyToken = await client.PostJsonAsync(root + "/apply", new { previewToken = "" });
        Assert.Equal(HttpStatusCode.BadRequest, emptyReason.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, emptyToken.StatusCode);
        using var shapedRequest = await client.PostJsonAsync(root + "/preview", new { reason = "Synthetic operator binding check" });
        Assert.Equal(HttpStatusCode.BadRequest, shapedRequest.StatusCode);
        Assert.Contains("Blaaiz is not enabled", await shapedRequest.Content.ReadAsStringAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Anonymous_requests_cannot_preview_or_apply_a_retained_deposit()
    {
        using var client = fixture.CreateClient();
        var root = $"/api/admin/providers/webhooks/{Guid.NewGuid()}/bank-deposit-recovery";
        using var preview = await client.PostJsonAsync(root + "/preview", new { reason = "Investigate retained deposit" });
        using var apply = await client.PostJsonAsync(root + "/apply", new { previewToken = "untrusted-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, apply.StatusCode);
    }

    [DatabaseIntegrationFact]
    public async Task Authenticated_consumers_cannot_preview_or_apply_a_retained_deposit()
    {
        using var client = fixture.CreateClient();
        var (_, authentication) = await client.RegisterConfirmAndLoginAsync(fixture.Factory.Services,
            new RegisterRequestDto("Recovery", "Consumer", $"recovery-consumer-{Guid.NewGuid():N}@example.test",
                "RecoveryAccess!123", null, "CA", UserType.Consumer));
        client.UseBearerToken(authentication.AccessToken);
        var root = $"/api/admin/providers/webhooks/{Guid.NewGuid()}/bank-deposit-recovery";
        using var preview = await client.PostJsonAsync(root + "/preview", new { reason = "Investigate retained deposit" });
        using var apply = await client.PostJsonAsync(root + "/apply", new { previewToken = "untrusted-token" });
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, apply.StatusCode);
    }
}
