using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.DigitalAssets;

public sealed class DigitalAssetTravelRuleRecord : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public Guid BusinessCustomerId { get; set; }
    public Guid DigitalAssetWithdrawalId { get; set; }
    public DigitalAssetWithdrawal DigitalAssetWithdrawal { get; set; } = null!;

    public DigitalAssetTravelRuleStatus Status { get; set; } =
        DigitalAssetTravelRuleStatus.NotRequired;

    public decimal ThresholdAmount { get; set; }
    public string AssetCode { get; set; } = "";
    public string NetworkCode { get; set; } = "";
    public string? OriginatorVasp { get; set; }
    public string? BeneficiaryVasp { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? ProviderReference { get; set; }
    public string? PayloadJson { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
