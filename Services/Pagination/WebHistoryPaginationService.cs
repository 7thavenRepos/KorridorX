using KorridorX.Data;
using KorridorX.Dtos.BusinessDigitalAssets;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Dtos.Instant;
using KorridorX.Dtos.Pagination;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Pagination;

public sealed class WebHistoryPaginationService(AppDbContext db, IBusinessAccessService access)
{
    public async Task<PagedResult<BusinessDigitalAssetActivityDto>> GetDigitalActivityAsync(
        Guid userId, WebHistoryPageQuery page, CancellationToken ct = default)
    {
        var scope = await access.EnsurePermissionAsync(userId, BusinessPermission.ViewDigitalAssets, ct);
        if (!await db.BusinessProfiles.AsNoTracking().AnyAsync(x => x.Id == scope.BusinessProfileId && !x.IsDeleted, ct))
            throw new InvalidOperationException("Business profile not found.");

        var keys = await ReadPageAsync(WebHistoryQueries.DigitalActivityKeys(db, scope.BusinessProfileId), page, ct);
        var items = new Dictionary<(int Kind, Guid Id), BusinessDigitalAssetActivityDto>();
        var collectionIds = keys.Items.Where(x => x.Kind == 1).Select(x => x.Id).ToArray();
        var payoutIds = keys.Items.Where(x => x.Kind == 2).Select(x => x.Id).ToArray();
        var intentIds = keys.Items.Where(x => x.Kind == 3).Select(x => x.Id).ToArray();

        // Only hydrate IDs from the requested page, and retain the business filters on every read.
        if (collectionIds.Length > 0)
        {
            var rows = await WebHistoryQueries.DigitalCollections(db, scope.BusinessProfileId)
                .Where(x => collectionIds.Contains(x.Id)).ToListAsync(ct);
            foreach (var x in rows)
                items[(1, x.Id)] = new(x.Id, "Deposit", x.Reference, x.CurrencyCode, null,
                    x.Amount, x.Status.ToString(), x.CreatedAt, x.ConfirmedAt);
        }
        if (payoutIds.Length > 0)
        {
            var rows = await WebHistoryQueries.DigitalPayouts(db, scope.BusinessProfileId)
                .Where(x => payoutIds.Contains(x.Id)).ToListAsync(ct);
            foreach (var x in rows)
                items[(2, x.Id)] = new(x.Id, "Withdrawal", x.Reference, x.CurrencyCode, null,
                    x.Amount, x.Status.ToString(), x.CreatedAt, x.CompletedAt);
        }
        if (intentIds.Length > 0)
        {
            var rows = await WebHistoryQueries.PendingDigitalIntents(db, scope.BusinessProfileId)
                .Where(x => intentIds.Contains(x.Id)).ToListAsync(ct);
            foreach (var x in rows)
                items[(3, x.Id)] = new(x.Id, "Deposit intent", x.ProviderCollectionId ?? x.Id.ToString("N"),
                    x.AssetCode, x.NetworkCode, x.Amount, x.Status.ToString(), x.CreatedAt, x.CompletedAt);
        }
        return new PagedResult<BusinessDigitalAssetActivityDto>
        {
            Meta = keys.Meta,
            Items = keys.Items.Where(x => items.ContainsKey((x.Kind, x.Id)))
                .Select(x => items[(x.Kind, x.Id)]).ToList()
        };
    }

    public async Task<PagedResult<BusinessWebhookDeliveryDto>> GetWebhookDeliveriesAsync(
        Guid userId, WebHistoryPageQuery page, BusinessWebhookDeliveryStatus? status, CancellationToken ct = default)
    {
        var scope = await access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        return await ReadPageAsync(WebHistoryQueries.WebhookDeliveries(db, scope.BusinessProfileId, status), page, ct);
    }

    public Task<PagedResult<DigitalAssetNetworkTransactionDto>> GetReconciliationExceptionsAsync(
        WebHistoryPageQuery page, CancellationToken ct = default) =>
        ReadPageAsync(WebHistoryQueries.ReconciliationExceptions(db), page, ct);

    public Task<PagedResult<DigitalAssetFeeExceptionDto>> GetFeeExceptionsAsync(
        WebHistoryPageQuery page, decimal minimumAbsoluteVariance, CancellationToken ct = default) =>
        ReadPageAsync(WebHistoryQueries.FeeExceptions(db, minimumAbsoluteVariance), page, ct);

    public async Task<PagedResult<DigitalAssetDepositIntentDto>> GetDepositIntentsAsync(
        Guid userId, WebHistoryPageQuery page, CancellationToken ct = default)
    {
        var businessId = await EnsureDigitalHistoryAccessAsync(userId, ct);
        return await ReadPageAsync(WebAdditionalHistoryQueries.DepositIntents(db, businessId), page, ct);
    }

    public async Task<PagedResult<DigitalAssetWithdrawalDto>> GetWithdrawalsAsync(
        Guid userId, WebHistoryPageQuery page, CancellationToken ct = default)
    {
        var businessId = await EnsureDigitalHistoryAccessAsync(userId, ct);
        return await ReadPageAsync(WebAdditionalHistoryQueries.Withdrawals(db, businessId), page, ct);
    }

    public Task<PagedResult<InstantSettlementExceptionDto>> GetSettlementExceptionsAsync(
        WebHistoryPageQuery page, CancellationToken ct = default) =>
        ReadPageAsync(WebAdditionalHistoryQueries.SettlementExceptions(db, DateTime.UtcNow.AddMinutes(-5)), page, ct);

    private async Task<Guid> EnsureDigitalHistoryAccessAsync(Guid userId, CancellationToken ct)
    {
        var scope = await access.EnsurePermissionAsync(userId, BusinessPermission.ViewDigitalAssets, ct);
        if (!await db.BusinessProfiles.AsNoTracking().AnyAsync(x => x.Id == scope.BusinessProfileId && !x.IsDeleted, ct))
            throw new InvalidOperationException("Business profile not found.");
        return scope.BusinessProfileId;
    }

    private static async Task<PagedResult<T>> ReadPageAsync<T>(IQueryable<T> query, WebHistoryPageQuery page, CancellationToken ct)
    {
        // Validate even for a non-HTTP caller, before performing a database operation.
        page.ToMeta(0);
        var total = await query.CountAsync(ct);
        var meta = page.ToMeta(total);
        var rows = await query.Skip(checked((meta.Page - 1) * meta.PageSize)).Take(meta.PageSize).ToListAsync(ct);
        return new PagedResult<T> { Items = rows, Meta = meta };
    }
}
