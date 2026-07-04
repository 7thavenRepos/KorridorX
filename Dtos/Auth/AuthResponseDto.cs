namespace KorridorX.Dtos.Auth;

public record AuthResponseDto
(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt
);