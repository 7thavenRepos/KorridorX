namespace KorridorX.Models.Enums;

public enum OutboundFundsRestrictionSubjectType
{
    User = 1,
    BusinessProfile = 2,
    BusinessCustomer = 3
}

public enum OutboundFundsRestrictionSource
{
    ExternalBankRestriction = 1,
    ComplianceReview = 2,
    LawEnforcementRequest = 3,
    FraudInvestigation = 4,
    InternalAuditReview = 5,
    Other = 99
}
