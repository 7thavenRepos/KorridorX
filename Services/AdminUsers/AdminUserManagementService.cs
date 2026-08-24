using KorridorX.Data;
using KorridorX.Data.Seed;
using KorridorX.Dtos.AdminUsers;
using KorridorX.Dtos.Audit;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Audit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.AdminUsers;

public sealed class AdminUserManagementService : IAdminUserManagementService
{
    private static readonly string[] AssignableInternalRoles =
    [
        IdentityRoleNames.Compliance,
        IdentityRoleNames.InternalAudit,
        IdentityRoleNames.Support,
        IdentityRoleNames.Operations,
        IdentityRoleNames.Admin,
        IdentityRoleNames.SuperAdmin
    ];

    private static readonly IReadOnlyDictionary<string, string> RoleDescriptions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [IdentityRoleNames.Compliance] = "Compliance review, screening and regulatory operations.",
            [IdentityRoleNames.InternalAudit] = "Independent internal audit, control review and protective restriction administration.",
            [IdentityRoleNames.Support] = "Customer support and transfer investigation operations.",
            [IdentityRoleNames.Operations] = "Operational, provider, treasury and settlement workflows.",
            [IdentityRoleNames.Admin] = "General administrative access to the operations console.",
            [IdentityRoleNames.SuperAdmin] = "Full administrative control, including internal user and role management."
        };

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public AdminUserManagementService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditService audit)
    {
        _db = db;
        _userManager = userManager;
        _audit = audit;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(
        string? search,
        UserStatus? status,
        UserType? userType,
        string? role,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                (x.Email != null && x.Email.ToLower().Contains(normalizedSearch)) ||
                x.FirstName.ToLower().Contains(normalizedSearch) ||
                x.LastName.ToLower().Contains(normalizedSearch) ||
                ((x.FirstName + " " + x.LastName).ToLower().Contains(normalizedSearch)));
        }

        if (status is not null)
            query = query.Where(x => x.Status == status.Value);

        if (userType is not null)
            query = query.Where(x => x.UserType == userType.Value);

        if (!string.IsNullOrWhiteSpace(role))
        {
            var requestedRole = role.Trim();
            var normalizedRole = requestedRole.ToUpperInvariant();

            var roleId = await _db.Roles
                .AsNoTracking()
                .Where(x => x.NormalizedName == normalizedRole)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(ct);

            if (roleId is null)
            {
                query = query.Where(_ => false);
            }
            else
            {
                query = query.Where(user =>
                    _db.UserRoles.Any(userRole =>
                        userRole.UserId == user.Id &&
                        userRole.RoleId == roleId.Value));
            }
        }

        var result = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Email)
            .Select(x => new AdminUserListItemDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Email = x.Email ?? "",
                PhoneNumber = x.PhoneNumber,
                CountryCode = x.CountryCode,
                UserType = x.UserType,
                Status = x.Status,
                EmailConfirmed = x.EmailConfirmed,
                MfaEnabled = x.TwoFactorEnabled,
                CreatedAt = x.CreatedAt,
                LastLoginAt = _db.LoginHistories
                    .Where(login => login.UserId == x.Id && login.WasSuccessful)
                    .Select(login => (DateTime?)login.OccurredAt)
                    .Max()
            })
            .PaginateAsync(page, pageSize, ct);

        await PopulateRolesAsync(result.Items, ct);
        return result;
    }

    public async Task<AdminUserDetailsDto> GetUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var roles = await GetRolesAsync(userId, ct);
        var now = DateTime.UtcNow;

        var lastLoginAt = await _db.LoginHistories
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.WasSuccessful)
            .Select(x => (DateTime?)x.OccurredAt)
            .MaxAsync(ct);

        var activeSessionCount = await _db.RefreshTokens
            .AsNoTracking()
            .CountAsync(
                x => x.UserId == userId &&
                     !x.IsRevoked &&
                     x.ExpiresAt > now,
                ct);

        return new AdminUserDetailsDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? "",
            user.PhoneNumber,
            user.CountryCode,
            user.UserType,
            user.Status,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            user.AccessFailedCount,
            user.LockoutEnd,
            user.CreatedAt,
            user.LastUpdatedAt,
            lastLoginAt,
            activeSessionCount,
            roles);
    }

    public IReadOnlyList<AdminRoleOptionDto> GetAssignableRoles() =>
        AssignableInternalRoles
            .Select(role => new AdminRoleOptionDto(
                role,
                RoleDescriptions[role]))
            .ToArray();

    public async Task<AdminUserDetailsDto> CreateInternalUserAsync(
        CreateAdminUserRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var firstName = RequiredText(request.FirstName, nameof(request.FirstName), 100);
        var lastName = RequiredText(request.LastName, nameof(request.LastName), 100);
        var email = NormalizeEmail(request.Email);
        var countryCode = NormalizeCountryCode(request.CountryCode);
        var phoneNumber = CleanOptional(request.PhoneNumber, 50);
        var roles = NormalizeAndValidateRoles(request.Roles);

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new InvalidOperationException("Password is required.");

        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
            throw new InvalidOperationException("A user with this email address already exists.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            CountryCode = countryCode,
            UserType = UserType.Admin,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        EnsureIdentitySucceeded(createResult, "Internal user creation failed");

        var roleResult = await _userManager.AddToRolesAsync(user, roles);
        EnsureIdentitySucceeded(roleResult, "Internal user role assignment failed");

        _audit.Stage(new AuditRecordRequest(
            Action: "ADMIN_USER_CREATED",
            Category: "IdentityAdministration",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            NewValues: new
            {
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.CountryCode,
                user.UserType,
                user.Status,
                Roles = roles
            },
            Metadata: new
            {
                ActorUserId = actorUserId,
                EmailConfirmedAtCreation = true,
                PrivilegedMfaEnrollmentRequiredAtLogin = true
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetUserAsync(user.Id, ct);
    }

    public async Task<AdminUserDetailsDto> UpdateStatusAsync(
        Guid userId,
        UpdateAdminUserStatusRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(UserStatus), request.Status))
            throw new InvalidOperationException("The requested user status is invalid.");

        var reason = RequiredText(request.Reason, nameof(request.Reason), 500);

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        if (user.Id == actorUserId && request.Status != UserStatus.Active)
        {
            throw new InvalidOperationException(
                "You cannot suspend or disable your own administrator account.");
        }

        var oldStatus = user.Status;
        if (oldStatus == request.Status)
            return await GetUserAsync(user.Id, ct);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var isSuperAdmin = currentRoles.Contains(
            IdentityRoleNames.SuperAdmin,
            StringComparer.OrdinalIgnoreCase);

        if (isSuperAdmin && request.Status != UserStatus.Active)
            await EnsureAnotherActiveSuperAdminAsync(user.Id);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        user.Status = request.Status;
        user.LastUpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(updateResult, "User status update failed");

        var revokedSessions = await RevokeSessionsAsync(
            user.Id,
            $"Administrator status change: {reason}",
            ct);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        EnsureIdentitySucceeded(stampResult, "User security state update failed");

        _audit.Stage(new AuditRecordRequest(
            Action: "ADMIN_USER_STATUS_CHANGED",
            Category: "IdentityAdministration",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            OldValues: new { Status = oldStatus },
            NewValues: new { Status = request.Status },
            Metadata: new
            {
                ActorUserId = actorUserId,
                Reason = reason,
                RevokedSessions = revokedSessions
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetUserAsync(user.Id, ct);
    }

    public async Task<AdminUserDetailsDto> UpdateRolesAsync(
        Guid userId,
        UpdateAdminUserRolesRequestDto request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var reason = RequiredText(request.Reason, nameof(request.Reason), 500);
        var requestedRoles = NormalizeAndValidateRoles(request.Roles);

        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        if (user.UserType != UserType.Admin)
        {
            throw new InvalidOperationException(
                "Administrative role management is only available for internal administrator accounts.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var currentInternalRoles = currentRoles
            .Where(IsAssignableInternalRole)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var requestedSuperAdmin = requestedRoles.Contains(
            IdentityRoleNames.SuperAdmin,
            StringComparer.OrdinalIgnoreCase);
        var currentlySuperAdmin = currentInternalRoles.Contains(
            IdentityRoleNames.SuperAdmin,
            StringComparer.OrdinalIgnoreCase);

        if (user.Id == actorUserId && currentlySuperAdmin && !requestedSuperAdmin)
        {
            throw new InvalidOperationException(
                "You cannot remove the SuperAdmin role from your own account.");
        }

        if (currentlySuperAdmin && !requestedSuperAdmin)
            await EnsureAnotherActiveSuperAdminAsync(user.Id);

        var rolesToRemove = currentInternalRoles
            .Except(requestedRoles, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rolesToAdd = requestedRoles
            .Except(currentInternalRoles, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rolesToRemove.Length == 0 && rolesToAdd.Length == 0)
            return await GetUserAsync(user.Id, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            EnsureIdentitySucceeded(removeResult, "Removing existing administrative roles failed");
        }

        if (rolesToAdd.Length > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            EnsureIdentitySucceeded(addResult, "Assigning administrative roles failed");
        }

        user.LastUpdatedAt = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(updateResult, "User role update failed");

        var revokedSessions = await RevokeSessionsAsync(
            user.Id,
            $"Administrator role change: {reason}",
            ct);

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        EnsureIdentitySucceeded(stampResult, "User security state update failed");

        _audit.Stage(new AuditRecordRequest(
            Action: "ADMIN_USER_ROLES_CHANGED",
            Category: "IdentityAdministration",
            EntityName: nameof(ApplicationUser),
            EntityId: user.Id.ToString(),
            OldValues: new { Roles = currentInternalRoles },
            NewValues: new { Roles = requestedRoles },
            Metadata: new
            {
                ActorUserId = actorUserId,
                Reason = reason,
                AddedRoles = rolesToAdd,
                RemovedRoles = rolesToRemove,
                RevokedSessions = revokedSessions
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await GetUserAsync(user.Id, ct);
    }

    private async Task PopulateRolesAsync(
        IReadOnlyList<AdminUserListItemDto> users,
        CancellationToken ct)
    {
        if (users.Count == 0)
            return;

        var userIds = users.Select(x => x.Id).ToArray();

        var rolePairs = await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new
            {
                userRole.UserId,
                RoleName = role.Name
            })
            .ToListAsync(ct);

        var roleMap = rolePairs
            .Where(x => !string.IsNullOrWhiteSpace(x.RoleName))
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(x => x.RoleName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        foreach (var user in users)
        {
            user.Roles = roleMap.TryGetValue(user.Id, out var roles)
                ? roles
                : [];
        }
    }

    private async Task<IReadOnlyList<string>> GetRolesAsync(
        Guid userId,
        CancellationToken ct)
    {
        return await (
            from userRole in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userRole.UserId == userId && role.Name != null
            orderby role.Name
            select role.Name!)
            .ToListAsync(ct);
    }

    private async Task EnsureAnotherActiveSuperAdminAsync(Guid excludedUserId)
    {
        var superAdmins = await _userManager.GetUsersInRoleAsync(
            IdentityRoleNames.SuperAdmin);

        if (superAdmins.Any(x =>
                x.Id != excludedUserId &&
                x.Status == UserStatus.Active))
        {
            return;
        }

        throw new InvalidOperationException(
            "This change would leave KorridorX without an active Super Administrator.");
    }

    private async Task<int> RevokeSessionsAsync(
        Guid userId,
        string reason,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var sessions = await _db.RefreshTokens
            .Where(x =>
                x.UserId == userId &&
                !x.IsRevoked &&
                x.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }

        return sessions.Count;
    }

    private static string[] NormalizeAndValidateRoles(
        IReadOnlyCollection<string>? roles)
    {
        if (roles is null || roles.Count == 0)
            throw new InvalidOperationException("At least one administrative role is required.");

        var normalizedRoles = roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRoles.Length == 0)
            throw new InvalidOperationException("At least one administrative role is required.");

        var invalidRoles = normalizedRoles
            .Where(role => !IsAssignableInternalRole(role))
            .ToArray();

        if (invalidRoles.Length > 0)
        {
            throw new InvalidOperationException(
                $"The following roles cannot be assigned through internal user management: {string.Join(", ", invalidRoles)}.");
        }

        return AssignableInternalRoles
            .Where(allowedRole => normalizedRoles.Contains(
                allowedRole,
                StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static bool IsAssignableInternalRole(string role) =>
        AssignableInternalRoles.Contains(
            role,
            StringComparer.OrdinalIgnoreCase);

    private static string RequiredText(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
            throw new InvalidOperationException($"{fieldName} cannot exceed {maxLength} characters.");

        return cleaned;
    }

    private static string NormalizeEmail(string? value)
    {
        var email = RequiredText(value, "Email", 256).ToLowerInvariant();

        if (!email.Contains('@') ||
            email.StartsWith('@') ||
            email.EndsWith('@'))
        {
            throw new InvalidOperationException("A valid email address is required.");
        }

        return email;
    }

    private static string NormalizeCountryCode(string? value)
    {
        var countryCode = RequiredText(value, "CountryCode", 10).ToUpperInvariant();

        if (countryCode.Length < 2 ||
            countryCode.Length > 10 ||
            !countryCode.All(char.IsLetter))
        {
            throw new InvalidOperationException(
                "CountryCode must contain between two and ten letters.");
        }

        return countryCode;
    }

    private static string? CleanOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
            throw new InvalidOperationException($"Value cannot exceed {maxLength} characters.");

        return cleaned;
    }

    private static void EnsureIdentitySucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join(
            "; ",
            result.Errors
                .Select(x => x.Description)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(errors)
                ? operation
                : $"{operation}: {errors}");
    }
}