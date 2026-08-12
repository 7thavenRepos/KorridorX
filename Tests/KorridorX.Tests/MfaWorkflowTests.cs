using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class MfaWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public MfaWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Privileged_login_requires_replay_safe_mfa_and_audited_recovery()
    {
        using var client = _fixture.CreateClient();
        var email = $"mfa-business-{Guid.NewGuid():N}@example.test";
        const string password = "ReleaseCandidate!123";

        using var registerResponse = await client.PostJsonAsync(
            "/api/auth/register",
            new RegisterRequestDto(
                "MFA",
                "Business",
                email,
                password,
                null,
                "US",
                UserType.Business));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerEnvelope = await registerResponse.ReadApiResponseAsync<RegistrationResultDto>();
        var registration = Assert.IsType<RegistrationResultDto>(registerEnvelope.Data);
        Assert.True(registration.EmailConfirmationRequired);
        Assert.Null(registration.Authentication);

        await ConfirmEmailAsync(registration.UserId);

        var enrollmentLogin = await LoginAsync(client, email, password);
        Assert.Equal(LoginStatuses.MfaEnrollmentRequired, enrollmentLogin.Status);
        var enrollmentChallenge = Assert.IsType<MfaChallengeDto>(enrollmentLogin.Challenge);
        Assert.Null(enrollmentLogin.Authentication);

        using var setupResponse = await client.PostJsonAsync(
            "/api/auth/mfa/enrollment/setup",
            new MfaEnrollmentSetupRequestDto(
                enrollmentChallenge.ChallengeId,
                enrollmentChallenge.ChallengeToken));
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        var setupEnvelope = await setupResponse.ReadApiResponseAsync<MfaEnrollmentSetupDto>();
        var setup = Assert.IsType<MfaEnrollmentSetupDto>(setupEnvelope.Data);
        Assert.NotEmpty(setup.SharedKey);
        Assert.StartsWith("otpauth://totp/", setup.AuthenticatorUri, StringComparison.Ordinal);

        var authenticatorCode = await GenerateAuthenticatorCodeAsync(registration.UserId);
        using var enrollmentResponse = await client.PostJsonAsync(
            "/api/auth/mfa/enrollment/confirm",
            new MfaVerificationRequestDto(
                enrollmentChallenge.ChallengeId,
                enrollmentChallenge.ChallengeToken,
                authenticatorCode,
                MfaCodeTypes.Authenticator));
        await enrollmentResponse.EnsureSuccessWithBodyAsync();
        var enrollmentEnvelope = await enrollmentResponse.ReadApiResponseAsync<MfaCompletionDto>();
        var enrollment = Assert.IsType<MfaCompletionDto>(enrollmentEnvelope.Data);
        Assert.InRange(enrollment.RecoveryCodes.Count, 8, 10);

        using var enrollmentReplay = await client.PostJsonAsync(
            "/api/auth/mfa/enrollment/confirm",
            new MfaVerificationRequestDto(
                enrollmentChallenge.ChallengeId,
                enrollmentChallenge.ChallengeToken,
                authenticatorCode,
                MfaCodeTypes.Authenticator));
        Assert.Equal(HttpStatusCode.Unauthorized, enrollmentReplay.StatusCode);

        var verificationLogin = await LoginAsync(client, email, password);
        Assert.Equal(LoginStatuses.MfaRequired, verificationLogin.Status);
        var verificationChallenge = Assert.IsType<MfaChallengeDto>(verificationLogin.Challenge);

        using var codeReplay = await client.PostJsonAsync(
            "/api/auth/mfa/challenge/verify",
            new MfaVerificationRequestDto(
                verificationChallenge.ChallengeId,
                verificationChallenge.ChallengeToken,
                authenticatorCode,
                MfaCodeTypes.Authenticator));
        Assert.Equal(HttpStatusCode.Unauthorized, codeReplay.StatusCode);

        using var recoveryLoginResponse = await client.PostJsonAsync(
            "/api/auth/mfa/challenge/verify",
            new MfaVerificationRequestDto(
                verificationChallenge.ChallengeId,
                verificationChallenge.ChallengeToken,
                enrollment.RecoveryCodes[0],
                MfaCodeTypes.RecoveryCode));
        Assert.Equal(HttpStatusCode.OK, recoveryLoginResponse.StatusCode);
        var recoveryLoginEnvelope = await recoveryLoginResponse.ReadApiResponseAsync<MfaCompletionDto>();
        var recoveryLogin = Assert.IsType<MfaCompletionDto>(recoveryLoginEnvelope.Data);
        client.UseBearerToken(recoveryLogin.Authentication.AccessToken);

        using var statusResponse = await client.GetAsync("/api/auth/mfa/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusEnvelope = await statusResponse.ReadApiResponseAsync<MfaStatusDto>();
        var status = Assert.IsType<MfaStatusDto>(statusEnvelope.Data);
        Assert.True(status.Required);
        Assert.True(status.Enabled);
        Assert.Equal(enrollment.RecoveryCodes.Count - 1, status.RecoveryCodesRemaining);

        using var regenerateResponse = await client.PostJsonAsync(
            "/api/auth/mfa/recovery-codes/regenerate",
            new MfaManagementVerificationDto(
                password,
                enrollment.RecoveryCodes[1],
                MfaCodeTypes.RecoveryCode));
        Assert.Equal(HttpStatusCode.OK, regenerateResponse.StatusCode);
        var regenerateEnvelope = await regenerateResponse.ReadApiResponseAsync<MfaRecoveryCodesDto>();
        var regenerated = Assert.IsType<MfaRecoveryCodesDto>(regenerateEnvelope.Data);
        Assert.InRange(regenerated.RecoveryCodes.Count, 8, 10);

        using var resetResponse = await client.PostJsonAsync(
            "/api/auth/mfa/reset",
            new MfaManagementVerificationDto(
                password,
                regenerated.RecoveryCodes[0],
                MfaCodeTypes.RecoveryCode));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        using var invalidatedAccess = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalidatedAccess.StatusCode);

        using var invalidatedRefresh = await client.PostJsonAsync(
            "/api/auth/refresh-token",
            new RefreshTokenRequestDto(
                recoveryLogin.Authentication.RefreshToken,
                "rc-mfa-device",
                "RC MFA Device"));
        Assert.Equal(HttpStatusCode.Unauthorized, invalidatedRefresh.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var reenrollmentLogin = await LoginAsync(client, email, password);
        Assert.Equal(LoginStatuses.MfaEnrollmentRequired, reenrollmentLogin.Status);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actions = await db.AuditLogs
            .Where(x => x.UserId == registration.UserId)
            .Select(x => x.Action)
            .ToListAsync();
        Assert.Contains("MFA_ENROLLED", actions);
        Assert.Contains("MFA_RECOVERY_CODE_USED", actions);
        Assert.Contains("MFA_RECOVERY_CODES_REGENERATED", actions);
        Assert.Contains("MFA_RESET", actions);
    }

    private async Task ConfirmEmailAsync(Guid userId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Registered user was not found.");
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var result = await userManager.ConfirmEmailAsync(user, token);
        Assert.True(result.Succeeded);
    }

    private async Task<string> GenerateAuthenticatorCodeAsync(Guid userId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Registered user was not found.");
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Registered user has no authenticator key.");

        return GenerateTotp(key, DateTimeOffset.UtcNow);
    }

    private static string GenerateTotp(string base32Key, DateTimeOffset timestamp)
    {
        var secret = DecodeBase32(base32Key);
        var counter = (ulong)(timestamp.ToUnixTimeSeconds() / 30);
        Span<byte> counterBytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(counterBytes, counter);

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];

        return (binaryCode % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var normalized = value
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .TrimEnd('=')
            .ToUpperInvariant();
        var result = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var character in normalized)
        {
            var digit = alphabet.IndexOf(character);
            if (digit < 0)
                throw new InvalidOperationException("Authenticator key is not valid Base32.");

            buffer = (buffer << 5) | digit;
            bitsInBuffer += 5;

            if (bitsInBuffer < 8)
                continue;

            bitsInBuffer -= 8;
            result.Add((byte)(buffer >> bitsInBuffer));
            buffer &= (1 << bitsInBuffer) - 1;
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Authenticator key is empty.");

        return result.ToArray();
    }

    private static async Task<LoginResultDto> LoginAsync(
        HttpClient client,
        string email,
        string password)
    {
        using var response = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequestDto(
                email,
                password,
                "rc-mfa-device",
                "RC MFA Device"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.ReadApiResponseAsync<LoginResultDto>();
        return Assert.IsType<LoginResultDto>(envelope.Data);
    }
}
