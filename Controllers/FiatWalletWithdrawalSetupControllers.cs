using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.WalletWithdrawals;
using KorridorX.Infrastructure;
using KorridorX.Services.WalletWithdrawals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

// Dedicated routes avoid changing the existing collection/deposit controllers.
// No API action in this slice can initiate a payout or debit a wallet.
[Authorize(Roles = "Consumer")]
[ApiController]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/wallets/{walletId:guid}")]
public sealed class ConsumerFiatWithdrawalSetupController(IWalletWithdrawalSetupService setup) : ControllerBase
{
    [HttpGet("withdrawal-methods")]
    public async Task<IActionResult> Methods(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.GetMethodsAsync(Actor(), walletId, false, ct), "Available same-currency withdrawal methods retrieved."));

    [HttpGet("withdrawal-destinations")]
    public async Task<IActionResult> Destinations(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.GetDestinationsAsync(Actor(), walletId, false, ct), "Withdrawal destinations retrieved."));

    [HttpPost("withdrawal-destinations")]
    public async Task<IActionResult> Add(Guid walletId, SaveWalletWithdrawalDestinationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.SaveDestinationAsync(Actor(), walletId, false, request, ct), "Withdrawal destination verified and saved."));

    [HttpPost("withdrawal-destinations/{destinationId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid walletId, Guid destinationId,
        DisableWalletWithdrawalDestinationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.DisableDestinationAsync(Actor(), walletId, destinationId, false, request, ct), "Withdrawal destination disabled."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}

[Authorize(Roles = "Business")]
[ApiController]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/business/funding/wallets/{walletId:guid}")]
public sealed class BusinessFiatWithdrawalSetupController(IWalletWithdrawalSetupService setup) : ControllerBase
{
    [HttpGet("withdrawal-methods")]
    public async Task<IActionResult> Methods(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.GetMethodsAsync(Actor(), walletId, true, ct), "Available same-currency withdrawal methods retrieved."));

    [HttpGet("withdrawal-destinations")]
    public async Task<IActionResult> Destinations(Guid walletId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.GetDestinationsAsync(Actor(), walletId, true, ct), "Withdrawal destinations retrieved."));

    [HttpPost("withdrawal-destinations")]
    public async Task<IActionResult> Add(Guid walletId, SaveWalletWithdrawalDestinationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.SaveDestinationAsync(Actor(), walletId, true, request, ct), "Withdrawal destination verified and saved."));

    [HttpPost("withdrawal-destinations/{destinationId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid walletId, Guid destinationId,
        DisableWalletWithdrawalDestinationRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await setup.DisableDestinationAsync(Actor(), walletId, destinationId, true, request, ct), "Withdrawal destination disabled."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
