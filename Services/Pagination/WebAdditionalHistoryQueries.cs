using KorridorX.Data;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Dtos.Instant;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Pagination;

/// <summary>Additional ordered history reads. Existing command and legacy list APIs are unchanged.</summary>
public static class WebAdditionalHistoryQueries
{
    public static IQueryable<DigitalAssetDepositIntentDto> DepositIntents(AppDbContext db, Guid businessId) =>
        db.DigitalAssetDepositIntents.AsNoTracking()
            .Where(x => x.BusinessProfileId == businessId && x.BusinessCustomerId == null && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new DigitalAssetDepositIntentDto(
                x.Id, x.FinancialAccountId, x.AssetNetworkId, x.AssetCode, x.NetworkCode,
                x.ProviderCode, x.Amount, x.Address, x.ProviderCollectionId, x.ProviderReference,
                x.Status, x.ProviderExpiresAt, x.CreatedAt, x.CompletedAt, x.FailureReason));

    public static IQueryable<DigitalAssetWithdrawal> WithdrawalRows(AppDbContext db, Guid businessId) =>
        db.DigitalAssetWithdrawals.AsNoTracking()
            .Where(x => x.BusinessProfileId == businessId && x.BusinessCustomerId == null && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id);

    public static IQueryable<DigitalAssetWithdrawalDto> Withdrawals(AppDbContext db, Guid businessId) =>
        WithdrawalRows(db, businessId)
            .Select(x => new DigitalAssetWithdrawalDto(
                x.Id, x.PayoutId, x.FinancialAccountId, x.DestinationId, x.AssetNetworkId,
                x.AssetCode, x.AssetNetwork.NetworkCode, x.ProviderCode,
                x.Amount, x.NetworkFee, x.TotalDebitAmount, x.Status,
                db.DigitalAssetNetworkTransactions.Where(t => t.PayoutId == x.PayoutId && !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
                    .Select(t => t.TransactionHash).FirstOrDefault(),
                db.DigitalAssetNetworkTransactions.Where(t => t.PayoutId == x.PayoutId && !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
                    .Select(t => (int?)t.Confirmations).FirstOrDefault() ?? 0,
                db.DigitalAssetNetworkTransactions.Where(t => t.PayoutId == x.PayoutId && !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
                    .Select(t => (int?)t.RequiredConfirmations).FirstOrDefault() ?? x.AssetNetwork.RequiredConfirmations,
                x.FailureReason, x.CreatedAt, x.CompletedAt));

    public static IQueryable<InstantSettlementExceptionDto> SettlementExceptions(AppDbContext db, DateTime staleBefore) =>
        db.InstantTrades.AsNoTracking()
            .Where(x => !x.IsDeleted &&
                (x.Status == InstantTradeStatus.Failed ||
                 ((x.Status == InstantTradeStatus.PendingSettlement || x.Status == InstantTradeStatus.Settling) &&
                  x.CreatedAt <= staleBefore)))
            .OrderBy(x => x.Status).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new InstantSettlementExceptionDto(
                x.Id, x.Reference, x.InstantQuoteId, x.InstantPairId, x.InstantPair.Code,
                x.Status, x.CreatedAt, x.SettlementStartedAt, x.FailedAt, x.FailureReason));
}
