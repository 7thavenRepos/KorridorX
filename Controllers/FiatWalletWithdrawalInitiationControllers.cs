using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Infrastructure;
using KorridorX.Services.WalletWithdrawals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Consumer")]
[ApiController]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/wallets/{walletId:guid}/withdrawals")]
public sealed class ConsumerFiatWithdrawalInitiationController(IWalletWithdrawalInitiationService withdrawals) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Initiate(Guid walletId, InitiateWalletWithdrawalRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await withdrawals.InitiateAsync(Actor(), walletId, false, request, idempotencyKey, ct),
            "Withdrawal funds reserved for future processing. No payout was dispatched."));

    [HttpGet]
    public async Task<IActionResult> List(Guid walletId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(await withdrawals.ListAsync(Actor(), walletId, false, page, pageSize, ct),
            "Wallet withdrawal history retrieved."));

    [HttpGet("{withdrawalId:guid}")]
    public async Task<IActionResult> Details(Guid walletId, Guid withdrawalId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await withdrawals.GetAsync(Actor(), walletId, withdrawalId, false, ct),
            "Wallet withdrawal retrieved."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}

[Authorize(Roles = "Business")]
[ApiController]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/business/funding/wallets/{walletId:guid}/withdrawals")]
public sealed class BusinessFiatWithdrawalInitiationController(IWalletWithdrawalInitiationService withdrawals) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Initiate(Guid walletId, InitiateWalletWithdrawalRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await withdrawals.InitiateAsync(Actor(), walletId, true, request, idempotencyKey, ct),
            "Withdrawal funds reserved for future processing. No payout was dispatched."));

    [HttpGet]
    public async Task<IActionResult> List(Guid walletId, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(ApiResponses.Ok(await withdrawals.ListAsync(Actor(), walletId, true, page, pageSize, ct),
            "Business wallet withdrawal history retrieved."));

    [HttpGet("{withdrawalId:guid}")]
    public async Task<IActionResult> Details(Guid walletId, Guid withdrawalId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await withdrawals.GetAsync(Actor(), walletId, withdrawalId, true, ct),
            "Business wallet withdrawal retrieved."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
