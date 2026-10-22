using System.Text.Json.Serialization;
using KorridorX.Models.Enums;

namespace KorridorX.Dtos.WalletWithdrawals;

public sealed class InitiateWalletWithdrawalRequestDto
{
    [JsonRequired] public Guid DestinationId { get; set; }
    [JsonRequired] public Guid ChannelId { get; set; }
    [JsonRequired] public decimal WithdrawalAmount { get; set; }
    // Required for consumer requests. Business access is MFA-authenticated.
    public string? TransactionPin { get; set; }
}

public sealed record WalletWithdrawalDetailsDto(
    Guid Id,
    Guid WalletId,
    Guid DestinationId,
    Guid ChannelId,
    string Reference,
    string CurrencyCode,
    decimal WithdrawalAmount,
    decimal ProviderFee,
    decimal KorridorXFee,
    string KorridorXFeePolicy,
    decimal TotalWalletDebit,
    decimal ExpectedRecipientAmount,
    FiatWalletWithdrawalStatus Status,
    PaymentMethod PaymentMethod,
    string? BankName,
    string? MaskedAccountNumber,
    string? RecipientEmail,
    DateTime FeeQuotedAtUtc,
    DateTime CreatedAt,
    bool ProviderDispatchEnabled);
