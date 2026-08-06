namespace KorridorX.Models.Enums;

public enum TransferStatus
{
    Draft = 1,
    Quoted = 2,
    PendingPayment = 3,
    PaymentReceived = 4,
    Processing = 5,
    PayoutInitiated = 6,
    PayoutCompleted = 7,
    Completed = 8,
    Failed = 9,
    Cancelled = 10,
    RefundPending = 11,
    Refunded = 12,
    PendingApproval = 13,
    Rejected = 14
}

public enum TransferType
{
    ConsumerToConsumer = 1,
    BusinessToConsumer = 2,
    BusinessToBusiness = 3
}

public enum TransferPurpose
{
    FamilySupport = 1,
    Education = 2,
    Medical = 3,
    Salary = 4,
    SupplierPayment = 5,
    GoodsAndServices = 6,
    Other = 99
}