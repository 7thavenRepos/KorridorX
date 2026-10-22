using System.Text.Json.Serialization;

namespace KorridorX.Dtos.WalletWithdrawals;

public sealed class WalletWithdrawalFeePreviewRequestDto
{
    [JsonRequired] public Guid DestinationId { get; set; }
    [JsonRequired] public Guid ChannelId { get; set; }
    // The entered amount INCLUDES the provider fee. It is the intended wallet debit.
    [JsonRequired] public decimal WithdrawalAmount { get; set; }
}

public sealed record WalletWithdrawalFeePreviewDto(
    Guid WalletId,
    Guid ChannelId,
    Guid DestinationId,
    string WalletCurrency,
    int CurrencyDecimalPlaces,
    decimal WithdrawalAmount,
    decimal ProviderFee,
    decimal KorridorXFee,
    string KorridorXFeePolicy,
    decimal TotalWalletDebit,
    decimal ExpectedRecipientAmount,
    decimal AvailableBalance,
    WalletWithdrawalDestinationDto Destination,
    DateTime QuotedAtUtc,
    bool RequiresFreshQuoteBeforeExecution);
