using KorridorX.Data;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.DigitalAssets;

public interface IDigitalAssetReconciliationService
{
    Task<IReadOnlyList<DigitalAssetNetworkTransaction>> GetExceptionsAsync(int take = 100, CancellationToken ct = default);
}

public sealed class DigitalAssetReconciliationService : IDigitalAssetReconciliationService
{
    private readonly AppDbContext _db;
    public DigitalAssetReconciliationService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DigitalAssetNetworkTransaction>> GetExceptionsAsync(int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        return await _db.DigitalAssetNetworkTransactions.AsNoTracking()
            .Where(x => !x.IsDeleted && (
                x.Status == DigitalAssetTransactionStatus.Failed ||
                (x.Direction == DigitalAssetTransactionDirection.Inbound &&
                 x.Status == DigitalAssetTransactionStatus.Confirmed &&
                 (!x.CollectionId.HasValue || !x.LedgerTransactionId.HasValue)) ||
                (x.Direction == DigitalAssetTransactionDirection.Outbound &&
                 x.Status == DigitalAssetTransactionStatus.Confirmed &&
                 !x.PayoutId.HasValue)))
            .OrderByDescending(x => x.LastUpdatedAt ?? x.CreatedAt)
            .Take(take).ToListAsync(ct);
    }
}
