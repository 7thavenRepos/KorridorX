using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Payments;

public enum PaymentChannelDirection { Deposit = 1, Withdrawal = 2 }
public enum PaymentChannelStatus { Draft = 1, Active = 2, Paused = 3 }

/// <summary>Admin intent. Readiness is evaluated separately, at discovery and initiation.</summary>
public sealed class PaymentChannel : AuditableEntity
{
    public string Environment { get; set; } = "";
    public string ProviderCode { get; set; } = "BLAAIZ";
    public Guid ProviderWalletConfigurationId { get; set; }
    public string AssetCode { get; set; } = "";
    // Deposit: payer/profile country. Withdrawal: destination country. * means any permitted mapping.
    public string CountryCode { get; set; } = "*";
    public PaymentChannelDirection Direction { get; set; }
    public string CapabilityCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IndividualEnabled { get; set; }
    public bool BusinessEnabled { get; set; }
    public bool ProviderCapabilityConfirmed { get; set; }
    // Sticky handover: a draft does not disable legacy methods; pausing after activation does.
    public bool HasBeenActivated { get; set; }
    public PaymentChannelStatus Status { get; set; } = PaymentChannelStatus.Draft;
    public int SortOrder { get; set; }
    public decimal MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

/// <summary>Immutable payment route snapshot. Config edits never retarget an existing payment.</summary>
public sealed class PaymentChannelSelection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OperationType { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid ChannelId { get; set; }
    public Guid ChannelRevision { get; set; }
    public Guid ProviderWalletConfigurationId { get; set; }
    public string ProviderWalletId { get; set; } = "";
    public string ProviderCode { get; set; } = "";
    public string Environment { get; set; } = "";
    public string CapabilityCode { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public bool Business { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? FinancialAccountId { get; set; }
    public string? IdempotencyKey { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A customer-owned receiving account, not a treasury/house or pooled account.</summary>
public sealed class WalletDepositAccount : AuditableEntity
{
    public Guid FinancialAccountId { get; set; }
    public Guid PaymentChannelId { get; set; }
    public Guid ProviderWalletConfigurationId { get; set; }
    public string ProviderWalletId { get; set; } = "";
    public string ProviderCustomerId { get; set; } = "";
    public string? ProviderAccountId { get; set; }
    public string Environment { get; set; } = "";
    public string ConnectionKey { get; set; } = "";
    public string AssetCode { get; set; } = "";
    public string Status { get; set; } = "Creating";
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? BankName { get; set; }
    public string? Iban { get; set; }
    public string? SortCode { get; set; }
    public string? RoutingNumber { get; set; }
    public string? ProviderReference { get; set; }
    public string? FailureReason { get; set; }
    public bool CanRetryCreation { get; set; }
    public DateTime? LastCheckedAt { get; set; }
}
