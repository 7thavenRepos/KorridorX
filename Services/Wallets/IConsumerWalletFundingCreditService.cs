using KorridorX.Models.Payments;

namespace KorridorX.Services.Wallets;

public interface IConsumerWalletFundingCreditService
{
    Task ApplySuccessfulCollectionAsync(
        Collection collection,
        Guid? actionedByUserId,
        string source,
        CancellationToken ct = default);
}
