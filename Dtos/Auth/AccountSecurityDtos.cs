using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Auth;

public sealed record RegistrationResultDto(
    Guid UserId,
    string Email,
    bool EmailConfirmationRequired,
    AuthResponseDto? Authentication);

public sealed record AccountSecurityRequestResultDto(bool Accepted = true);

public sealed record AccountSecurityActionResultDto(bool Succeeded = true);

public sealed record PasswordResetRequestDto(
    [property: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record PasswordResetConfirmationDto(
    Guid UserId,
    [property: Required, MaxLength(4096)] string Token,
    [property: Required, MaxLength(256)] string NewPassword);

public sealed record EmailConfirmationRequestDto(
    [property: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record EmailConfirmationDto(
    Guid UserId,
    [property: Required, MaxLength(4096)] string Token);
