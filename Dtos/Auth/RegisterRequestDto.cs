using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Auth;

public record RegisterRequestDto
(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? PhoneNumber,
    string? CountryCode,
    UserType UserType
);