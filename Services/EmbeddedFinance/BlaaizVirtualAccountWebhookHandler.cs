using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class BlaaizVirtualAccountWebhookHandler(AppDbContext source)
{
    public async Task<bool> ProcessAsync(string eventType, string rawPayload, CancellationToken ct = default)
    {
        var message = BlaaizVirtualAccountState.ParseWebhook(rawPayload, eventType);
        // Commit the account and its business outbox together. A failed attempt must
        // not leave tracked changes that the caller's error-log save could persist.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(source.Database.GetConnectionString()).Options;
        await using var db = new AppDbContext(options);
        var provider = ProviderCode.Blaaiz.ToString();
        var query = db.ProviderAccountMappings
            .Include(x => x.CollectionAccount).ThenInclude(x => x.BusinessCustomer)
            .Where(x => x.ProviderCode == provider && !x.IsDeleted &&
                !x.CollectionAccount.IsDeleted && !x.CollectionAccount.BusinessCustomer.IsDeleted);
        var mapping = await query.SingleOrDefaultAsync(x => x.ProviderAccountId == message.Id, ct);
        if (mapping is null)
        {
            // The callback can beat the POST response. Only the saved customer,
            // currency and durable wallet selection may identify an unbound mapping.
            var candidates = await query.Where(x => x.ProviderAccountId == null &&
                x.Status == ProviderAccountMappingStatus.Pending &&
                x.CollectionAccount.AssetCode == message.Currency &&
                db.ProviderCustomers.Any(c => !c.IsDeleted && c.ProviderCode == ProviderCode.Blaaiz &&
                    c.BusinessCustomerId == x.CollectionAccount.BusinessCustomerId && c.ProviderCustomerId == message.CustomerId) &&
                db.ProviderWalletSelections.Any(w => w.OperationType == "CollectionAccount" &&
                    w.OperationId == x.CollectionAccountId && w.Purpose == ProviderWalletResolver.Collection &&
                    w.ProviderWalletId == message.WalletId)).Take(2).ToListAsync(ct);
            if (candidates.Count != 1)
                throw new InvalidOperationException("Virtual-account event does not identify exactly one requested customer account.");
            mapping = candidates[0];
        }
        var selection = await db.ProviderWalletSelections.AsNoTracking().Include(x => x.WalletConfiguration)
            .SingleOrDefaultAsync(x => x.OperationType == "CollectionAccount" && x.OperationId == mapping.CollectionAccountId, ct)
            ?? throw new InvalidOperationException("Virtual-account event has no durable wallet selection.");
        if (selection.ProviderWalletId != message.WalletId || selection.Purpose != ProviderWalletResolver.Collection ||
            selection.WalletConfiguration.ProviderCode != "BLAAIZ" ||
            selection.WalletConfiguration.AssetCode != message.Currency || selection.WalletConfiguration.NetworkCode != "")
            throw new InvalidOperationException("Virtual-account event disagrees with its recorded wallet route.");
        if (!await db.ProviderCustomers.AsNoTracking().AnyAsync(c => !c.IsDeleted &&
            c.ProviderCode == ProviderCode.Blaaiz && c.BusinessCustomerId == mapping.CollectionAccount.BusinessCustomerId &&
            c.ProviderCustomerId == message.CustomerId, ct))
            throw new InvalidOperationException("Virtual-account event belongs to a different customer.");

        var prior = mapping.Status;
        var priorAccount = mapping.CollectionAccount.Status;
        message.Apply(mapping, selection.ProviderWalletId);
        if (mapping.Status != prior)
        {
            new EmbeddedWebhookOutboxStager(db).Stage(mapping.CollectionAccount.BusinessProfileId,
                "account.provisioning.updated", new
                {
                    collectionAccountId = mapping.CollectionAccountId,
                    businessCustomerId = mapping.CollectionAccount.BusinessCustomerId,
                    assetCode = mapping.CollectionAccount.AssetCode,
                    provider = mapping.ProviderCode,
                    status = mapping.Status.ToString(),
                    failureReason = mapping.FailureReason
                });
        }
        if (mapping.CollectionAccount.Status != priorAccount)
        {
            new EmbeddedWebhookOutboxStager(db).Stage(mapping.CollectionAccount.BusinessProfileId,
                "account.status.changed", new
                {
                    id = mapping.CollectionAccountId,
                    businessCustomerId = mapping.CollectionAccount.BusinessCustomerId,
                    assetCode = mapping.CollectionAccount.AssetCode,
                    previousStatus = priorAccount.ToString(),
                    status = mapping.CollectionAccount.Status.ToString(),
                    changedAt = mapping.LastUpdatedAt
                });
        }
        await db.SaveChangesAsync(ct);
        return true;
    }
}
