using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Auth;

public record CurrentUserDto
(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? CountryCode,
    UserType UserType,
    UserStatus Status,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed
);
