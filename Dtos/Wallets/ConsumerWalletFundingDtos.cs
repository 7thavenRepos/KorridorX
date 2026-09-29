using KorridorX.Models.Enums;

namespace KorridorX.Dtos.Wallets;

public sealed record CreateConsumerWalletFundingCollectionRequestDto(
    decimal Amount,
    PaymentMethod PaymentMethod);
