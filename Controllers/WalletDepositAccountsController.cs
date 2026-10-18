using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Infrastructure;
using KorridorX.Services.PaymentChannels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Consumer")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/wallet-deposit-accounts/{walletId:guid}")]
public sealed class WalletDepositAccountsController(WalletDepositAccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid walletId, [FromQuery] Guid channelId, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.GetAsync(Actor(), walletId, channelId, false, false, ct), "Deposit account retrieved."));

    [HttpPost]
    public async Task<IActionResult> Create(Guid walletId, [FromBody] CreateWalletDepositAccountRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.ProvisionAsync(Actor(), walletId, request.ChannelId, false, ct), "Deposit account setup processed. Review readiness before sending funds."));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(Guid walletId, [FromBody] CreateWalletDepositAccountRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.GetAsync(Actor(), walletId, request.ChannelId, false, true, ct), "Deposit account status checked."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
