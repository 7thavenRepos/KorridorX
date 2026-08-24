using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public sealed class ComplianceOperationsQueryService : IComplianceOperationsQueryService
{
    private readonly AppDbContext _db;

    public ComplianceOperationsQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ComplianceOperationsSummaryDto> GetSummaryAsync(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var last30Days = now.AddDays(-30);

        var kyc = _db.KycApplications
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                !x.KycProfile.IsDeleted &&
                !x.KycProfile.CustomerProfile.IsDeleted);

        var kyb = _db.BusinessKybApplications
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                !x.BusinessProfile.IsDeleted);

        var cases = _db.ComplianceCases
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var flags = _db.AmlFlags
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var screenings = _db.ScreeningRecords
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var reports = _db.RegulatoryReports
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        return new ComplianceOperationsSummaryDto(
            KycPending: await kyc.CountAsync(
                x => x.Status == KycStatus.Pending,
                ct),

            KycUnderReview: await kyc.CountAsync(
                x => x.Status == KycStatus.UnderReview,
                ct),

            KycRejected: await kyc.CountAsync(
                x => x.Status == KycStatus.Rejected,
                ct),

            KycApprovedLast30Days: await _db.KycProfiles
                .AsNoTracking()
                .CountAsync(
                    x =>
                        !x.IsDeleted &&
                        x.Status == KycStatus.Approved &&
                        x.ApprovedAt.HasValue &&
                        x.ApprovedAt.Value >= last30Days,
                    ct),

            KybPending: await kyb.CountAsync(
                x => x.Status == KybStatus.Pending,
                ct),

            KybUnderReview: await kyb.CountAsync(
                x => x.Status == KybStatus.UnderReview,
                ct),

            KybRejected: await kyb.CountAsync(
                x => x.Status == KybStatus.Rejected,
                ct),

            KybApprovedLast30Days: await _db.BusinessProfiles
                .AsNoTracking()
                .CountAsync(
                    x =>
                        !x.IsDeleted &&
                        x.KybStatus == KybStatus.Approved &&
                        x.KybApprovedAt.HasValue &&
                        x.KybApprovedAt.Value >= last30Days,
                    ct),

            OpenCases: await cases.CountAsync(
                x =>
                    x.Status != ComplianceCaseStatus.Resolved &&
                    x.Status != ComplianceCaseStatus.Closed,
                ct),

            BlockingCases: await cases.CountAsync(
                x =>
                    x.IsBlocking &&
                    x.Status != ComplianceCaseStatus.Resolved &&
                    x.Status != ComplianceCaseStatus.Closed,
                ct),

            OverdueCases: await cases.CountAsync(
                x =>
                    x.DueAt.HasValue &&
                    x.DueAt.Value < now &&
                    x.Status != ComplianceCaseStatus.Resolved &&
                    x.Status != ComplianceCaseStatus.Closed,
                ct),

            CriticalCases: await cases.CountAsync(
                x =>
                    x.Priority == ComplianceCasePriority.Critical &&
                    x.Status != ComplianceCaseStatus.Resolved &&
                    x.Status != ComplianceCaseStatus.Closed,
                ct),

            UnresolvedRiskFlags: await flags.CountAsync(
                x => !x.IsResolved,
                ct),

            BlockingRiskFlags: await flags.CountAsync(
                x => !x.IsResolved && x.IsBlocking,
                ct),

            PotentialScreeningMatches: await screenings.CountAsync(
                x => x.Status == ScreeningStatus.PotentialMatch,
                ct),

            ManualReviewScreenings: await screenings.CountAsync(
                x => x.Status == ScreeningStatus.ManualReview,
                ct),

            FailedScreenings: await screenings.CountAsync(
                x => x.Status == ScreeningStatus.Failed,
                ct),

            RegulatoryReportsPendingApproval: await reports.CountAsync(
                x => x.Status == RegulatoryReportStatus.PendingApproval,
                ct),

            RegulatoryReportsApproved: await reports.CountAsync(
                x => x.Status == RegulatoryReportStatus.Approved,
                ct),

            OverdueRegulatoryReports: await reports.CountAsync(
                x =>
                    x.FilingDueAt.HasValue &&
                    x.FilingDueAt.Value < now &&
                    x.Status != RegulatoryReportStatus.Filed &&
                    x.Status != RegulatoryReportStatus.Withdrawn,
                ct),

            CheckedAt: now);
    }
}