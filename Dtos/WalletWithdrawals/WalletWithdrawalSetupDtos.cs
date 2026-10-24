using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.WalletWithdrawals;

public sealed record WalletWithdrawalMethodDto(
    Guid ChannelId,
    string CurrencyCode,
    string DestinationCountryCode,
    PaymentMethod PaymentMethod,
    string DisplayName,
    decimal MinimumAmount,
    decimal? MaximumAmount);

public sealed record WalletWithdrawalDestinationDto(
    Guid Id,
    Guid WalletId,
    string CurrencyCode,
    string DestinationCountryCode,
    PaymentMethod PaymentMethod,
    string DisplayName,
    string? BankName,
    string? MaskedAccountNumber,
    string? VerifiedAccountName,
    string? Email,
    bool IsActive,
    DateTime VerifiedAtUtc);

public sealed class SaveWalletWithdrawalDestinationRequestDto
{
    // JSON presence is mandatory; the enum's default must not silently select bank transfer.
    [JsonRequired] public PaymentMethod PaymentMethod { get; set; }
    [MaxLength(100)] public string? ProviderBankId { get; set; }
    [MaxLength(34)] public string? AccountNumber { get; set; }
    [MaxLength(254)] public string? Email { get; set; }
    // The fields below are used only by the bank rail selected for USD/EUR/GBP.
    // Routing/address details are encrypted on the destination and withdrawal snapshots.
    [MaxLength(2)] public string? DestinationCountryCode { get; set; }
    [MaxLength(250)] public string? AccountName { get; set; }
    [MaxLength(150)] public string? BankName { get; set; }
    [MaxLength(20)] public string? AccountType { get; set; }
    [MaxLength(20)] public string? RoutingNumber { get; set; }
    [MaxLength(20)] public string? SwiftCode { get; set; }
    [MaxLength(20)] public string? SortCode { get; set; }
    [MaxLength(50)] public string? Iban { get; set; }
    [MaxLength(20)] public string? BicCode { get; set; }
    [MaxLength(100)] public string? State { get; set; }
    [MaxLength(150)] public string? Street { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(30)] public string? ZipCode { get; set; }
    // Consumer only. Never persisted, logged, returned, or forwarded to the payout provider.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TransactionPin { get; set; }
}

public sealed class DisableWalletWithdrawalDestinationRequestDto
{
    public string? TransactionPin { get; set; }
}
