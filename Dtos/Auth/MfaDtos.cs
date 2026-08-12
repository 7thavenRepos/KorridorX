using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Auth;

public static class LoginStatuses
{
    public const string Authenticated = "Authenticated";
    public const string MfaRequired = "MfaRequired";
    public const string MfaEnrollmentRequired = "MfaEnrollmentRequired";
}

public static class MfaChallengePurposes
{
    public const string Verification = "Verification";
    public const string Enrollment = "Enrollment";
}

public static class MfaCodeTypes
{
    public const string Authenticator = "Authenticator";
    public const string RecoveryCode = "RecoveryCode";
}

public sealed record LoginResultDto(
    string Status,
    AuthResponseDto? Authentication,
    MfaChallengeDto? Challenge);

public sealed record MfaChallengeDto(
    Guid ChallengeId,
    string ChallengeToken,
    string Purpose,
    DateTime ExpiresAt,
    int AttemptsRemaining);

public sealed record MfaEnrollmentSetupRequestDto(
    Guid ChallengeId,
    [param: Required, MaxLength(512)] string ChallengeToken);

public sealed record MfaEnrollmentSetupDto(
    string SharedKey,
    string AuthenticatorUri);

public sealed record MfaVerificationRequestDto(
    Guid ChallengeId,
    [param: Required, MaxLength(512)] string ChallengeToken,
    [param: Required, MaxLength(128)] string Code,
    [param: Required, MaxLength(32)] string CodeType);

public sealed record MfaCompletionDto(
    AuthResponseDto Authentication,
    IReadOnlyList<string> RecoveryCodes);

public sealed record MfaStatusDto(
    bool Required,
    bool Enabled,
    int RecoveryCodesRemaining);

public sealed record MfaManagementVerificationDto(
    [param: Required, MaxLength(256)] string CurrentPassword,
    [param: Required, MaxLength(128)] string Code,
    [param: Required, MaxLength(32)] string CodeType);

public sealed record MfaRecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);
