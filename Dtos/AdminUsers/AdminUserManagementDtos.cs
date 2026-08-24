using KorridorX.Models.Enums;

namespace KorridorX.Dtos.AdminUsers;

public sealed class AdminUserListItemDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string? PhoneNumber { get; init; }
    public string? CountryCode { get; init; }
    public UserType UserType { get; init; }
    public UserStatus Status { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool MfaEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public List<string> Roles { get; set; } = [];
}

public sealed record AdminUserDetailsDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? CountryCode,
    UserType UserType,
    UserStatus Status,
    bool EmailConfirmed,
    bool MfaEnabled,
    int AccessFailedCount,
    DateTimeOffset? LockoutEnd,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt,
    DateTime? LastLoginAt,
    int ActiveSessionCount,
    IReadOnlyList<string> Roles);

public sealed record CreateAdminUserRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? PhoneNumber,
    string CountryCode,
    IReadOnlyCollection<string> Roles);

public sealed record UpdateAdminUserStatusRequestDto(
    UserStatus Status,
    string Reason);

public sealed record UpdateAdminUserRolesRequestDto(
    IReadOnlyCollection<string> Roles,
    string Reason);

public sealed record AdminRoleOptionDto(
    string Name,
    string Description);