using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public interface IRegulatoryReportingService
{
    Task<RegulatoryReportDto> CreateAsync(
        Guid complianceCaseId,
        Guid userId,
        CreateRegulatoryReportRequestDto request,
        CancellationToken ct = default);

    Task<RegulatoryReportDto> UpdateAsync(
        Guid reportId,
        Guid userId,
        UpdateRegulatoryReportRequestDto request,
        CancellationToken ct = default);

    Task<RegulatoryReportDto> SubmitForApprovalAsync(
        Guid reportId,
        Guid userId,
        CancellationToken ct = default);

    Task<RegulatoryReportDto> ReviewAsync(
        Guid reportId,
        Guid userId,
        ReviewRegulatoryReportRequestDto request,
        CancellationToken ct = default);

    Task<RegulatoryReportDto> MarkFiledAsync(
        Guid reportId,
        Guid userId,
        MarkRegulatoryReportFiledRequestDto request,
        CancellationToken ct = default);

    Task<RegulatoryReportDto> GetAsync(Guid reportId, CancellationToken ct = default);

    Task<PagedResult<RegulatoryReportDto>> GetPagedAsync(
        RegulatoryReportStatus? status,
        RegulatoryReportType? reportType,
        string? jurisdictionCode,
        Guid? complianceCaseId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<RegulatoryReportSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<RegulatoryReportExportResult> ExportPackageAsync(
        Guid reportId,
        Guid userId,
        CancellationToken ct = default);
}
