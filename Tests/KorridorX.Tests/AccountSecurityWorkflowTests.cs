using System.Net;
using System.Text.RegularExpressions;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Notifications;
using KorridorX.Models.Enums;
using KorridorX.Services.Notifications;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed partial class AccountSecurityWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public AccountSecurityWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Confirmation_and_password_recovery_are_enumeration_safe_and_revoke_every_token()
    {
        using var client = _fixture.CreateClient();
        var email = $"account-security-{Guid.NewGuid():N}@example.test";
        const string oldPassword = "ReleaseCandidate!123";
        const string newPassword = "ChangedPassword!456";

        using var registerResponse = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequestDto(
                "Security",
                "Tester",
                email,
                oldPassword,
                null,
                "US",
                UserType.Consumer));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerEnvelope = await registerResponse.ReadApiResponseAsync<RegistrationResultDto>();
        var registration = Assert.IsType<RegistrationResultDto>(registerEnvelope.Data);
        Assert.True(registration.EmailConfirmationRequired);
        Assert.Null(registration.Authentication);

        using var unconfirmedLogin = await LoginAsync(client, email, oldPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, unconfirmedLogin.StatusCode);

        using var unknownConfirmationRequest = await client.PostJsonAsync(
            "/api/auth/email-confirmation/request",
            new EmailConfirmationRequestDto($"missing-{Guid.NewGuid():N}@example.test"));
        using var knownConfirmationRequest = await client.PostJsonAsync(
            "/api/auth/email-confirmation/request",
            new EmailConfirmationRequestDto(email));
        await AssertStatusAsync(HttpStatusCode.OK, unknownConfirmationRequest);
        await AssertStatusAsync(HttpStatusCode.OK, knownConfirmationRequest);
        var unknownConfirmationEnvelope =
            await unknownConfirmationRequest.ReadApiResponseAsync<AccountSecurityRequestResultDto>();
        var knownConfirmationEnvelope =
            await knownConfirmationRequest.ReadApiResponseAsync<AccountSecurityRequestResultDto>();
        Assert.Equal(unknownConfirmationEnvelope.Message, knownConfirmationEnvelope.Message);
        Assert.True(unknownConfirmationEnvelope.Data?.Accepted == true);
        Assert.True(knownConfirmationEnvelope.Data?.Accepted == true);

        var confirmationLink = await GetSecurityLinkAsync(
            registration.UserId,
            "Confirm your KorridorX email address");
        var confirmationQuery = ParseFragment(confirmationLink);

        using var confirmationResponse = await client.PostJsonAsync(
            "/api/auth/email-confirmation/confirm",
            new EmailConfirmationDto(
                Guid.Parse(confirmationQuery["userId"].ToString()),
                confirmationQuery["token"].ToString()));
        Assert.Equal(HttpStatusCode.OK, confirmationResponse.StatusCode);

        using var confirmationReplayResponse = await client.PostJsonAsync(
            "/api/auth/email-confirmation/confirm",
            new EmailConfirmationDto(
                Guid.Parse(confirmationQuery["userId"].ToString()),
                confirmationQuery["token"].ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, confirmationReplayResponse.StatusCode);

        using var loginResponse = await LoginAsync(client, email, oldPassword);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginEnvelope = await loginResponse.ReadApiResponseAsync<LoginResultDto>();
        var login = Assert.IsType<LoginResultDto>(loginEnvelope.Data);
        Assert.Equal(LoginStatuses.Authenticated, login.Status);
        var authentication = Assert.IsType<AuthResponseDto>(login.Authentication);
        client.UseBearerToken(authentication.AccessToken);

        using var unknownResetResponse = await client.PostJsonAsync(
            "/api/auth/password-reset/request",
            new PasswordResetRequestDto($"missing-{Guid.NewGuid():N}@example.test"));
        using var knownResetResponse = await client.PostJsonAsync(
            "/api/auth/password-reset/request",
            new PasswordResetRequestDto(email));

        Assert.Equal(HttpStatusCode.OK, unknownResetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, knownResetResponse.StatusCode);
        var unknownEnvelope = await unknownResetResponse.ReadApiResponseAsync<AccountSecurityRequestResultDto>();
        var knownEnvelope = await knownResetResponse.ReadApiResponseAsync<AccountSecurityRequestResultDto>();
        Assert.Equal(unknownEnvelope.Message, knownEnvelope.Message);
        Assert.True(unknownEnvelope.Data?.Accepted == true);
        Assert.True(knownEnvelope.Data?.Accepted == true);

        var resetLink = await GetSecurityLinkAsync(
            registration.UserId,
            "Reset your KorridorX password");
        var resetQuery = ParseFragment(resetLink);
        var resetToken = resetQuery["token"].ToString();
        var resetUserId = Guid.Parse(resetQuery["userId"].ToString());
        Assert.Equal(registration.UserId, resetUserId);

        using var notificationsResponse = await client.GetAsync("/api/notifications?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, notificationsResponse.StatusCode);
        var notificationsEnvelope = await notificationsResponse.ReadApiResponseAsync<List<NotificationMessageDto>>();
        var sensitiveMessages = (notificationsEnvelope.Data ?? [])
            .Where(x => x.RelatedEntityType == NotificationSecurityPolicy.AccountSecurityEntityType)
            .ToList();
        Assert.NotEmpty(sensitiveMessages);
        Assert.All(
            sensitiveMessages,
            x => Assert.Equal(NotificationSecurityPolicy.RedactedBody, x.Body));
        Assert.DoesNotContain(sensitiveMessages, x => x.Body.Contains(resetToken, StringComparison.Ordinal));

        using var resetResponse = await client.PostJsonAsync(
            "/api/auth/password-reset/confirm",
            new PasswordResetConfirmationDto(resetUserId, resetToken, newPassword));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using var invalidatedAccessResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidatedAccessResponse.StatusCode);

        using var revokedRefreshResponse = await client.PostJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequestDto(
                authentication.RefreshToken,
                "rc-device",
                "RC Test Device"));
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefreshResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        using var oldPasswordResponse = await LoginAsync(client, email, oldPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);
        using var newPasswordResponse = await LoginAsync(client, email, newPassword);
        Assert.Equal(HttpStatusCode.OK, newPasswordResponse.StatusCode);

        using var replayResponse = await client.PostJsonAsync(
            "/api/auth/password-reset/confirm",
            new PasswordResetConfirmationDto(resetUserId, resetToken, newPassword));
        Assert.Equal(HttpStatusCode.BadRequest, replayResponse.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.AuditLogs
            .Where(x => x.UserId == registration.UserId)
            .Select(x => x.Action)
            .ToListAsync();
        Assert.Contains("EMAIL_CONFIRMED", actions);
        Assert.Contains("PASSWORD_RESET_REQUESTED", actions);
        Assert.Contains("PASSWORD_RESET_COMPLETED", actions);
    }

    private async Task<Uri> GetSecurityLinkAsync(Guid userId, string subject)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var body = await db.NotificationMessages
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Subject == subject)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Body)
            .FirstAsync();

        var match = HrefRegex().Match(body);
        Assert.True(match.Success, "The queued security email did not contain an action link.");
        return new Uri(WebUtility.HtmlDecode(match.Groups[1].Value));
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password) =>
        client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequestDto(email, password, "rc-device", "RC Test Device"));

    private static async Task AssertStatusAsync(
        HttpStatusCode expected,
        HttpResponseMessage response)
    {
        var body = expected == response.StatusCode
            ? ""
            : await response.Content.ReadAsStringAsync();
        Assert.True(
            expected == response.StatusCode,
            $"Expected {(int)expected} ({expected}), got " +
            $"{(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
    }

    private static Dictionary<string, Microsoft.Extensions.Primitives.StringValues> ParseFragment(
        Uri link) =>
        QueryHelpers.ParseQuery($"?{link.Fragment.TrimStart('#')}");

    [GeneratedRegex("href=\\\"([^\\\"]+)\\\"", RegexOptions.CultureInvariant)]
    private static partial Regex HrefRegex();
}
