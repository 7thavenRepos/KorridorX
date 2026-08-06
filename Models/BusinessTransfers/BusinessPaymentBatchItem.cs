using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.BusinessTransfers;

public class BusinessPaymentBatchItem : AuditableEntity
{
    public Guid BusinessPaymentBatchId { get; set; }
    public BusinessPaymentBatch BusinessPaymentBatch { get; set; } = null!;

    public int RowNumber { get; set; }
    public string? ExternalReference { get; set; }

    public Guid? BusinessBeneficiaryId { get; set; }
    public BusinessBeneficiary? BusinessBeneficiary { get; set; }

    public Guid? BusinessBeneficiaryBankAccountId { get; set; }
    public BusinessBeneficiaryBankAccount? BusinessBeneficiaryBankAccount { get; set; }

    public Guid? BusinessBeneficiaryMobileWalletId { get; set; }
    public BusinessBeneficiaryMobileWallet? BusinessBeneficiaryMobileWallet { get; set; }

    public string DestinationCountryCode { get; set; } = "";
    public string DestinationCurrencyCode { get; set; } = "";
    public decimal SourceAmount { get; set; }
    public decimal DestinationAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TotalPayableAmount { get; set; }
    public decimal CustomerRate { get; set; }
    public decimal ProviderRate { get; set; }

    public TransferPurpose Purpose { get; set; } = TransferPurpose.SupplierPayment;
    public string? PurposeNote { get; set; }

    public BusinessPaymentBatchItemStatus Status { get; set; } = BusinessPaymentBatchItemStatus.PendingValidation;
    public string? ValidationErrors { get; set; }

    public Guid? TransferQuoteId { get; set; }
    public TransferQuote? TransferQuote { get; set; }

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }
}
