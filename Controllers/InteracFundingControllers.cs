using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Infrastructure;
using KorridorX.Services.PaymentChannels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

public abstract class InteracFundingControllerBase(InteracFundingService service, bool business) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.GetAsync(Actor(), walletId, business, ct), "Interac funding instructions retrieved."));
    [HttpPost("senders")]
    public async Task<IActionResult> Add(Guid walletId, AddInteracSendingEmailRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.AddAsync(Actor(), walletId, business, request.Email, ct), "Verification email queued. Enter the code sent to that email address."));
    [HttpPost("senders/{senderId:guid}/resend")]
    public async Task<IActionResult> Resend(Guid walletId, Guid senderId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.ResendAsync(Actor(), walletId, business, senderId, ct), "A new verification code was queued. Previous codes no longer work."));
    [HttpPost("senders/{senderId:guid}/verify")]
    public async Task<IActionResult> Verify(Guid walletId, Guid senderId, VerifyInteracSendingEmailRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.VerifyAsync(Actor(), walletId, business, senderId, request.Code, ct), "Sending email verified."));
    [HttpDelete("senders/{senderId:guid}")]
    public async Task<IActionResult> Remove(Guid walletId, Guid senderId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.RemoveAsync(Actor(), walletId, business, senderId, ct), "Sending email removed. Historical ownership is retained for reconciliation."));
    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}

[ApiController, Authorize(Roles = "Consumer"), EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/wallets/{walletId:guid}/interac")]
public sealed class InteracFundingController(InteracFundingService service) : InteracFundingControllerBase(service, false);

[ApiController, Authorize, EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/business/funding/wallets/{walletId:guid}/interac")]
public sealed class BusinessInteracFundingController(InteracFundingService service) : InteracFundingControllerBase(service, true);

[ApiController, Authorize(Roles = "SuperAdmin"), EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/admin/interac")]
public sealed class AdminInteracController(InteracAdminService service) : ControllerBase
{
    [HttpGet("provider-registrations")]
    public async Task<IActionResult> ProviderRegistrations([FromQuery] string? status = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, CancellationToken ct = default) => Ok(ApiResponses.Ok(await service.ProviderRegistrationsAsync(status, search, page, ct), "Provider Interac registrations retrieved."));
    [HttpGet("routes")]
    public async Task<IActionResult> Routes(CancellationToken ct) => Ok(ApiResponses.Ok(await service.ListRoutesAsync(ct), "Interac receiving routes retrieved."));
    [HttpPost("routes")]
    public async Task<IActionResult> Register(RegisterInteracRouteRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await service.RegisterAsync(Actor(), request, ct), "Receiving route saved as an inactive draft."));
    [HttpPost("routes/{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, VerifyInteracRouteRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await service.VerifyAsync(Actor(), id, request, ct), "Provider verification completed. Review readiness before sending funds."));
    [HttpPut("routes/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, SetInteracRouteStatusRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await service.StatusAsync(Actor(), id, request, ct), "Receiving route status updated."));
    [HttpGet("deposits")]
    public async Task<IActionResult> Deposits([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(await service.DepositsAsync(page, pageSize, status, ct), "Interac deposit review queue retrieved."));
    [HttpGet("deposits/{id:guid}")]
    public async Task<IActionResult> Deposit(Guid id, CancellationToken ct) => Ok(ApiResponses.Ok(await service.DepositAsync(id, ct), "Interac deposit review retrieved."));
    [HttpPost("deposits/{id:guid}/notes")]
    public async Task<IActionResult> Note(Guid id, AddInteracDepositNoteRequest request, CancellationToken ct) => Ok(ApiResponses.Ok(await service.AddNoteAsync(Actor(), id, request.Note, ct), "Review note saved. No funds were moved."));
    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
