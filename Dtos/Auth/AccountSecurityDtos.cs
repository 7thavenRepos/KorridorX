using System.ComponentModel.DataAnnotations;

namespace KorridorX.Dtos.Auth;

public sealed record RegistrationResultDto(
    Guid UserId,
    string Email,
    bool EmailConfirmationRequired,
    bool SignInRequired,
    AuthResponseDto? Authentication);

public sealed record AccountSecurityRequestResultDto(bool Accepted = true);

public sealed record AccountSecurityActionResultDto(bool Succeeded = true);

public sealed record PasswordResetRequestDto(
    [param: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record PasswordResetConfirmationDto(
    Guid UserId,
    [param: Required, MaxLength(4096)] string Token,
    [param: Required, MaxLength(256)] string NewPassword);

public sealed record EmailConfirmationRequestDto(
    [param: Required, EmailAddress, MaxLength(256)] string Email);

public sealed record EmailConfirmationDto(
    Guid UserId,
    [param: Required, MaxLength(4096)] string Token);
