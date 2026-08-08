namespace KorridorX.Models.Enums;

public enum FinanceTranslationRateType
{
    Closing = 1,
    PeriodAverage = 2
}

public enum ProviderInvoiceStatus
{
    Imported = 1,
    Matched = 2,
    Variance = 3,
    Approved = 4,
    Posted = 5,
    Rejected = 6
}

public enum TaxType
{
    Vat = 1,
    Gst = 2,
    SalesTax = 3,
    Withholding = 4,
    Other = 5
}

public enum TaxApplicability
{
    TransferFee = 1,
    ProviderInvoice = 2,
    Other = 3
}

public enum FinanceCloseRequestStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Executed = 4
}
