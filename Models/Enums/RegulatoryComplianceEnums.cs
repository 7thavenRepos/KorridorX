namespace KorridorX.Models.Enums;

public enum RegulatoryReportType
{
    SuspiciousActivityReport = 1,
    SuspiciousTransactionReport = 2,
    InternalEscalation = 3
}

public enum RegulatoryReportStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Filed = 4,
    Rejected = 5,
    Withdrawn = 6
}

public enum RegulatoryReportApprovalDecision
{
    Approve = 1,
    Reject = 2
}

public enum RetentionRecordType
{
    NotificationMessage = 1,
    ProviderRequestLog = 2,
    WebhookEvent = 3,
    AuditLog = 4,
    ScreeningRecord = 5,
    ComplianceCase = 6,
    RegulatoryReport = 7
}

public enum RetentionAction
{
    ReviewOnly = 1,
    SoftDelete = 2
}

public enum LegalHoldStatus
{
    Active = 1,
    Released = 2
}
