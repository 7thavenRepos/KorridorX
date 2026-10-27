using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.AdminFunding;
using KorridorX.Dtos.Audit;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.AdminFunding;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.Audit;
using KorridorX.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.AdminFunding;

/// <summary>
/// Internal house-account-backed wallet funding. It never calls a payout or deposit provider,
/// never treats external provider observations as transferable settled cash, and never
/// issues an unbacked customer balance adjustment.
/// </summary>
public sealed class AdminWalletFundingService(
    AppDbContext db,
    IAuditService audit,
    INotificationQueueService notifications)
{
    public async Task<PagedResult<AdminWalletFundingWalletDto>> SearchWalletsAsync(
        string? search, string? assetCode, int page, int pageSize, CancellationToken ct)
    {
        var query = db.FinancialAccounts.AsNoTracking().Where(x =>
            !x.IsDeleted && x.AccountType == FinancialAccountType.Customer &&
            (x.OwnerType == FinancialAccountOwnerType.User || x.OwnerType == FinancialAccountOwnerType.Business));
        if (!string.IsNullOrWhiteSpace(assetCode))
        {
            var asset = assetCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.AssetCode == asset);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Trim() + "%";
            query = query.Where(x =>
                x.OwnerType == FinancialAccountOwnerType.User && db.Users.Any(u =>
                    u.Id == x.OwnerId && (EF.Functions.ILike(u.Email ?? "", pattern) ||
                    EF.Functions.ILike(u.FirstName, pattern) || EF.Functions.ILike(u.LastName, pattern))) ||
                x.OwnerType == FinancialAccountOwnerType.Business && db.BusinessProfiles.Any(b =>
                    b.Id == x.OwnerId && !b.IsDeleted &&
                    (EF.Functions.ILike(b.BusinessName, pattern) ||
                     EF.Functions.ILike(b.TradingName ?? "", pattern) ||
                     EF.Functions.ILike(b.ContactEmail ?? "", pattern))));
        }
        var paged = await query.OrderBy(x => x.AssetCode).ThenBy(x => x.OwnerType)
            .ThenBy(x => x.OwnerId).ThenBy(x => x.Id)
            .PaginateAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), ct);
        var userIds = paged.Items.Where(x => x.OwnerType == FinancialAccountOwnerType.User)
            .Select(x => x.OwnerId).Distinct().ToArray();
        var businessIds = paged.Items.Where(x => x.OwnerType == FinancialAccountOwnerType.Business)
            .Select(x => x.OwnerId).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email })
            .ToDictionaryAsync(x => x.Id, ct);
        var businesses = await db.BusinessProfiles.AsNoTracking().Where(x => businessIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.BusinessName, x.ContactEmail })
            .ToDictionaryAsync(x => x.Id, ct);
        return new PagedResult<AdminWalletFundingWalletDto>
        {
            Meta = paged.Meta,
            Items = paged.Items.Select(x =>
            {
                var isUser = x.OwnerType == FinancialAccountOwnerType.User;
                var foundUser = isUser && users.TryGetValue(x.OwnerId, out _);
                var foundBusiness = !isUser && businesses.TryGetValue(x.OwnerId, out _);
                var name = isUser
                    ? foundUser ? $"{users[x.OwnerId].FirstName} {users[x.OwnerId].LastName}".Trim() : "Account unavailable"
                    : foundBusiness ? businesses[x.OwnerId].BusinessName : "Business unavailable";
                var email = isUser
                    ? foundUser ? users[x.OwnerId].Email : null
                    : foundBusiness ? businesses[x.OwnerId].ContactEmail : null;
                return new AdminWalletFundingWalletDto(x.Id, x.OwnerType, x.OwnerId, name, email,
                    x.AccountCode, x.AssetCode, x.Status, x.SettledBalance,
                    x.AvailableBalance, x.HeldBalance);
            }).ToList()
        };
    }

    public async Task<IReadOnlyList<AdminWalletFundingHouseAccountDto>> MasterWalletsAsync(
        string? assetCode, CancellationToken ct)
    {
        var query = db.FinancialAccounts.AsNoTracking().Where(x =>
            !x.IsDeleted && x.AccountType == FinancialAccountType.House &&
            (x.OwnerType == FinancialAccountOwnerType.Treasury ||
             x.OwnerType == FinancialAccountOwnerType.Platform));
        if (!string.IsNullOrWhiteSpace(assetCode))
        {
            var code = assetCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.AssetCode == code);
        }
        return await query.OrderBy(x => x.AssetCode).ThenBy(x => x.AccountCode)
            .Select(x => new AdminWalletFundingHouseAccountDto(x.Id, x.AccountCode, x.AssetCode,
                x.Status, x.SettledBalance, x.AvailableBalance, x.HeldBalance))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<AdminWalletFundingRequestDto>> RequestsAsync(
        AdminWalletFundingStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.AdminWalletFundingRequests.AsNoTracking().Where(x => !x.IsDeleted);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var result = await query.OrderByDescending(x => x.RequestedAtUtc).ThenBy(x => x.Id)
            .PaginateAsync(Math.Max(page, 1), Math.Clamp(pageSize, 1, 100), ct);
        return new PagedResult<AdminWalletFundingRequestDto>
        {
            Items = result.Items.Select(Map).ToList(),
            Meta = result.Meta
        };
    }

    public async Task<AdminWalletFundingRequestDto> GetRequestAsync(Guid id, CancellationToken ct)
    {
        var request = await db.AdminWalletFundingRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Wallet funding request not found.");
        return Map(request);
    }

    public async Task<AdminWalletFundingRequestDto> CreateAsync(
        Guid actorId, bool directAdminFunding, CreateAdminWalletFundingRequestDto command,
        string? idempotencyKey, CancellationToken ct)
    {
        RequireActor(actorId);
        var reason = CleanRequired(command.Reason, "Funding reason", 1000);
        var key = CleanRequired(idempotencyKey, "Idempotency-Key header", 160);
        if (command.Amount <= 0m)
            throw new InvalidOperationException("Funding amount must be greater than zero.");
        if (command.SourceHouseAccountId == command.DestinationWalletId ||
            command.SourceHouseAccountId == Guid.Empty || command.DestinationWalletId == Guid.Empty)
            throw new InvalidOperationException("Select a valid house account and a different customer/business wallet.");

        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new {
                command.SourceHouseAccountId, command.DestinationWalletId, command.Amount, Reason = reason
            }))));
        var previous = await db.AdminWalletFundingRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestedByUserId == actorId && x.IdempotencyKey == key, ct);
        if (previous is not null)
        {
            EnsureSameRequest(previous, requestHash);
            return Map(previous);
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Repeat lookup within the transaction. The unique index is the final race boundary.
        previous = await db.AdminWalletFundingRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestedByUserId == actorId && x.IdempotencyKey == key, ct);
        if (previous is not null)
        {
            EnsureSameRequest(previous, requestHash);
            return Map(previous);
        }

        // Checks master and destination identities before recording an Operations request.
        var accounts = directAdminFunding
            ? await LoadLockedPairAsync(command.SourceHouseAccountId, command.DestinationWalletId, ct)
            : await LoadPairAsync(command.SourceHouseAccountId, command.DestinationWalletId, ct);
        await ValidateAccountsAsync(accounts.Source, accounts.Destination, command.Amount, ct);
        // Operations requests do not reserve funds; confirm liquidity here and again at approval.
        if (accounts.Source.AvailableBalance < command.Amount ||
            accounts.Source.SettledBalance < command.Amount)
            throw new InvalidOperationException("Insufficient house-account liquidity for funding request.");

        var request = new AdminWalletFundingRequest
        {
            Reference = "KXWF-" + Guid.NewGuid().ToString("N"),
            SourceHouseAccountId = accounts.Source.Id,
            DestinationWalletId = accounts.Destination.Id,
            AssetCode = accounts.Source.AssetCode,
            Amount = command.Amount,
            Reason = reason,
            IdempotencyKey = key,
            RequestHash = requestHash,
            RequestedByUserId = actorId,
            RequestedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorId
        };
        db.AdminWalletFundingRequests.Add(request);
        audit.Stage(new AuditRecordRequest(
            "ADMIN_WALLET_FUNDING_REQUESTED", "WalletFunding", nameof(AdminWalletFundingRequest),
            request.Id.ToString(), NewValues: new {
                request.Reference, request.SourceHouseAccountId, request.DestinationWalletId,
                request.AssetCode, request.Amount, request.Status, DirectAdminFunding = directAdminFunding
            }, UserId: actorId));

        if (directAdminFunding)
            await PostUnderTransactionAsync(request, accounts.Source, accounts.Destination, actorId, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(request);
    }

    public async Task<AdminWalletFundingRequestDto> ApproveAsync(Guid actorId, Guid requestId, CancellationToken ct)
    {
        RequireActor(actorId);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.AdminWalletFundingRequests
            .FromSqlInterpolated($"SELECT * FROM \"AdminWalletFundingRequests\" WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Wallet funding request not found.");
        if (request.IsDeleted) throw new InvalidOperationException("Wallet funding request not found.");
        if (request.Status == AdminWalletFundingStatus.Posted) return Map(request);
        if (request.Status != AdminWalletFundingStatus.PendingApproval)
            throw new InvalidOperationException("Only pending funding requests can be approved.");
        if (request.RequestedByUserId == actorId)
            throw new InvalidOperationException("Funding request creators cannot approve their own requests.");

        var pair = await LoadLockedPairAsync(request.SourceHouseAccountId, request.DestinationWalletId, ct);
        await ValidateAccountsAsync(pair.Source, pair.Destination, request.Amount, ct);
        if (!string.Equals(request.AssetCode, pair.Source.AssetCode, StringComparison.Ordinal))
            throw new InvalidOperationException("Funding asset changed; approval is blocked.");
        await PostUnderTransactionAsync(request, pair.Source, pair.Destination, actorId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(request);
    }

    public async Task<AdminWalletFundingRequestDto> RejectAsync(
        Guid actorId, Guid requestId, string? reason, CancellationToken ct)
    {
        RequireActor(actorId);
        var cleanReason = CleanRequired(reason, "Rejection reason", 1000);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var request = await db.AdminWalletFundingRequests
            .FromSqlInterpolated($"SELECT * FROM \"AdminWalletFundingRequests\" WHERE \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Wallet funding request not found.");
        if (request.IsDeleted || request.Status != AdminWalletFundingStatus.PendingApproval)
            throw new InvalidOperationException("Only pending funding requests can be rejected.");
        if (request.RequestedByUserId == actorId)
            throw new InvalidOperationException("Funding request creators cannot reject their own requests.");
        request.Status = AdminWalletFundingStatus.Rejected;
        request.ReviewedByUserId = actorId;
        request.ReviewedAtUtc = DateTime.UtcNow;
        request.RejectionReason = cleanReason;
        request.LastUpdatedByUserId = actorId;
        request.LastUpdatedAt = DateTime.UtcNow;
        audit.Stage(new AuditRecordRequest(
            "ADMIN_WALLET_FUNDING_REJECTED", "WalletFunding", nameof(AdminWalletFundingRequest),
            request.Id.ToString(), NewValues: new { request.Status, Reason = cleanReason }, UserId: actorId));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(request);
    }

    private async Task PostUnderTransactionAsync(AdminWalletFundingRequest request,
        FinancialAccount source, FinancialAccount destination, Guid actorId, CancellationToken ct)
    {
        // Atomic write: master debit, destination credit, two postings, audit and notification.
        // The enclosing PostgreSQL SERIALIZABLE transaction commits or rolls back all of them.
        if (request.Status != AdminWalletFundingStatus.PendingApproval || request.LedgerTransactionId.HasValue)
            throw new InvalidOperationException("This funding request is not eligible for posting.");
        if (source.AvailableBalance < request.Amount || source.SettledBalance < request.Amount)
            throw new InvalidOperationException("Insufficient settled and available house-account funds; no credit was made.");

        source.AvailableBalance -= request.Amount;
        source.SettledBalance -= request.Amount;
        destination.AvailableBalance += request.Amount;
        destination.SettledBalance += request.Amount;
        var now = DateTime.UtcNow;
        foreach (var account in new[] { source, destination })
        {
            account.LastUpdatedAt = now;
            account.LastUpdatedByUserId = actorId;
        }
        var description = $"Administrative wallet funding {request.Reference}. {request.Reason}";
        if (description.Length > 1000) description = description[..1000];
        var ledger = new LedgerTransaction
        {
            Reference = "KXLED-" + Guid.NewGuid().ToString("N"),
            AssetCode = request.AssetCode,
            Type = LedgerTransactionType.FundingCredit,
            Status = LedgerTransactionStatus.Posted,
            Amount = request.Amount,
            Description = description,
            RelatedEntityType = nameof(AdminWalletFundingRequest),
            RelatedEntityId = request.Id,
            ContextEntityType = nameof(FinancialAccount),
            ContextEntityId = destination.Id,
            IdempotencyScope = "AdminHouseWalletFunding",
            IdempotencyKey = request.Id.ToString("N"),
            IdempotencyRequestHash = request.RequestHash,
            PostedAt = now,
            CreatedByUserId = actorId
        };
        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransaction = ledger, FinancialAccount = source,
            FinancialAccountId = source.Id,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Debit,
            Amount = request.Amount,
            AccountBalanceAfter = source.AvailableBalance
        });
        ledger.Postings.Add(new LedgerPosting
        {
            LedgerTransaction = ledger, FinancialAccount = destination,
            FinancialAccountId = destination.Id,
            BalanceBucket = LedgerBalanceBucket.Available,
            Side = LedgerPostingSide.Credit,
            Amount = request.Amount,
            AccountBalanceAfter = destination.AvailableBalance
        });
        db.LedgerTransactions.Add(ledger);
        request.Status = AdminWalletFundingStatus.Posted;
        request.LedgerTransactionId = ledger.Id;
        request.PostedAtUtc = now;
        request.ReviewedByUserId = actorId;
        request.ReviewedAtUtc = now;
        request.LastUpdatedByUserId = actorId;
        request.LastUpdatedAt = now;
        audit.Stage(new AuditRecordRequest(
            "ADMIN_WALLET_FUNDING_POSTED", "WalletFunding", nameof(AdminWalletFundingRequest),
            request.Id.ToString(), NewValues: new {
                request.Reference, request.Amount, request.AssetCode,
                HouseAccountId = source.Id, CustomerAccountId = destination.Id,
                MasterBalanceAfter = source.AvailableBalance,
                WalletBalanceAfter = destination.AvailableBalance,
                LedgerTransactionId = ledger.Id
            }, UserId: actorId));
        var message = $"Your {request.AssetCode} wallet received {request.Amount:N2} {request.AssetCode}. Reference: {request.Reference}.";
        if (destination.OwnerType == FinancialAccountOwnerType.User)
            await notifications.QueueUserAsync(destination.OwnerId, "Wallet funded", message,
                nameof(FinancialAccount), destination.Id, ct);
        else
            await notifications.QueueBusinessAsync(destination.OwnerId, "Business wallet funded", message,
                nameof(FinancialAccount), destination.Id, ct);
    }

    private async Task ValidateAccountsAsync(FinancialAccount source, FinancialAccount destination,
        decimal amount, CancellationToken ct)
    {
        if (source.IsDeleted || destination.IsDeleted ||
            source.Status != FinancialAccountStatus.Active ||
            destination.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Both source house account and destination wallet must be active.");
        if (source.AccountType != FinancialAccountType.House ||
            source.OwnerType is not (FinancialAccountOwnerType.Treasury or FinancialAccountOwnerType.Platform))
            throw new InvalidOperationException("Source must be a platform/treasury house account, not a provider balance.");
        if (destination.AccountType != FinancialAccountType.Customer ||
            destination.OwnerType is not (FinancialAccountOwnerType.User or FinancialAccountOwnerType.Business))
            throw new InvalidOperationException("Destination must be an existing consumer or business customer wallet.");
        if (source.Id == destination.Id || !string.Equals(source.AssetCode, destination.AssetCode, StringComparison.Ordinal))
            throw new InvalidOperationException("Source and destination must be distinct accounts denominated in the same asset.");

        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Code == source.AssetCode, ct);
        if (asset is null || !asset.IsSupported || asset.Type != AssetType.Fiat)
            throw new InvalidOperationException("Administrative funding currently supports configured fiat assets only.");
        if (amount <= 0m || decimal.Round(amount, Math.Clamp(asset.DecimalPlaces, 0, 18)) != amount)
            throw new InvalidOperationException("Funding amount must be positive and use the configured currency precision.");
        if (destination.OwnerType == FinancialAccountOwnerType.User)
        {
            var active = await db.Users.AsNoTracking().AnyAsync(x => x.Id == destination.OwnerId &&
                x.UserType == UserType.Consumer && x.Status == UserStatus.Active, ct);
            if (!active) throw new InvalidOperationException("Destination consumer account is not active.");
        }
        else
        {
            var businessExists = await db.BusinessProfiles.AsNoTracking()
                .AnyAsync(x => x.Id == destination.OwnerId && !x.IsDeleted, ct);
            if (!businessExists) throw new InvalidOperationException("Destination business does not exist.");
        }
    }

    private async Task<(FinancialAccount Source, FinancialAccount Destination)> LoadPairAsync(
        Guid fromId, Guid toId, CancellationToken ct)
    {
        var accounts = await db.FinancialAccounts
            .Where(x => x.Id == fromId || x.Id == toId).ToListAsync(ct);
        return (accounts.SingleOrDefault(x => x.Id == fromId)
                ?? throw new InvalidOperationException("House account not found."),
            accounts.SingleOrDefault(x => x.Id == toId)
                ?? throw new InvalidOperationException("Destination wallet not found."));
    }

    private async Task<(FinancialAccount Source, FinancialAccount Destination)> LoadLockedPairAsync(
        Guid fromId, Guid toId, CancellationToken ct)
    {
        var accounts = new Dictionary<Guid, FinancialAccount>();
        foreach (var id in new[] { fromId, toId }.OrderBy(x => x))
        {
            // Lock in stable GUID order to avoid deadlock on competing house-funded credits.
            var account = await db.FinancialAccounts
                .FromSqlInterpolated($"SELECT * FROM \"FinancialAccounts\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Funding source or destination wallet not found.");
            accounts.Add(id, account);
        }
        return (accounts[fromId], accounts[toId]);
    }

    private static string CleanRequired(string? text, string label, int maxLength)
    {
        var clean = text?.Trim() ?? "";
        if (clean.Length == 0 || clean.Length > maxLength)
            throw new InvalidOperationException($"{label} is required (maximum {maxLength} characters).");
        return clean;
    }

    private static void EnsureSameRequest(AdminWalletFundingRequest previous, string hash)
    {
        if (!string.Equals(previous.RequestHash, hash, StringComparison.Ordinal))
            throw new InvalidOperationException("Idempotency-Key was previously used for a different funding request.");
    }

    private static void RequireActor(Guid id)
    {
        if (id == Guid.Empty) throw new UnauthorizedAccessException("Valid administrative identity is required.");
    }

    private static AdminWalletFundingRequestDto Map(AdminWalletFundingRequest x) => new(
        x.Id, x.Reference, x.SourceHouseAccountId, x.DestinationWalletId, x.AssetCode,
        x.Amount, x.Reason, x.Status, x.RequestedByUserId, x.RequestedAtUtc,
        x.ReviewedByUserId, x.ReviewedAtUtc, x.RejectionReason, x.LedgerTransactionId, x.PostedAtUtc);
}
