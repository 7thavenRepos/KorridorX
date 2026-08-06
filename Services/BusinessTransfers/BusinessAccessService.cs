using KorridorX.Data;
using KorridorX.Exceptions;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessTransfers;

public class BusinessAccessService : IBusinessAccessService
{
    private readonly AppDbContext _db;

    public BusinessAccessService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BusinessAccessContext> GetAccessAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var owned = await _db.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
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
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted && !x.BusinessProfile.IsDeleted)
            .OrderBy(x => x.BusinessProfile.CreatedAt)
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
            throw new InvalidOperationException("The authenticated user is not attached to an active business profile.");
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
                BusinessPermission.ViewReports,
            BusinessUserRole.Compliance =>
                BusinessPermission.ViewBeneficiaries |
                BusinessPermission.ViewTransfers |
                BusinessPermission.ApproveTransfers |
                BusinessPermission.ViewBatches |
                BusinessPermission.ApproveBatches |
                BusinessPermission.ViewReports,
            _ =>
                BusinessPermission.ViewBeneficiaries |
                BusinessPermission.ViewTransfers |
                BusinessPermission.ViewBatches
        };
}
