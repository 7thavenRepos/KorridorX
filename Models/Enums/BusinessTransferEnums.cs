namespace KorridorX.Models.Enums;

[Flags]
public enum BusinessPermission : long
{
    None = 0,
    ViewBeneficiaries = 1L << 0,
    ManageBeneficiaries = 1L << 1,
    ViewTransfers = 1L << 2,
    CreateTransfers = 1L << 3,
    ApproveTransfers = 1L << 4,
    ViewBatches = 1L << 5,
    ManageBatches = 1L << 6,
    ApproveBatches = 1L << 7,
    ViewReports = 1L << 8,
    ManageBusinessUsers = 1L << 9,
    All = ViewBeneficiaries |
          ManageBeneficiaries |
          ViewTransfers |
          CreateTransfers |
          ApproveTransfers |
          ViewBatches |
          ManageBatches |
          ApproveBatches |
          ViewReports |
          ManageBusinessUsers
}

public enum BusinessApprovalStatus
{
    NotRequired = 1,
    Pending = 2,
    Approved = 3,
    Rejected = 4
}

public enum BusinessApprovalAction
{
    Approved = 1,
    Rejected = 2
}

public enum BusinessFundingSource
{
    BusinessWallet = 1,
    ExternalCollection = 2
}

public enum BusinessPaymentBatchStatus
{
    Draft = 1,
    ValidationFailed = 2,
    ReadyForApproval = 3,
    PendingApproval = 4,
    Approved = 5,
    Processing = 6,
    PartiallyCompleted = 7,
    Completed = 8,
    Rejected = 9,
    Failed = 10,
    Cancelled = 11
}

public enum BusinessPaymentBatchItemStatus
{
    PendingValidation = 1,
    Invalid = 2,
    Valid = 3,
    PendingApproval = 4,
    Approved = 5,
    TransferCreated = 6,
    Processing = 7,
    Completed = 8,
    Failed = 9,
    Rejected = 10
}
