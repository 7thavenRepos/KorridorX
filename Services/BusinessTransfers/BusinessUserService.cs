using KorridorX.Data;
using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessTransfers;

public class BusinessUserService : IBusinessUserService
{
    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _accessService;

    public BusinessUserService(AppDbContext db, IBusinessAccessService accessService)
    {
        _db = db;
        _accessService = accessService;
    }

    public async Task<IReadOnlyList<BusinessUserDto>> GetUsersAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(
            userId,
            BusinessPermission.ManageBusinessUsers,
            ct);

        var profile = await _db.BusinessProfiles
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .FirstAsync(x => x.Id == access.BusinessProfileId, ct);

        var memberEntities = await _db.BusinessUsers
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted)
            .OrderBy(x => x.User.Email)
            .ToListAsync(ct);

        var members = memberEntities
            .Select(x => new BusinessUserDto(
                x.Id,
                x.UserId,
                x.User.Email ?? "",
                x.Role,
                x.Permissions == BusinessPermission.None
                    ? BusinessAccessService.DefaultPermissions(x.Role)
                    : x.Permissions,
                x.IsActive,
                false,
                x.CreatedAt))
            .ToList();

        members.Insert(0, new BusinessUserDto(
            Guid.Empty,
            profile.OwnerUserId,
            profile.OwnerUser.Email ?? "",
            BusinessUserRole.Owner,
            BusinessPermission.All,
            true,
            true,
            profile.CreatedAt));

        return members;
    }

    public async Task<BusinessUserDto> AddUserAsync(
        Guid userId,
        AddBusinessUserRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(
            userId,
            BusinessPermission.ManageBusinessUsers,
            ct);

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("User email is required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (!Enum.IsDefined(typeof(BusinessUserRole), request.Role))
        {
            throw new InvalidOperationException("A valid business user role is required.");
        }

        if (request.Role == BusinessUserRole.Owner)
        {
            throw new InvalidOperationException("Business ownership cannot be assigned through the member endpoint.");
        }

        var normalizedEmail = email.ToUpperInvariant();
        var target = await _db.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct)
            ?? throw new InvalidOperationException("No registered KorridorX user was found with that email address.");

        if (target.Id == access.OwnerUserId)
        {
            throw new InvalidOperationException("The business owner is already attached to this profile.");
        }

        var member = await _db.BusinessUsers
            .FirstOrDefaultAsync(x =>
                x.BusinessProfileId == access.BusinessProfileId &&
                x.UserId == target.Id,
                ct);

        if (member is null)
        {
            member = new BusinessUser
            {
                BusinessProfileId = access.BusinessProfileId,
                UserId = target.Id,
                Role = request.Role,
                Permissions = NormalizePermissions(request.Role, request.Permissions),
                IsActive = true,
                CreatedByUserId = userId
            };
            _db.BusinessUsers.Add(member);
        }
        else
        {
            member.IsDeleted = false;
            member.DeletedAt = null;
            member.DeletedByUserId = null;
            member.Role = request.Role;
            member.Permissions = NormalizePermissions(request.Role, request.Permissions);
            member.IsActive = true;
            member.LastUpdatedAt = DateTime.UtcNow;
            member.LastUpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(member, target.Email ?? email);
    }

    public async Task<BusinessUserDto> UpdateAccessAsync(
        Guid userId,
        Guid businessUserId,
        UpdateBusinessUserAccessRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(
            userId,
            BusinessPermission.ManageBusinessUsers,
            ct);

        if (!Enum.IsDefined(typeof(BusinessUserRole), request.Role))
        {
            throw new InvalidOperationException("A valid business user role is required.");
        }

        if (request.Role == BusinessUserRole.Owner)
        {
            throw new InvalidOperationException("Business ownership cannot be assigned through the member endpoint.");
        }

        var member = await _db.BusinessUsers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.Id == businessUserId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business user not found.");

        if (member.UserId == userId && !request.IsActive)
        {
            throw new InvalidOperationException("You cannot deactivate your own business membership.");
        }

        member.Role = request.Role;
        member.Permissions = NormalizePermissions(request.Role, request.Permissions);
        member.IsActive = request.IsActive;
        member.LastUpdatedAt = DateTime.UtcNow;
        member.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return ToDto(member, member.User.Email ?? "");
    }

    public async Task<BusinessApprovalPolicyDto> GetApprovalPolicyAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var access = await _accessService.GetAccessAsync(userId, ct);
        var profile = await _db.BusinessProfiles.AsNoTracking()
            .FirstAsync(x => x.Id == access.BusinessProfileId && !x.IsDeleted, ct);

        return new BusinessApprovalPolicyDto(
            profile.RequiresTransferApproval,
            Math.Max(1, profile.RequiredTransferApprovals),
            profile.TransferApprovalThreshold,
            profile.AllowTransferCreatorApproval);
    }

    public async Task<BusinessApprovalPolicyDto> UpdateApprovalPolicyAsync(
        Guid userId,
        UpdateBusinessApprovalPolicyRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(
            userId,
            BusinessPermission.ManageBusinessUsers,
            ct);

        if (request.RequiredTransferApprovals < 1 || request.RequiredTransferApprovals > 5)
        {
            throw new InvalidOperationException("Required transfer approvals must be between 1 and 5.");
        }

        if (request.TransferApprovalThreshold.HasValue && request.TransferApprovalThreshold.Value < 0)
        {
            throw new InvalidOperationException("Transfer approval threshold cannot be negative.");
        }

        var profile = await _db.BusinessProfiles
            .FirstAsync(x => x.Id == access.BusinessProfileId && !x.IsDeleted, ct);

        profile.RequiresTransferApproval = request.RequiresTransferApproval;
        profile.RequiredTransferApprovals = request.RequiredTransferApprovals;
        profile.TransferApprovalThreshold = request.TransferApprovalThreshold;
        profile.AllowTransferCreatorApproval = request.AllowTransferCreatorApproval;
        profile.LastUpdatedAt = DateTime.UtcNow;
        profile.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return new BusinessApprovalPolicyDto(
            profile.RequiresTransferApproval,
            profile.RequiredTransferApprovals,
            profile.TransferApprovalThreshold,
            profile.AllowTransferCreatorApproval);
    }

    private static BusinessPermission NormalizePermissions(
        BusinessUserRole role,
        BusinessPermission permissions) =>
        permissions == BusinessPermission.None
            ? BusinessAccessService.DefaultPermissions(role)
            : permissions & BusinessPermission.All;

    private static BusinessUserDto ToDto(BusinessUser member, string email) =>
        new(
            member.Id,
            member.UserId,
            email,
            member.Role,
            member.Permissions == BusinessPermission.None
                ? BusinessAccessService.DefaultPermissions(member.Role)
                : member.Permissions,
            member.IsActive,
            false,
            member.CreatedAt);
}
