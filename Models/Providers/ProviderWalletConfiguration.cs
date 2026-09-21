using KorridorX.Models.Common;

namespace KorridorX.Models.Providers;

public sealed class ProviderWalletConfiguration : AuditableEntity
{
    public string ProviderCode { get; set; } = "BLAAIZ";
    public string Environment { get; set; } = "";
    public string ProviderWalletId { get; set; } = "";
    public string AssetCode { get; set; } = "";
    // Empty for fiat. Crypto routes always name a configured asset network.
    public string NetworkCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool CollectionEnabled { get; set; }
    public bool PayoutEnabled { get; set; }
    public bool DefaultForCollection { get; set; }
    public bool DefaultForPayout { get; set; }
    public bool IsActive { get; set; }
    public string? VerifiedConnectionKey { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public decimal? LastProviderBalance { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

public sealed class ProviderWalletSelection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OperationType { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid WalletConfigurationId { get; set; }
    public ProviderWalletConfiguration WalletConfiguration { get; set; } = null!;
    public string ProviderWalletId { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime SelectedAt { get; set; } = DateTime.UtcNow;
}
