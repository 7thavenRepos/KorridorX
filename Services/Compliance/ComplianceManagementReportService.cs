using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public sealed class ComplianceManagementReportService : IComplianceManagementReportService
{
    private readonly AppDbContext _db;
    private readonly IRegulatoryReportingService _regulatoryReporting;

    public ComplianceManagementReportService(
        AppDbContext db,
        IRegulatoryReportingService regulatoryReporting)
    {
        _db = db;
        _regulatoryReporting = regulatoryReporting;
    }

    public async Task<RegulatoryComplianceManagementSummaryDto> GetSummaryAsync(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var next7Days = now.AddDays(7);
        var reports = await _regulatoryReporting.GetSummaryAsync(ct);
        var cases = _db.ComplianceCases.AsNoTracking().Where(x => !x.IsDeleted);

        return new RegulatoryComplianceManagementSummaryDto(
            reports,
            await _db.LegalHolds.AsNoTracking().CountAsync(x =>
                !x.IsDeleted && x.Status == LegalHoldStatus.Active, ct),
            await _db.DataRetentionPolicies.AsNoTracking().CountAsync(x =>
                !x.IsDeleted && x.IsActive, ct),
            await cases.CountAsync(x =>
                x.Status != ComplianceCaseStatus.Resolved &&
                x.Status != ComplianceCaseStatus.Closed, ct),
            await cases.CountAsync(x =>
                x.IsBlocking &&
                x.Status != ComplianceCaseStatus.Closed, ct),
            await cases.CountAsync(x =>
                x.DueAt.HasValue &&
                x.DueAt.Value < now &&
                x.Status != ComplianceCaseStatus.Resolved &&
                x.Status != ComplianceCaseStatus.Closed, ct),
            await _db.ScreeningRecords.AsNoTracking().CountAsync(x =>
                !x.IsDeleted &&
                x.ExpiresAt.HasValue &&
                x.ExpiresAt.Value >= now &&
                x.ExpiresAt.Value <= next7Days, ct));
    }
}
