namespace KorridorX.Models.Enums;

public enum SupportTicketStatus
{
    Open = 1,
    InProgress = 2,
    AwaitingCustomer = 3,
    Resolved = 4,
    Closed = 5
}

public enum SupportTicketPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}

public enum SupportTicketCategory
{
    General = 1,
    Transfer = 2,
    Collection = 3,
    Payout = 4,
    Refund = 5,
    Compliance = 6,
    Account = 7,
    Recipient = 8,
    Business = 9,
    Funding = 10,
    Technical = 11,
    Other = 99
}

public enum SupportMessageAuthorType
{
    Customer = 1,
    Agent = 2,
    System = 3
}

public enum TransferDisputeStatus
{
    Open = 1,
    Investigating = 2,
    AwaitingCustomer = 3,
    Resolved = 4,
    Rejected = 5,
    Withdrawn = 6
}

public enum TransferDisputeType
{
    TransferNotReceived = 1,
    IncorrectAmount = 2,
    DuplicateTransfer = 3,
    UnauthorizedTransfer = 4,
    RefundRequest = 5,
    PayoutFailure = 6,
    Other = 99
}

public enum TransferInvestigationStatus
{
    Open = 1,
    InProgress = 2,
    AwaitingProvider = 3,
    AwaitingCustomer = 4,
    Resolved = 5,
    Closed = 6
}

public enum TransferInvestigationOutcome
{
    None = 0,
    NoIssueFound = 1,
    ProviderIssue = 2,
    CustomerError = 3,
    Refunded = 4,
    Resent = 5,
    EscalatedCompliance = 6,
    Other = 99
}
