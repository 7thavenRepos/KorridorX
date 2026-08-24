namespace KorridorX.Dtos.Compliance;

public sealed record ComplianceOperationsSummaryDto(
    int KycPending,
    int KycUnderReview,
    int KycRejected,
    int KycApprovedLast30Days,
    int KybPending,
    int KybUnderReview,
    int KybRejected,
    int KybApprovedLast30Days,
    int OpenCases,
    int BlockingCases,
    int OverdueCases,
    int CriticalCases,
    int UnresolvedRiskFlags,
    int BlockingRiskFlags,
    int PotentialScreeningMatches,
    int ManualReviewScreenings,
    int FailedScreenings,
    int RegulatoryReportsPendingApproval,
    int RegulatoryReportsApproved,
    int OverdueRegulatoryReports,
    DateTime CheckedAt);