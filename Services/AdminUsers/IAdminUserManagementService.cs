using KorridorX.Dtos.AdminUsers;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.AdminUsers;

public interface IAdminUserManagementService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(
        string? search,
        UserStatus? status,
        UserType? userType,
        string? role,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AdminUserDetailsDto> GetUserAsync(
        Guid userId,
        CancellationToken ct = default);

    IReadOnlyList<AdminRoleOptionDto> GetAssignableRoles();

    Task<AdminUserDetailsDto> CreateInternalUserAsync(
        CreateAdminUserRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<AdminUserDetailsDto> UpdateStatusAsync(
        Guid userId,
        UpdateAdminUserStatusRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default);

    Task<AdminUserDetailsDto> UpdateRolesAsync(
        Guid userId,
        UpdateAdminUserRolesRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default);
}