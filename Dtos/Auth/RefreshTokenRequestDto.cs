namespace KorridorX.Dtos.Auth;

public record RefreshTokenRequestDto(
    string RefreshToken,
    string? DeviceFingerprint = null,
    string? DeviceName = null);
