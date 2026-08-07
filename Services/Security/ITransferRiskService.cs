using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Security;

public interface ITransferRiskService
{
    Task<TransferRiskDecisionDto> AssessAsync(
        Transfer transfer,
        Guid initiatedByUserId,
        CancellationToken ct = default);

    void EnsureCanProceedToPayout(Transfer transfer);

    Task<PagedResult<AmlFlagDto>> GetFlagsAsync(
        bool? isResolved,
        string? severity,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AmlFlagDetailsDto> GetFlagAsync(
        Guid flagId,
        CancellationToken ct = default);

    Task<AmlFlagDetailsDto> ReviewFlagAsync(
        Guid flagId,
        Guid reviewedByUserId,
        ReviewAmlFlagRequestDto request,
        CancellationToken ct = default);

    Task<TransferRiskDecisionDto> ReassessAsync(
        Guid transferId,
        Guid reviewedByUserId,
        CancellationToken ct = default);
}
