using KorridorX.Data;
using KorridorX.Dtos.BusinessContext;
using KorridorX.Exceptions;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessContext;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessTransfers;

public class BusinessAccessService : IBusinessAccessService
{
    private readonly AppDbContext _db;
    private readonly IBusinessContextAccessor _contextAccessor;

    public BusinessAccessService(
        AppDbContext db,
        IBusinessContextAccessor contextAccessor)
    {
        _db = db;
        _contextAccessor = contextAccessor;
    }

    public async Task<BusinessAccessContext> GetAccessAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var selectedBusinessId = _contextAccessor.GetSelectedBusinessProfileId()
            ?? await ResolveSingleBusinessAsync(userId, ct);

        var owned = await _db.BusinessProfiles
            .AsNoTracking()
            .Where(x =>
                x.Id == selectedBusinessId &&
                x.OwnerUserId == userId &&
                !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.OwnerUserId,
                x.RequiresTransferApproval,
                x.RequiredTransferApprovals,
                x.TransferApprovalThreshold,
                x.AllowTransferCreatorApproval
            })
            .FirstOrDefaultAsync(ct);

        if (owned is not null)
        {
            return new BusinessAccessContext(
                owned.Id,
                owned.OwnerUserId,
                BusinessUserRole.Owner,
                BusinessPermission.All,
                true,
                owned.RequiresTransferApproval,
                Math.Max(1, owned.RequiredTransferApprovals),
                owned.TransferApprovalThreshold,
                owned.AllowTransferCreatorApproval);
        }

        var membership = await _db.BusinessUsers
            .AsNoTracking()
            .Where(x =>
                x.BusinessProfileId == selectedBusinessId &&
                x.UserId == userId &&
                x.IsActive &&
                !x.IsDeleted &&
                !x.BusinessProfile.IsDeleted)
            .Select(x => new
            {
                x.BusinessProfileId,
                x.BusinessProfile.OwnerUserId,
                x.Role,
                x.Permissions,
                x.BusinessProfile.RequiresTransferApproval,
                x.BusinessProfile.RequiredTransferApprovals,
                x.BusinessProfile.TransferApprovalThreshold,
                x.BusinessProfile.AllowTransferCreatorApproval
            })
            .FirstOrDefaultAsync(ct);

        if (membership is null)
        {
            throw new ForbiddenException(
                "The authenticated user does not have access to the selected business profile.");
        }

        var permissions = membership.Permissions == BusinessPermission.None
            ? DefaultPermissions(membership.Role)
            : membership.Permissions;

        return new BusinessAccessContext(
            membership.BusinessProfileId,
            membership.OwnerUserId,
            membership.Role,
            permissions,
            false,
            membership.RequiresTransferApproval,
            Math.Max(1, membership.RequiredTransferApprovals),
            membership.TransferApprovalThreshold,
            membership.AllowTransferCreatorApproval);
    }

    public async Task<BusinessAccessContext> EnsurePermissionAsync(
        Guid userId,
        BusinessPermission permission,
        CancellationToken ct = default)
    {
        var access = await GetAccessAsync(userId, ct);

        if (!access.HasPermission(permission))
        {
            throw new ForbiddenException(
                $"The business user does not have the required permission: {permission}.");
        }

        return access;
    }

    public async Task<IReadOnlyList<AvailableBusinessContextDto>> GetAvailableBusinessesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var selected = _contextAccessor.GetSelectedBusinessProfileId();

        var owned = await _db.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId && !x.IsDeleted)
            .Select(x => new AvailableBusinessContextDto(
                x.Id,
                x.BusinessName,
                BusinessUserRole.Owner,
                BusinessPermission.All,
                true,
                x.KybStatus,
                selected == x.Id))
            .ToListAsync(ct);

        var memberships = await _db.BusinessUsers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.IsActive &&
                !x.IsDeleted &&
                !x.BusinessProfile.IsDeleted &&
                x.BusinessProfile.OwnerUserId != userId)
            .Select(x => new
            {
                x.BusinessProfileId,
                x.BusinessProfile.BusinessName,
                x.Role,
                x.Permissions,
                x.BusinessProfile.KybStatus
            })
            .ToListAsync(ct);

        var results = new List<AvailableBusinessContextDto>(owned);
        results.AddRange(memberships.Select(x => new AvailableBusinessContextDto(
            x.BusinessProfileId,
            x.BusinessName,
            x.Role,
            x.Permissions == BusinessPermission.None ? DefaultPermissions(x.Role) : x.Permissions,
            false,
            x.KybStatus,
            selected == x.BusinessProfileId)));

        return results
            .OrderByDescending(x => x.IsSelected)
            .ThenBy(x => x.BusinessName)
            .ToList();
    }

    private async Task<Guid> ResolveSingleBusinessAsync(
        Guid userId,
        CancellationToken ct)
    {
        var ownedIds = await _db.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId && !x.IsDeleted)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var memberIds = await _db.BusinessUsers
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.IsActive &&
                !x.IsDeleted &&
                !x.BusinessProfile.IsDeleted)
            .Select(x => x.BusinessProfileId)
            .ToListAsync(ct);

        var available = ownedIds
            .Concat(memberIds)
            .Distinct()
            .ToList();

        return available.Count switch
        {
            0 => throw new InvalidOperationException(
                "The authenticated user is not attached to an active business profile."),
            1 => available[0],
            _ => throw new InvalidOperationException(
                $"Multiple business profiles are available. Supply the {HttpBusinessContextAccessor.HeaderName} header.")
        };
    }

    public static BusinessPermission DefaultPermissions(BusinessUserRole role) =>
        role switch
        {
            BusinessUserRole.Owner => BusinessPermission.All,
            BusinessUserRole.Admin => BusinessPermission.All,
            BusinessUserRole.Finance =>
                BusinessPermission.ViewBeneficiaries |
                BusinessPermission.ManageBeneficiaries |
                BusinessPermission.ViewTransfers |
                BusinessPermission.CreateTransfers |
                BusinessPermission.ViewBatches |
                BusinessPermission.ManageBatches |
                BusinessPermission.ViewReports |
                BusinessPermission.ViewWallets |
                BusinessPermission.ManageFunding |
                BusinessPermission.ViewEmbeddedFinance |
                BusinessPermission.ViewTrading |
                BusinessPermission.Trade |
                BusinessPermission.ViewDigitalAssets |
                BusinessPermission.ManageDigitalAssets,
            BusinessUserRole.Compliance =>
                BusinessPermission.ViewBeneficiaries |
                BusinessPermission.ViewTransfers |
                BusinessPermission.ApproveTransfers |
                BusinessPermission.ViewBatches |
                BusinessPermission.ApproveBatches |
                BusinessPermission.ViewReports |
                BusinessPermission.ViewWallets |
                BusinessPermission.ViewTrading |
                BusinessPermission.ViewDigitalAssets,
            _ =>
                BusinessPermission.ViewBeneficiaries |
                BusinessPermission.ViewTransfers |
                BusinessPermission.ViewBatches |
                BusinessPermission.ViewWallets |
                BusinessPermission.ViewTrading |
                BusinessPermission.ViewDigitalAssets
        };
}