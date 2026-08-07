namespace KorridorX.Models.Enums;

public enum ScreeningSubjectType
{
    Customer = 1,
    Business = 2,
    Recipient = 3,
    BusinessBeneficiary = 4,
    Transfer = 5,
    BusinessBeneficialOwner = 6
}

public enum ScreeningReason
{
    Onboarding = 1,
    ProfileChanged = 2,
    TransferCreated = 3,
    OngoingRescreening = 4,
    Manual = 5
}

public enum ScreeningStatus
{
    Skipped = 1,
    Clear = 2,
    PotentialMatch = 3,
    ConfirmedMatch = 4,
    Failed = 5,
    ManualReview = 6
}

public enum WatchlistType
{
    Sanctions = 1,
    Pep = 2,
    AdverseMedia = 3,
    LawEnforcement = 4,
    Other = 5
}

public enum ComplianceCaseType
{
    SanctionsScreening = 1,
    PepScreening = 2,
    AdverseMediaScreening = 3,
    TransactionMonitoring = 4,
    KycReview = 5,
    ManualReview = 6
}

public enum ComplianceCaseStatus
{
    Open = 1,
    InReview = 2,
    AwaitingInformation = 3,
    Escalated = 4,
    Resolved = 5,
    Closed = 6
}

public enum ComplianceCasePriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ComplianceCaseDecision
{
    Cleared = 1,
    FalsePositive = 2,
    ConfirmedMatch = 3,
    Escalated = 4,
    ReportFiled = 5,
    ClosedNoAction = 6
}
