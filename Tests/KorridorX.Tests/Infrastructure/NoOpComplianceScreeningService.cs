using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Services.Compliance;

namespace KorridorX.Tests.Infrastructure;

public sealed class NoOpComplianceScreeningService : IComplianceScreeningService
{
    public Task<ScreeningRecordDto?> ScreenCustomerAsync(
        Guid customerProfileId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default) =>
        Task.FromResult<ScreeningRecordDto?>(null);

    public Task<ScreeningRecordDto?> ScreenBusinessAsync(
        Guid businessProfileId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default) =>
        Task.FromResult<ScreeningRecordDto?>(null);

    public Task<ScreeningRecordDto?> ScreenRecipientAsync(
        Guid recipientId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default) =>
        Task.FromResult<ScreeningRecordDto?>(null);

    public Task<ScreeningRecordDto?> ScreenBusinessBeneficiaryAsync(
        Guid businessBeneficiaryId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default) =>
        Task.FromResult<ScreeningRecordDto?>(null);

    public Task<ScreeningRecordDto?> ScreenBusinessBeneficialOwnerAsync(
        Guid businessBeneficialOwnerId,
        ScreeningReason reason,
        Guid? initiatedByUserId,
        Guid? transferId = null,
        CancellationToken ct = default) =>
        Task.FromResult<ScreeningRecordDto?>(null);

    public Task<IReadOnlyList<ScreeningRecordDto>> ScreenTransferAsync(
        Transfer transfer,
        Guid initiatedByUserId,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ScreeningRecordDto>>(Array.Empty<ScreeningRecordDto>());

    public Task<ScreeningRecordDto> ManualScreenAsync(
        ScreeningSubjectType subjectType,
        Guid subjectId,
        Guid? transferId,
        Guid initiatedByUserId,
        CancellationToken ct = default) =>
        throw new InvalidOperationException("Manual sanctions screening is disabled in the release-candidate test host.");

    public Task EnsureTransferCanProceedToPayoutAsync(
        Transfer transfer,
        CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<PagedResult<ScreeningRecordDto>> GetScreeningsAsync(
        ScreeningSubjectType? subjectType,
        ScreeningStatus? status,
        bool? isBlocking,
        Guid? transferId,
        int page,
        int pageSize,
        CancellationToken ct = default) =>
        Task.FromResult(new PagedResult<ScreeningRecordDto>
        {
            Meta = new PageMeta
            {
                Page = Math.Max(1, page),
                PageSize = Math.Max(1, pageSize),
                TotalItems = 0
            }
        });

    public Task<int> RunDueRescreeningAsync(
        int batchSize,
        CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task<int> RunDueRescreeningAsync(
        int batchSize,
        Guid initiatedByUserId,
        string reason,
        CancellationToken ct = default) =>
        Task.FromResult(0);
}
