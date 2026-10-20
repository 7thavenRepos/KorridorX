using KorridorX.Dtos.PaymentChannels;

namespace KorridorX.Dtos.Wallets;

public sealed record ConsumerWalletReceivingAccountDto(
    string Status,
    string Message,
    Guid? ChannelId,
    WalletDepositAccountDto? Account,
    bool CanRetry);
