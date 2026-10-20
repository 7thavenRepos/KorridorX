using KorridorX.Dtos.Fx;

namespace KorridorX.Services.Fx;

public interface IAdminTransferFeeService
{
    Task<TransferFeeOptionsDto> GetOptionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminTransferFeeDto>> GetAsync(CancellationToken ct = default);
    Task<AdminTransferFeeDto> SaveAsync(Guid actor, Guid? id, SaveTransferFeeRequestDto request, CancellationToken ct = default);
    Task<AdminTransferFeeDto> SetActiveAsync(Guid actor, Guid id, string revision, bool active, CancellationToken ct = default);
}
