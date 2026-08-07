namespace KorridorX.Dtos.Auth;

public record UserSessionDto(
    Guid Id,
    string? DeviceName,
    string? DeviceFingerprint,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastUsedAt,
    bool IsActive,
    DateTime? RevokedAt,
    string? RevokedReason);

public record RevokeAllSessionsRequestDto(
    string? Reason = null);
