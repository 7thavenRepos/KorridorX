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
[Route("api/wallets/{walletId:guid}/withdrawal-fee-preview")]
public sealed class ConsumerFiatWithdrawalFeePreviewController(IWalletWithdrawalFeePreviewService preview) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Preview(Guid walletId, WalletWithdrawalFeePreviewRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await preview.PreviewAsync(Actor(), walletId, false, request, ct),
            "Indicative same-currency withdrawal fee preview. No funds have been moved or reserved."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}

[Authorize(Roles = "Business")]
[ApiController]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/business/funding/wallets/{walletId:guid}/withdrawal-fee-preview")]
public sealed class BusinessFiatWithdrawalFeePreviewController(IWalletWithdrawalFeePreviewService preview) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Preview(Guid walletId, WalletWithdrawalFeePreviewRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await preview.PreviewAsync(Actor(), walletId, true, request, ct),
            "Indicative same-currency withdrawal fee preview. No funds have been moved or reserved."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
