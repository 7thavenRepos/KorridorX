using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;

namespace KorridorX.Models.Providers;

public class ProviderTransaction : BaseEntity
{
    public ProviderCode ProviderCode { get; set; } = ProviderCode.Blaaiz;

    public Guid? TransferId { get; set; }
    public Transfer? Transfer { get; set; }

    public Guid? CollectionId { get; set; }
    public Collection? Collection { get; set; }

    public Guid? PayoutId { get; set; }
    public Payout? Payout { get; set; }

    public string ProviderTransactionId { get; set; } = "";
    public string? ProviderReference { get; set; }

    public string TransactionType { get; set; } = "";
    public string ProviderStatus { get; set; } = "";

    public string CurrencyCode { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? AmountWithoutFee { get; set; }
    public decimal? ProviderFeeAmount { get; set; }
    public string? ProviderFeeCurrencyCode { get; set; }

    public string? RawPayloadJson { get; set; }

    public DateTime? ProviderCreatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}