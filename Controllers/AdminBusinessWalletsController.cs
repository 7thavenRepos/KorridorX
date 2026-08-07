using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.BusinessFunding;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessFunding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/business-wallets")]
public class AdminBusinessWalletsController : ControllerBase
{
    private readonly IBusinessFundingService _service;

    public AdminBusinessWalletsController(IBusinessFundingService service)
    {
        _service = service;
    }

    [HttpPost("credit")]
    public async Task<IActionResult> Credit(
        [FromBody] AdminCreditBusinessWalletRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await _service.CreditWalletAsync(GetUserId(), request, idempotencyKey ?? "", ct);
        return Ok(ApiResponses.Ok(result, "Business wallet credited successfully."));
    }

    [HttpPost("{walletId:guid}/freeze")]
    public async Task<IActionResult> Freeze(
        Guid walletId,
        [FromBody] AdminSetBusinessWalletStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.SetWalletStatusAsync(
            GetUserId(),
            walletId,
            BusinessWalletStatus.Frozen,
            request.Reason,
            ct);
        return Ok(ApiResponses.Ok(result, "Business wallet frozen successfully."));
    }

    [HttpPost("{walletId:guid}/unfreeze")]
    public async Task<IActionResult> Unfreeze(
        Guid walletId,
        [FromBody] AdminSetBusinessWalletStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.SetWalletStatusAsync(
            GetUserId(),
            walletId,
            BusinessWalletStatus.Active,
            request.Reason,
            ct);
        return Ok(ApiResponses.Ok(result, "Business wallet reactivated successfully."));
    }

    [HttpPost("{walletId:guid}/adjust")]
    public async Task<IActionResult> Adjust(
        Guid walletId,
        [FromBody] AdminAdjustBusinessWalletRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await _service.AdjustWalletAsync(
            GetUserId(),
            walletId,
            request,
            idempotencyKey ?? "",
            ct);
        return Ok(ApiResponses.Ok(result, "Business wallet adjusted successfully."));
    }

    [HttpPost("ledger/{transactionId:guid}/reverse")]
    public async Task<IActionResult> ReverseLedgerTransaction(
        Guid transactionId,
        [FromBody] AdminReverseBusinessLedgerTransactionRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.ReverseLedgerTransactionAsync(
            GetUserId(),
            transactionId,
            request.Reason,
            ct);
        return Ok(ApiResponses.Ok(result, "Business ledger transaction reversed successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
