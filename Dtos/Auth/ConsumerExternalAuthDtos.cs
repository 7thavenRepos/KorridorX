using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Auth;

public sealed record ConsumerExternalProviderDto(string Provider, bool Enabled);
public sealed record ConsumerExternalChallengeRequestDto([param: Required, MaxLength(16)] string Provider);
public sealed record ConsumerExternalChallengeDto(string Provider, string ChallengeToken, string Nonce, DateTimeOffset ExpiresAt);
public sealed record ConsumerExternalLoginRequestDto(
    [param: Required, MaxLength(16)] string Provider,
    [param: Required, MaxLength(4096)] string ChallengeToken,
    [param: Required, MaxLength(16384)] string IdToken,
    [param: MaxLength(250)] string? DeviceFingerprint,
    [param: MaxLength(250)] string? DeviceName);
public sealed record ConsumerExternalLinkRequestDto(
    [param: Required, MaxLength(16)] string Provider,
    [param: Required, MaxLength(4096)] string ChallengeToken,
    [param: Required, MaxLength(16384)] string IdToken,
    [param: Required, MaxLength(256)] string CurrentPassword,
    [param: MaxLength(128)] string? MfaCode,
    [param: MaxLength(32)] string? MfaCodeType);
public sealed record ConsumerExternalLinkedProviderDto(string Provider);
public sealed record ConsumerExternalLinkResultDto(string Provider, bool Linked, bool SignInRequired);
