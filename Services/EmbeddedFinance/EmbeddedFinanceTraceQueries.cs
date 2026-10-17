using KorridorX.Data;
using KorridorX.Models.Audit;
using KorridorX.Models.Finance;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Providers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

/// <summary>Unpaged trace scopes. Count and page the same scope; never derive a scope from a visible page.</summary>
public static class EmbeddedFinanceTraceQueries
{
    public static IOrderedQueryable<ProviderTransaction> ProviderTransactions(AppDbContext db, HashSet<Guid> traceIds) =>
        db.ProviderTransactions.AsNoTracking().Where(x => !x.IsDeleted &&
            ((x.TransferId.HasValue && traceIds.Contains(x.TransferId.Value)) ||
             (x.CollectionId.HasValue && traceIds.Contains(x.CollectionId.Value)) ||
             (x.PayoutId.HasValue && traceIds.Contains(x.PayoutId.Value))))
        .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);

    public static IOrderedQueryable<ProviderRequestLog> ProviderRequests(AppDbContext db, HashSet<Guid> traceIds) =>
        db.ProviderRequestLogs.AsNoTracking().Where(x => !x.IsDeleted &&
            ((x.RelatedTransferId.HasValue && traceIds.Contains(x.RelatedTransferId.Value)) ||
             (x.RelatedCollectionId.HasValue && traceIds.Contains(x.RelatedCollectionId.Value)) ||
             (x.RelatedPayoutId.HasValue && traceIds.Contains(x.RelatedPayoutId.Value))))
        .OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id);

    public static IOrderedQueryable<FinancialReservation> Reservations(AppDbContext db, Guid activityId, HashSet<Guid> traceIds) =>
        db.FinancialReservations.AsNoTracking().Where(x => !x.IsDeleted &&
            (x.Id == activityId || traceIds.Contains(x.RelatedEntityId)))
        .OrderByDescending(x => x.ReservedAt).ThenByDescending(x => x.Id);

    public static IOrderedQueryable<LedgerTransaction> Ledgers(AppDbContext db, HashSet<Guid> traceIds) =>
        db.LedgerTransactions.AsNoTracking().Where(x => !x.IsDeleted && x.RelatedEntityId.HasValue &&
            traceIds.Contains(x.RelatedEntityId.Value))
        .OrderByDescending(x => x.PostedAt).ThenByDescending(x => x.Id);

    public static IOrderedQueryable<JournalEntry> Journals(AppDbContext db, Guid activityId, HashSet<Guid> traceIds)
    {
        // These are SQL subqueries over ALL matching reservations/ledgers, not their displayed pages.
        var reservationIds = Reservations(db, activityId, traceIds).Select(x => x.Id);
        var ledgerIds = Ledgers(db, traceIds).Select(x => x.Id);
        return db.JournalEntries.AsNoTracking().Where(x => !x.IsDeleted && x.SourceId.HasValue &&
                (traceIds.Contains(x.SourceId.Value) || reservationIds.Contains(x.SourceId.Value) ||
                 ledgerIds.Contains(x.SourceId.Value)))
            .OrderByDescending(x => x.PostedAt).ThenByDescending(x => x.Id);
    }

    public static IOrderedQueryable<AuditLog> AuditTimeline(AppDbContext db, string entityName,
        Guid activityId, Guid customerId, Guid? accountId, Guid? applicationId)
    {
        var activity = activityId.ToString();
        var customer = customerId.ToString();
        var account = accountId?.ToString();
        var application = applicationId?.ToString();
        var linkedAccountId = accountId ?? Guid.Empty;
        var mappingIds = db.ProviderAccountMappings.AsNoTracking()
            .Where(x => !x.IsDeleted && accountId.HasValue && x.CollectionAccountId == linkedAccountId)
            .Select(x => x.Id.ToString());
        return db.AuditLogs.AsNoTracking().Where(x => !x.IsDeleted && x.Category == "EmbeddedFinance" &&
                ((x.EntityName == entityName && x.EntityId == activity) ||
                 (x.EntityName == "BusinessCustomer" && x.EntityId == customer) ||
                 (account != null && x.EntityName == "CollectionAccount" && x.EntityId == account) ||
                 (application != null && x.EntityName == "ApiApplication" && x.EntityId == application) ||
                 (x.EntityName == "ProviderAccountMapping" && x.EntityId != null && mappingIds.Contains(x.EntityId))))
            .OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id);
    }
}
