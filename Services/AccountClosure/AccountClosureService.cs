using KorridorX.Data;
using KorridorX.Dtos.AccountClosure;
using KorridorX.Dtos.Audit;
using KorridorX.Extensions;
using KorridorX.Exceptions;
using KorridorX.Infrastructure;
using KorridorX.Models.AccountClosure;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.Audit;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.AccountClosure;

/// <summary>
/// W11C.5 first slice: request, status and withdrawal only. Final closure,
/// provider account retirement and PII deletion are deliberately NOT enabled.
/// </summary>
public sealed class AccountClosureService(
    AppDbContext db,
    IBusinessAccessService businessAccess,
    INotificationQueueService notifications,
    IAuditService audit) : IAccountClosureService
{
    public Task<AccountClosureOverviewDto> GetPersonalAsync(Guid userId, CancellationToken ct) =>
        OverviewAsync(AccountClosureScope.Personal, userId, ct);

    public Task<AccountClosureOverviewDto> RequestPersonalAsync(Guid userId, CancellationToken ct) =>
        RequestAsync(AccountClosureScope.Personal, userId, userId, ct);

    public Task<AccountClosureOverviewDto> WithdrawPersonalAsync(Guid userId, CancellationToken ct) =>
        WithdrawAsync(AccountClosureScope.Personal, userId, userId, ct);

    public async Task<AccountClosureOverviewDto> GetBusinessAsync(Guid userId, CancellationToken ct)
    {
        var businessId = await RequireOwnerAsync(userId, ct);
        return await OverviewAsync(AccountClosureScope.Business, businessId, ct);
    }

    public async Task<AccountClosureOverviewDto> RequestBusinessAsync(Guid userId, CancellationToken ct)
    {
        var businessId = await RequireOwnerAsync(userId, ct);
        return await RequestAsync(AccountClosureScope.Business, businessId, userId, ct);
    }

    public async Task<AccountClosureOverviewDto> WithdrawBusinessAsync(Guid userId, CancellationToken ct)
    {
        var businessId = await RequireOwnerAsync(userId, ct);
        return await WithdrawAsync(AccountClosureScope.Business, businessId, userId, ct);
    }

    public async Task<PagedResult<AccountClosureQueueRowDto>> ListForOperationsAsync(
        int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return await db.AccountClosureRequests.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.RequestedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new AccountClosureQueueRowDto(
                x.Id, x.Scope.ToString(), x.SubjectId, x.RequestedByUserId,
                x.Status.ToString(), x.RequestedAtUtc, x.WithdrawnAtUtc))
            .PaginateAsync(page, pageSize, ct);
    }

    private async Task<Guid> RequireOwnerAsync(Guid actorUserId, CancellationToken ct)
    {
        var access = await businessAccess.GetAccessAsync(actorUserId, ct);
        if (!access.IsOwner || access.OwnerUserId != actorUserId)
            throw new ForbiddenException("Only the owner of the selected business can request its closure.");
        return access.BusinessProfileId;
    }

    private async Task<AccountClosureOverviewDto> OverviewAsync(
        AccountClosureScope scope, Guid subjectId, CancellationToken ct)
    {
        var open = await db.AccountClosureRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.Scope == scope &&
                x.SubjectId == subjectId && x.Status == AccountClosureRequestStatus.Requested, ct);
        return await BuildOverviewAsync(scope, subjectId, open, ct);
    }

    private async Task<AccountClosureOverviewDto> RequestAsync(
        AccountClosureScope scope, Guid subjectId, Guid actorUserId, CancellationToken ct)
    {
        var open = await db.AccountClosureRequests
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.Scope == scope &&
                x.SubjectId == subjectId && x.Status == AccountClosureRequestStatus.Requested, ct);
        if (open is not null)
            return await BuildOverviewAsync(scope, subjectId, open, ct);

        // A closure request never bypasses financial obligations. Request acceptance
        // is allowed even while obligations remain and never disables sign-in.
        var request = new AccountClosureRequest
        {
            Scope = scope, SubjectId = subjectId, RequestedByUserId = actorUserId,
            Status = AccountClosureRequestStatus.Requested,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId
        };
        db.AccountClosureRequests.Add(request);
        audit.Stage(new AuditRecordRequest(
            Action: "ACCOUNT_CLOSURE_REQUESTED", Category: "AccountClosure",
            EntityName: nameof(AccountClosureRequest), EntityId: request.Id.ToString(),
            NewValues: new { request.Scope, request.SubjectId, request.Status },
            UserId: actorUserId));
        await notifications.QueueUserAsync(actorUserId,
            "Account closure request received",
            "<p>We received your request. Your account remains active while we review any outstanding obligations. You can view the request in Settings.</p>",
            nameof(AccountClosureRequest), request.Id, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Partial unique index won the race in a different request.
            db.ChangeTracker.Clear();
            var winner = await db.AccountClosureRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => !x.IsDeleted && x.Scope == scope &&
                    x.SubjectId == subjectId && x.Status == AccountClosureRequestStatus.Requested, ct);
            if (winner is null) throw;
            return await BuildOverviewAsync(scope, subjectId, winner, ct);
        }
        return await BuildOverviewAsync(scope, subjectId, request, ct);
    }

    private async Task<AccountClosureOverviewDto> WithdrawAsync(
        AccountClosureScope scope, Guid subjectId, Guid actorUserId, CancellationToken ct)
    {
        var request = await db.AccountClosureRequests.SingleOrDefaultAsync(x =>
            !x.IsDeleted && x.Scope == scope && x.SubjectId == subjectId &&
            x.Status == AccountClosureRequestStatus.Requested, ct);
        if (request is null)
            return await BuildOverviewAsync(scope, subjectId, null, ct);

        // Audit and state change share one transaction; a failed audit may not
        // silently leave a request withdrawn without an audit trail.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var updated = await db.AccountClosureRequests.Where(x =>
                    x.Id == request.Id && x.Status == AccountClosureRequestStatus.Requested && !x.IsDeleted)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.Status, AccountClosureRequestStatus.Withdrawn)
                    .SetProperty(x => x.WithdrawnAtUtc, now)
                    .SetProperty(x => x.LastUpdatedAt, now)
                    .SetProperty(x => x.LastUpdatedByUserId, actorUserId), ct);
            if (updated != 1)
                throw new InvalidOperationException("Closure request changed; refresh its status.");
            // ExecuteUpdate skips the tracker. Discard its old snapshot.
            db.ChangeTracker.Clear();
            audit.Stage(new AuditRecordRequest(
                Action: "ACCOUNT_CLOSURE_REQUEST_WITHDRAWN", Category: "AccountClosure",
                EntityName: nameof(AccountClosureRequest), EntityId: request.Id.ToString(),
                NewValues: new { Status = AccountClosureRequestStatus.Withdrawn },
                UserId: actorUserId));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        return await BuildOverviewAsync(scope, subjectId, null, ct);
    }

    private async Task<AccountClosureOverviewDto> BuildOverviewAsync(
        AccountClosureScope scope, Guid subjectId,
        AccountClosureRequest? request, CancellationToken ct)
    {
        var blockers = await GetBlockersAsync(scope, subjectId, ct);
        return new AccountClosureOverviewDto(
            SubjectId: subjectId, Scope: scope.ToString(),
            OpenRequestId: request?.Id,
            RequestStatus: request?.Status.ToString(),
            RequestedAtUtc: request?.RequestedAtUtc,
            MayRequest: true,
            ReadyForOperationsReview: blockers.Count == 0,
            FinalizationAvailable: false,
            Blockers: blockers);
    }

    private async Task<IReadOnlyList<string>> GetBlockersAsync(
        AccountClosureScope scope, Guid subjectId, CancellationToken ct)
    {
        var blockers = new List<string>();
        var personal = scope == AccountClosureScope.Personal;
        Guid[] customerIds = [];
        Guid[] businessCustomerIds = [];
        if (personal)
        {
            customerIds = await db.CustomerProfiles.AsNoTracking()
                .Where(x => x.UserId == subjectId && !x.IsDeleted)
                .Select(x => x.Id).ToArrayAsync(ct);
        }
        else
        {
            businessCustomerIds = await db.BusinessCustomers.AsNoTracking()
                .Where(x => x.BusinessProfileId == subjectId && !x.IsDeleted)
                .Select(x => x.Id).ToArrayAsync(ct);
        }

        var accounts = db.FinancialAccounts.AsNoTracking()
            .Where(x => !x.IsDeleted && (personal
                ? x.OwnerType == FinancialAccountOwnerType.User && x.OwnerId == subjectId
                : x.OwnerType == FinancialAccountOwnerType.Business && x.OwnerId == subjectId ||
                  x.OwnerType == FinancialAccountOwnerType.BusinessCustomer &&
                  businessCustomerIds.Contains(x.OwnerId)));

        if (await accounts.AnyAsync(x => x.SettledBalance != 0m ||
                x.AvailableBalance != 0m || x.HeldBalance != 0m, ct))
            blockers.Add("Funds remain in a wallet. Withdraw or resolve balances before closure.");

        var accountIds = await accounts.Select(x => x.Id).ToArrayAsync(ct);
        if (accountIds.Length > 0 && await db.FinancialReservations.AsNoTracking()
                .AnyAsync(x => !x.IsDeleted && accountIds.Contains(x.FinancialAccountId) &&
                    x.Status == FinancialReservationStatus.Active, ct))
            blockers.Add("There are outstanding wallet reservations or held funds.");

        var transfers = db.Transfers.AsNoTracking().Where(x => !x.IsDeleted &&
            (personal ? x.CustomerProfileId.HasValue && customerIds.Contains(x.CustomerProfileId.Value)
                : x.BusinessProfileId == subjectId));
        if (await transfers.AnyAsync(x => x.Status != TransferStatus.Completed &&
                x.Status != TransferStatus.Cancelled && x.Status != TransferStatus.Refunded &&
                x.Status != TransferStatus.Rejected, ct))
            blockers.Add("At least one transfer is unfinished or requires review.");

        if (await db.FiatWalletWithdrawals.AsNoTracking().AnyAsync(x => !x.IsDeleted &&
                x.OwnerId == subjectId && (personal
                    ? x.OwnerType == FinancialAccountOwnerType.User
                    : x.OwnerType == FinancialAccountOwnerType.Business) &&
                x.Status != FiatWalletWithdrawalStatus.Succeeded &&
                x.Status != FiatWalletWithdrawalStatus.FailedReleased &&
                x.Status != FiatWalletWithdrawalStatus.CancelledReleased, ct))
            blockers.Add("A wallet withdrawal is unsettled or under review.");

        if (await db.Payouts.AsNoTracking().AnyAsync(x => !x.IsDeleted &&
                x.Transfer != null && (personal
                    ? x.Transfer.CustomerProfileId.HasValue &&
                      customerIds.Contains(x.Transfer.CustomerProfileId.Value)
                    : x.Transfer.BusinessProfileId == subjectId) &&
                x.Status != PayoutStatus.Successful && x.Status != PayoutStatus.Failed, ct))
            blockers.Add("A transfer payout still requires resolution.");

        if (await db.Collections.AsNoTracking().AnyAsync(x => !x.IsDeleted &&
                x.Transfer != null && (personal
                    ? x.Transfer.CustomerProfileId.HasValue &&
                      customerIds.Contains(x.Transfer.CustomerProfileId.Value)
                    : x.Transfer.BusinessProfileId == subjectId) &&
                x.Status != CollectionStatus.Successful && x.Status != CollectionStatus.Failed &&
                x.Status != CollectionStatus.Cancelled && x.Status != CollectionStatus.Refunded &&
                x.Status != CollectionStatus.Expired, ct))
            blockers.Add("A payment or refund remains in progress.");

        if (await db.SupportTickets.AsNoTracking().AnyAsync(x => !x.IsDeleted &&
                (personal ? x.UserId == subjectId : x.BusinessProfileId == subjectId) &&
                x.Status != SupportTicketStatus.Closed && x.Status != SupportTicketStatus.Resolved, ct))
            blockers.Add("An open support issue requires review.");

        if (await db.TransferDisputes.AsNoTracking().AnyAsync(x => !x.IsDeleted &&
                (personal ? x.UserId == subjectId : x.BusinessProfileId == subjectId) &&
                x.Status != TransferDisputeStatus.Resolved &&
                x.Status != TransferDisputeStatus.Rejected &&
                x.Status != TransferDisputeStatus.Withdrawn, ct))
            blockers.Add("An open transfer dispute requires review.");

        if (personal)
        {
            if (await db.BusinessProfiles.AsNoTracking().AnyAsync(x =>
                    !x.IsDeleted && x.OwnerUserId == subjectId, ct))
                blockers.Add("You own a business workspace that must be addressed separately.");
            if (await db.BusinessUsers.AsNoTracking().AnyAsync(x =>
                    !x.IsDeleted && x.UserId == subjectId && x.IsActive &&
                    !x.BusinessProfile.IsDeleted, ct))
                blockers.Add("You have active business memberships to resolve.");
        }
        else
        {
            if (await db.BusinessPaymentBatches.AsNoTracking().AnyAsync(x =>
                    !x.IsDeleted && x.BusinessProfileId == subjectId &&
                    x.Status != BusinessPaymentBatchStatus.Completed &&
                    x.Status != BusinessPaymentBatchStatus.Rejected &&
                    x.Status != BusinessPaymentBatchStatus.Cancelled, ct))
                blockers.Add("Business batch payments or approvals require review.");
            if (await db.DigitalAssetWithdrawals.AsNoTracking().AnyAsync(x =>
                    !x.IsDeleted && x.BusinessProfileId == subjectId &&
                    x.Status != DigitalAssetWithdrawalStatus.Completed &&
                    x.Status != DigitalAssetWithdrawalStatus.Failed &&
                    x.Status != DigitalAssetWithdrawalStatus.Cancelled, ct))
                blockers.Add("A digital-asset withdrawal is still pending.");
            if (await db.BusinessCustomers.AsNoTracking().AnyAsync(x =>
                    !x.IsDeleted && x.BusinessProfileId == subjectId, ct))
                blockers.Add("Embedded-finance customer accounts require operations review.");
        }

        var subjectText = subjectId.ToString("D");
        if (await db.LegalHolds.AsNoTracking().AnyAsync(x =>
                !x.IsDeleted && x.Status == LegalHoldStatus.Active &&
                (x.AppliesToAllComplianceData || x.EntityId == subjectText), ct))
            blockers.Add("A legal or compliance retention hold requires review.");

        // This slice intentionally never returns authorization to finalize.
        // Full checks (provider accounts, API keys, KYB/KYC, regulatory retention,
        // incoming funding and data deletion) are the next reviewed gate.
        return blockers;
    }
}
