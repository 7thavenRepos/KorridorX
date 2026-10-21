using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Fx;

public sealed record TransferFeePairOptionDto(string SourceCurrencyCode, string DestinationCurrencyCode, bool IsActive);
public sealed record TransferFeeCountryOptionDto(string CountryCode, string CountryName, string AssetCode,
    bool CanSend, bool CanReceive, bool IndividualEnabled, bool BusinessEnabled, int DecimalPlaces);
public sealed record TransferFeeOptionsDto(IReadOnlyList<TransferFeePairOptionDto> Pairs,
    IReadOnlyList<TransferFeeCountryOptionDto> Countries);

public sealed class SaveTransferFeeRequestDto
{
    // Pricing follows the sending wallet currency and the selected currency pair.
    // Sending and receiving country are intentionally not part of the fee request.
    [Required, MaxLength(20)] public string SourceCurrencyCode { get; set; } = "";
    [Required, MaxLength(20)] public string DestinationCurrencyCode { get; set; } = "";
    public TransferType TransferType { get; set; }
    [JsonRequired] public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    [JsonRequired] public decimal FixedFee { get; set; }
    [JsonRequired] public decimal PercentageFee { get; set; }
    [MaxLength(30)] public string? Revision { get; set; }
}

public sealed class TransferFeeRevisionRequestDto
{
    [Required, MaxLength(30)] public string Revision { get; set; } = "";
}

public sealed record AdminTransferFeeDto(Guid Id, string SourceCountryCode, string DestinationCountryCode,
    string SourceCurrencyCode, string DestinationCurrencyCode, TransferType TransferType,
    decimal MinAmount, decimal? MaxAmount, decimal FixedFee, decimal PercentageFee, string FeeCurrencyCode,
    bool IsActive, string Revision, DateTime? UpdatedAtUtc);
