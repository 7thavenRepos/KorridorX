using System.Security.Claims;
using KorridorX.Dtos.Providers;
using KorridorX.Infrastructure;
using KorridorX.Services.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[ApiController]
[Route("api/admin/providers/webhooks/{webhookEventId:guid}/bank-deposit-recovery")]
public sealed class AdminBankDepositRecoveryController(RetainedBankDepositRecoveryService recovery) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(Guid webhookEventId, [FromBody] RetainedBankDepositPreviewRequestDto request,
        CancellationToken ct) => Ok(ApiResponses.Ok(
            await recovery.PreviewAsync(webhookEventId, Actor(), request.Reason, ct),
            "Retained bank deposit verified for review. No wallet credit was applied."));

    [HttpPost("apply")]
    public async Task<IActionResult> Apply(Guid webhookEventId, [FromBody] RetainedBankDepositApplyRequestDto request,
        CancellationToken ct) => Ok(ApiResponses.Ok(
            await recovery.ApplyAsync(webhookEventId, Actor(), request.PreviewToken, ct),
            "Retained bank deposit recovery completed."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) && actor != Guid.Empty
        ? actor : throw new UnauthorizedAccessException("An authenticated operator is required.");
}
