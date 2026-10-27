using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.AdminFunding;
using KorridorX.Infrastructure;
using KorridorX.Models.AdminFunding;
using KorridorX.Services.AdminFunding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/admin/wallet-funding")]
public sealed class AdminWalletFundingController(AdminWalletFundingService service) : ControllerBase
{
    [HttpGet("wallets")]
    public async Task<IActionResult> Wallets([FromQuery] string? search = null,
        [FromQuery] string? assetCode = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await service.SearchWalletsAsync(search, assetCode, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Customer and business wallets retrieved."));
    }

    [HttpGet("master-wallets")]
    public async Task<IActionResult> MasterWallets([FromQuery] string? assetCode = null,
        CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(await service.MasterWalletsAsync(assetCode, ct), "Backed house accounts retrieved."));

    [HttpGet("requests")]
    public async Task<IActionResult> Requests([FromQuery] AdminWalletFundingStatus? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await service.RequestsAsync(status, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Funding requests retrieved."));
    }

    [HttpGet("requests/{id:guid}")]
    public async Task<IActionResult> Request(Guid id, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.GetRequestAsync(id, ct), "Funding request retrieved."));

    /// <summary>Operations: pending request; Admin/SuperAdmin: immediate backed ledger posting.</summary>
    [HttpPost("requests")]
    public async Task<IActionResult> Create([FromBody] CreateAdminWalletFundingRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var canPostDirectly = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
        var result = await service.CreateAsync(Actor(), canPostDirectly, request, idempotencyKey, ct);
        return StatusCode(result.Status == AdminWalletFundingStatus.Posted ? 200 : 202,
            ApiResponses.Ok(result, result.Status == AdminWalletFundingStatus.Posted
                ? "House-backed wallet funding posted." : "Funding request awaits independent approval."));
    }

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost("requests/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.ApproveAsync(Actor(), id, ct), "Funding approved and posted."));

    [Authorize(Roles = "Admin,SuperAdmin")]
    [HttpPost("requests/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewAdminWalletFundingRequestDto request,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.RejectAsync(Actor(), id, request.Reason, ct), "Funding request rejected."));

    private Guid Actor()
    {
        var text = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(text, out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("Valid internal administrator identity is required.");
        return id;
    }
}
