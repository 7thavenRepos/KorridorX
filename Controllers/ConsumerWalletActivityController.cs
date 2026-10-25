using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

// W9: read-only, wallet-ID-scoped ledger. One activity per ledger transaction,
// even when its available/held/external movements have multiple postings.
[ApiController]
[Authorize(Roles = "Consumer")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/wallets/{walletId:guid}/activity")]
public sealed class ConsumerWalletActivityController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> List(Guid walletId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor))
            throw new UnauthorizedAccessException("Invalid authenticated user.");

        // Authorize the specific wallet, not just the requested currency.
        var ownsWallet = await db.FinancialAccounts.AsNoTracking().AnyAsync(x =>
            x.Id == walletId && x.OwnerId == actor &&
            x.OwnerType == FinancialAccountOwnerType.User &&
            x.AccountType == FinancialAccountType.Customer && !x.IsDeleted, ct);
        if (!ownsWallet) return NotFound();

        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = db.LedgerTransactions.AsNoTracking().Where(x =>
            !x.IsDeleted && x.Postings.Any(p => p.FinancialAccountId == walletId));
        var total = await query.CountAsync(ct);
        var transactions = await query
            .OrderByDescending(x => x.PostedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Reference, x.Type, x.Description, x.Amount,
                x.PostedAt, x.RelatedEntityType, x.RelatedEntityId, x.Status
            }).ToListAsync(ct);

        var ids = transactions.Select(x => x.Id).ToArray();
        var postings = await db.LedgerPostings.AsNoTracking().Where(x =>
                x.FinancialAccountId == walletId && ids.Contains(x.LedgerTransactionId))
            .Select(x => new { x.LedgerTransactionId, x.BalanceBucket, x.Side, x.Amount })
            .ToListAsync(ct);

        var withdrawalIds = transactions.Where(x => x.RelatedEntityType == nameof(FiatWalletWithdrawal) &&
                x.RelatedEntityId.HasValue)
            .Select(x => x.RelatedEntityId!.Value).Distinct().ToArray();
        var withdrawals = await db.FiatWalletWithdrawals.AsNoTracking().Where(x =>
                withdrawalIds.Contains(x.Id) && x.FinancialAccountId == walletId && !x.IsDeleted)
            .Select(x => new { x.Id, x.Reference, x.Status }).ToListAsync(ct);
        var withdrawalById = withdrawals.ToDictionary(x => x.Id);

        var transferIds = transactions.Where(x => x.RelatedEntityType == "Transfer" &&
                x.RelatedEntityId.HasValue)
            .Select(x => x.RelatedEntityId!.Value).Distinct().ToArray();
        var transfers = await db.Transfers.AsNoTracking().Where(x =>
                transferIds.Contains(x.Id) && x.SourceFinancialAccountId == walletId && !x.IsDeleted)
            .Select(x => new { x.Id, x.Reference, x.Status }).ToListAsync(ct);
        var transferById = transfers.ToDictionary(x => x.Id);

        var items = new List<ConsumerWalletActivityDto>(transactions.Count);
        foreach (var row in transactions)
        {
            var (availableDelta, heldDelta) = WalletActivityDisplayPolicy.BucketDeltas(
                postings.Where(p => p.LedgerTransactionId == row.Id)
                    .Select(p => (p.BalanceBucket, p.Side, p.Amount)));
            string? operationReference = null;
            var status = row.Status.ToString();
            if (row.RelatedEntityId is Guid id && row.RelatedEntityType == nameof(FiatWalletWithdrawal) &&
                withdrawalById.TryGetValue(id, out var withdrawal))
            {
                operationReference = withdrawal.Reference;
                status = WalletActivityDisplayPolicy.WithdrawalStatus(withdrawal.Status);
            }
            else if (row.RelatedEntityId is Guid transferId && row.RelatedEntityType == "Transfer" &&
                transferById.TryGetValue(transferId, out var transfer))
            {
                operationReference = transfer.Reference;
                status = transfer.Status.ToString();
            }
            items.Add(new ConsumerWalletActivityDto(
                row.Id, row.Reference, row.Type.ToString(),
                WalletActivityDisplayPolicy.Title(row.Type, row.RelatedEntityType),
                row.Description, row.Amount, availableDelta, heldDelta, status,
                row.PostedAt, row.RelatedEntityType, row.RelatedEntityId, operationReference));
        }

        var meta = new PageMeta { Page = page, PageSize = pageSize, TotalItems = total };
        return Ok(ApiResponses.OkPaged(items, meta, "Wallet activity retrieved."));
    }
}

public sealed record ConsumerWalletActivityDto(
    Guid Id, string Reference, string Type, string Title, string Description,
    decimal Amount, decimal AvailableDelta, decimal HeldDelta, string Status,
    DateTime OccurredAtUtc, string? RelatedEntityType, Guid? RelatedEntityId,
    string? OperationReference);

// Pure formatting helpers. A hold changes Available/Held, NOT cash settled;
// show both wallet-bucket deltas instead of calling it a completed debit.
public static class WalletActivityDisplayPolicy
{
    public static (decimal AvailableDelta, decimal HeldDelta) BucketDeltas(
        IEnumerable<(LedgerBalanceBucket Bucket, LedgerPostingSide Side, decimal Amount)> postings)
    {
        decimal available = 0m, held = 0m;
        foreach (var (bucket, side, amount) in postings)
        {
            var signed = side == LedgerPostingSide.Credit ? amount : -amount;
            if (bucket == LedgerBalanceBucket.Available) available += signed;
            if (bucket == LedgerBalanceBucket.Held) held += signed;
        }
        return (available, held);
    }

    public static string WithdrawalStatus(FiatWalletWithdrawalStatus status) => status switch
    {
        FiatWalletWithdrawalStatus.AwaitingExecution => "Awaiting payout",
        FiatWalletWithdrawalStatus.Processing => "Processing",
        FiatWalletWithdrawalStatus.Succeeded => "Completed",
        FiatWalletWithdrawalStatus.FailedReleased => "Failed · funds released",
        FiatWalletWithdrawalStatus.RequiresReconciliation => "Verification needed",
        FiatWalletWithdrawalStatus.CancelledReleased => "Cancelled · funds released",
        _ => "Status unknown"
    };

    public static string Title(LedgerTransactionType type, string? linkedEntity) => type switch
    {
        LedgerTransactionType.FundingCredit => "Money added",
        LedgerTransactionType.ExternalCollectionCredit => "Deposit received",
        LedgerTransactionType.TransferReservation => "Transfer funded",
        LedgerTransactionType.Withdrawal => "Withdrawal reserved",
        LedgerTransactionType.ReservationCapture when linkedEntity == nameof(FiatWalletWithdrawal) => "Withdrawal settled",
        LedgerTransactionType.ReservationCapture => "Funds sent",
        LedgerTransactionType.ReservationRelease => "Held funds released",
        LedgerTransactionType.MarketplaceReservation => "Trade reserved",
        LedgerTransactionType.InstantReservation => "Instant trade reserved",
        LedgerTransactionType.Trade => "Trade settled",
        LedgerTransactionType.Conversion => "Currency exchanged",
        LedgerTransactionType.Fee => "Fee",
        LedgerTransactionType.Refund => "Refund",
        LedgerTransactionType.Reversal => "Reversal",
        LedgerTransactionType.ManualAdjustment => "Balance adjustment",
        LedgerTransactionType.Deposit => "Deposit",
        LedgerTransactionType.Settlement => "Settlement",
        _ => type.ToString()
    };
}
