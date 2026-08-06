using KorridorX.Dtos.BusinessTransfers;

namespace KorridorX.Services.BusinessTransfers;

public interface IBusinessUserService
{
    Task<IReadOnlyList<BusinessUserDto>> GetUsersAsync(Guid userId, CancellationToken ct = default);
    Task<BusinessUserDto> AddUserAsync(Guid userId, AddBusinessUserRequestDto request, CancellationToken ct = default);
    Task<BusinessUserDto> UpdateAccessAsync(Guid userId, Guid businessUserId, UpdateBusinessUserAccessRequestDto request, CancellationToken ct = default);
    Task<BusinessApprovalPolicyDto> GetApprovalPolicyAsync(Guid userId, CancellationToken ct = default);
    Task<BusinessApprovalPolicyDto> UpdateApprovalPolicyAsync(Guid userId, UpdateBusinessApprovalPolicyRequestDto request, CancellationToken ct = default);
}
