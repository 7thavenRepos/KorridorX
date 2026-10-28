using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KorridorX.Dtos.Treasury;

public sealed record HouseFundingAccountDto(Guid Id, string AccountCode, decimal AvailableBalance, decimal HeldBalance);
public sealed class HouseFundingPositionDto
{
    public string AssetCode { get; set; } = "";
    public decimal ProviderBalance { get; set; }
    public DateTime? ProviderObservedAt { get; set; }
    public decimal CustomerLiabilities { get; set; }
    public decimal CustomerHeldIncluded { get; set; }
    // Customer settled balances include available funds and holds exactly once.
    public decimal TotalLiabilities => CustomerLiabilities;
    public decimal NetCompanyFunds => ProviderBalance - TotalLiabilities;
    public decimal RecordedCompanyFunds { get; set; }
    public decimal RecordedHouseFunds { get; set; }
    public decimal OtherAllocatedFunds { get; set; }
    public decimal TotalRecordedAllocations => RecordedCompanyFunds + OtherAllocatedFunds;
    public decimal UnallocatedCompanyFunds => NetCompanyFunds - TotalRecordedAllocations;
    public decimal FundingLimit => Warnings.Count == 0 ? Math.Max(0m, UnallocatedCompanyFunds) : 0m;
    public int DecimalPlaces { get; set; } = 2;
    public List<string> Warnings { get; set; } = [];
    public List<HouseFundingAccountDto> HouseAccounts { get; set; } = [];
}
public sealed record HouseFundingOverviewDto(DateTime GeneratedAt, bool CanFund, string Environment,
    IReadOnlyList<HouseFundingPositionDto> Positions);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PreviewHouseFundingRequestDto
{
    public Guid HouseAccountId { get; set; }
    [Range(typeof(decimal), "0.00000001", "79228162514264337593543950335")]
    public decimal Amount { get; set; }
    [Required, MaxLength(150)] public string FundingReference { get; set; } = "";
    [Required, MaxLength(800)] public string Reason { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ConfirmHouseFundingRequestDto
{
    [Required, MaxLength(16384)] public string PreviewToken { get; set; } = "";
    public bool ConfirmedCompanyFunds { get; set; }
}
public sealed record HouseFundingPreviewDto(string AssetCode, Guid HouseAccountId, string AccountCode,
    decimal Amount, string FundingReference, string Reason, HouseFundingPositionDto Position,
    DateTimeOffset ExpiresAt, string? PreviewToken, bool AlreadyPosted, string? LedgerReference);
public sealed record HouseFundingResultDto(Guid LedgerTransactionId, string Reference, string AssetCode,
    Guid HouseAccountId, decimal Amount, decimal AvailableBalanceAfter, bool AlreadyPosted);
