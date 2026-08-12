using KorridorX.Dtos.Auth;

namespace KorridorX.Services.Auth;

public interface IAuthService
{
    Task<RegistrationResultDto> RegisterAsync(
        RegisterRequestDto request,
        string? ipAddress,
        CancellationToken ct = default);

    Task<LoginResultDto> LoginAsync(
        LoginRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task<MfaEnrollmentSetupDto> GetMfaEnrollmentSetupAsync(
        MfaEnrollmentSetupRequestDto request,
        CancellationToken ct = default);

    Task<MfaCompletionDto> ConfirmMfaEnrollmentAsync(
        MfaVerificationRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task<MfaCompletionDto> VerifyMfaChallengeAsync(
        MfaVerificationRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task<AuthResponseDto> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    Task<CurrentUserDto> GetCurrentUserAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<MfaStatusDto> GetMfaStatusAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<MfaRecoveryCodesDto> RegenerateMfaRecoveryCodesAsync(
        Guid userId,
        MfaManagementVerificationDto request,
        CancellationToken ct = default);

    Task ResetMfaAsync(
        Guid userId,
        MfaManagementVerificationDto request,
        string? ipAddress,
        CancellationToken ct = default);

    Task<IReadOnlyList<UserSessionDto>> GetSessionsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        string? ipAddress,
        string? reason,
        CancellationToken ct = default);

    Task<int> RevokeAllSessionsAsync(
        Guid userId,
        string? ipAddress,
        string? reason,
        CancellationToken ct = default);

    Task RequestPasswordResetAsync(
        PasswordResetRequestDto request,
        CancellationToken ct = default);

    Task ConfirmPasswordResetAsync(
        PasswordResetConfirmationDto request,
        string? ipAddress,
        CancellationToken ct = default);

    Task RequestEmailConfirmationAsync(
        EmailConfirmationRequestDto request,
        CancellationToken ct = default);

    Task ConfirmEmailAsync(
        EmailConfirmationDto request,
        CancellationToken ct = default);
}
