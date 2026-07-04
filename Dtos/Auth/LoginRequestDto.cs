namespace KorridorX.Dtos.Auth;

public record LoginRequestDto
(
    string Email,
    string Password,
    string? DeviceFingerprint,
    string? DeviceName
);