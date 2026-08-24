using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Compliance;

public interface IComplianceScreeningService
{
    Task<ScreeningRecordDto?> ScreenCustomerAsync(
        Guid customerProfileId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default);

    Task<ScreeningRecordDto?> ScreenBusinessAsync(
        Guid businessProfileId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default);

    Task<ScreeningRecordDto?> ScreenRecipientAsync(
        Guid recipientId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default);

    Task<ScreeningRecordDto?> ScreenBusinessBeneficiaryAsync(
        Guid businessBeneficiaryId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default);

    Task<ScreeningRecordDto?> ScreenBusinessBeneficialOwnerAsync(
        Guid businessBeneficialOwnerId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ScreeningRecordDto>> ScreenTransferAsync(
        Transfer transfer,
        Guid initiatedByUserId,
        CancellationToken ct = default);

    Task<ScreeningRecordDto> ManualScreenAsync(
        ScreeningSubjectType subjectType,
        Guid subjectId,
        Guid? transferId,
        Guid initiatedByUserId,
        CancellationToken ct = default);

    Task EnsureTransferCanProceedToPayoutAsync(
        Transfer transfer,
        CancellationToken ct = default);

    Task<PagedResult<ScreeningRecordDto>> GetScreeningsAsync(
        ScreeningSubjectType? subjectType,
        ScreeningStatus? status,
        bool? isBlocking,
        Guid? transferId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<int> RunDueRescreeningAsync(
        int batchSize,
        CancellationToken ct = default);

    Task<int> RunDueRescreeningAsync(
        int batchSize,
        Guid initiatedByUserId,
        string reason,
        CancellationToken ct = default);
}
