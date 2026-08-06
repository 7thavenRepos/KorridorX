using KorridorX.Models.Customers;
using KorridorX.Models.Enums;

namespace KorridorX.Services.BusinessTransfers;

public interface IBusinessAccessService
{
    Task<BusinessAccessContext> GetAccessAsync(Guid userId, CancellationToken ct = default);
    Task<BusinessAccessContext> EnsurePermissionAsync(
        Guid userId,
        BusinessPermission permission,
        CancellationToken ct = default);
}

public sealed record BusinessAccessContext(
    Guid BusinessProfileId,
    Guid OwnerUserId,
    BusinessUserRole Role,
    BusinessPermission Permissions,
    bool IsOwner,
    bool RequiresTransferApproval,
    int RequiredTransferApprovals,
    decimal? TransferApprovalThreshold,
    bool AllowTransferCreatorApproval)
{
    public bool HasPermission(BusinessPermission permission) =>
        IsOwner || (Permissions & permission) == permission;
}
