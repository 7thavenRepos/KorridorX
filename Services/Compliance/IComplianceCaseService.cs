using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public interface IComplianceCaseService
{
    Task<ComplianceCase> OpenScreeningCaseAsync(
        ScreeningRecord screening,
        Guid? initiatedByUserId,
        CancellationToken ct = default);

    Task<ComplianceCase> OpenTransactionMonitoringCaseAsync(
        Guid transferId,
        Guid? customerProfileId,
        Guid? businessProfileId,
        AmlFlag flag,
        ComplianceCasePriority priority,
        string title,
        string description,
        Guid? initiatedByUserId,
        CancellationToken ct = default);

    Task<PagedResult<ComplianceCaseDto>> GetCasesAsync(
        ComplianceCaseStatus? status,
        ComplianceCaseType? caseType,
        ComplianceCasePriority? priority,
        bool? isBlocking,
        Guid? assignedToUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ComplianceCaseDetailsDto> GetCaseAsync(
        Guid caseId,
        CancellationToken ct = default);

    Task<ComplianceCaseDetailsDto> AssignAsync(
        Guid caseId,
        Guid actionedByUserId,
        AssignComplianceCaseRequestDto request,
        CancellationToken ct = default);

    Task<ComplianceCaseDetailsDto> AddNoteAsync(
        Guid caseId,
        Guid actionedByUserId,
        AddComplianceCaseNoteRequestDto request,
        CancellationToken ct = default);

    Task<ComplianceCaseDetailsDto> AddEvidenceAsync(
        Guid caseId,
        Guid actionedByUserId,
        AddComplianceCaseEvidenceRequestDto request,
        CancellationToken ct = default);

    Task<ComplianceCaseDetailsDto> DecideAsync(
        Guid caseId,
        Guid actionedByUserId,
        DecideComplianceCaseRequestDto request,
        CancellationToken ct = default);

    Task<ComplianceCaseSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<bool> HasBlockingCaseAsync(
        Guid transferId,
        Guid? customerProfileId,
        Guid? businessProfileId,
        Guid? recipientId,
        Guid? businessBeneficiaryId,
        CancellationToken ct = default);
}
