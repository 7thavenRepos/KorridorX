using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Auth;

public record ChannelRegistrationRequestDto
(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? PhoneNumber,
    string? CountryCode
);

// Integration-test input retained outside the public controller contract. Public
// callers cannot select UserType; the channel-specific endpoint assigns it.
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
