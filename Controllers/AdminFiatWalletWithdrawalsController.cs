using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.WalletWithdrawals;
using KorridorX.Services.Audit;
using KorridorX.Services.WalletWithdrawals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

public sealed record AdminFiatWalletWithdrawalActionDto(string Reason);

// Read-only queue in every environment. Manual reconciliation and cancellation
// are separately guarded by the Development-only execution service.
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/fiat-wallet-withdrawals")]
public sealed class AdminFiatWalletWithdrawalsController(
    AppDbContext db, IWalletWithdrawalExecutionService execution, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FiatWalletWithdrawalStatus? status,
        [FromQuery] Guid? ownerId, [FromQuery] string? reference,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.FiatWalletWithdrawals.AsNoTracking().Where(x => !x.IsDeleted);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (ownerId.HasValue) query = query.Where(x => x.OwnerId == ownerId.Value);
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var search = reference.Trim();
            query = query.Where(x => x.Reference.Contains(search));
        }
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(ApiResponses.OkPaged(rows.Select(Summarize).ToArray(),
            new PageMeta { Page = page, PageSize = pageSize, TotalItems = count },
            "Withdrawal operations queue retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await db.FiatWalletWithdrawals.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(ApiResponses.Fail("Withdrawal not found."));
        return Ok(ApiResponses.Ok(Summarize(row), "Withdrawal status retrieved."));
    }

    [HttpPost("{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id,
        [FromBody] AdminFiatWalletWithdrawalActionDto request, CancellationToken ct)
    {
        var reason = RequireReason(request);
        if (!await execution.RecoverOneAsync(id, ct))
            return Conflict(ApiResponses.Fail("This withdrawal is not currently recoverable by the guarded provider lookup."));
        await audit.RecordAsync(new AuditRecordRequest("FIAT_WALLET_WITHDRAWAL_ADMIN_RECONCILED",
            "WalletWithdrawal", nameof(FiatWalletWithdrawal), id.ToString(),
            Metadata: new { Reason = reason }, UserId: GetActor()), ct);
        return Ok(ApiResponses.Ok(new { withdrawalId = id },
            "Provider evidence retrieved and withdrawal settlement evaluated."));
    }

    [HttpPost("{id:guid}/cancel-unsubmitted")]
    public async Task<IActionResult> CancelUnsubmitted(Guid id,
        [FromBody] AdminFiatWalletWithdrawalActionDto request, CancellationToken ct)
    {
        var reason = RequireReason(request);
        await execution.CancelUnsubmittedAsync(id, GetActor(), reason, ct);
        return Ok(ApiResponses.Ok(new { withdrawalId = id },
            "Never-submitted withdrawal cancelled and its reservation released."));
    }

    private static object Summarize(FiatWalletWithdrawal x) => new
    {
        x.Id, x.Reference, x.OwnerType, x.OwnerId, x.FinancialAccountId,
        x.CurrencyCode, x.WithdrawalAmount, x.ProviderFee, x.KorridorXFee,
        x.TotalWalletDebit, x.ExpectedRecipientAmount, x.PaymentMethod,
        x.Status, x.PayoutId, x.SubmittedToProviderAtUtc, x.SettledAtUtc,
        x.OperationalHoldReason, x.CreatedAt, x.LastUpdatedAt,
        x.BankName, x.AccountNumberLast4, x.RecipientEmail
    };

    private static string RequireReason(AdminFiatWalletWithdrawalActionDto? request) =>
        string.IsNullOrWhiteSpace(request?.Reason) || request.Reason.Trim().Length > 500
            ? throw new InvalidOperationException("An operations reason of at most 500 characters is required.")
            : request.Reason.Trim();

    private Guid GetActor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated operations user.");
}
