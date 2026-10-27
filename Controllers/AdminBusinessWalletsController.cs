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

    [HttpGet]
    public async Task<IActionResult> GetWallets(
        [FromQuery] Guid? businessProfileId,
        [FromQuery] string? search,
        [FromQuery] string? currencyCode,
        [FromQuery] FinancialAccountStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _service.GetAdminWalletsAsync(
            businessProfileId,
            search,
            currencyCode,
            status,
            page,
            pageSize,
            ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business wallets retrieved successfully."));
    }

    [HttpGet("businesses")]
    public async Task<IActionResult> SearchBusinesses(
        [FromQuery] string? search,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(
            await _service.SearchAdminBusinessesAsync(search, take, ct),
            "Business wallet subjects retrieved successfully."));

    [HttpGet("{walletId:guid}/ledger")]
    public async Task<IActionResult> GetLedger(
        Guid walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _service.GetAdminLedgerAsync(walletId, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Business wallet ledger retrieved successfully."));
    }

    [HttpPost("credit")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public IActionResult Credit() => StatusCode(410, new {
        success = false,
        message = "Legacy unbacked wallet credits are retired. Use /api/admin/wallet-funding/requests with a funded house account."
    });

    [HttpPost("{walletId:guid}/freeze")]
    public async Task<IActionResult> Freeze(
        Guid walletId,
        [FromBody] AdminSetBusinessWalletStatusRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.SetWalletStatusAsync(
            GetUserId(),
            walletId,
            FinancialAccountStatus.Frozen,
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
            FinancialAccountStatus.Active,
            request.Reason,
            ct);
        return Ok(ApiResponses.Ok(result, "Business wallet reactivated successfully."));
    }

    [HttpPost("{walletId:guid}/adjust")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public IActionResult Adjust(Guid walletId) => StatusCode(410, new {
        success = false,
        message = "Legacy unbacked manual wallet adjustments are retired. Use a separately reviewed ledger recovery workflow."
    });

    [HttpPost("ledger/{transactionId:guid}/reverse")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public IActionResult ReverseLedgerTransaction(Guid transactionId) => StatusCode(410, new {
        success = false,
        message = "Legacy external-bucket reversals are retired. Contact finance for a reviewed reversal workflow."
    });

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
