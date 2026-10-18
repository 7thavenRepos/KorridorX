using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.PaymentChannels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[Route("api/admin/payment-channels")]
public sealed class AdminPaymentChannelsController(PaymentChannelService service, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? assetCode = null, CancellationToken ct = default)
    {
        var result = await service.ListAsync(page, pageSize, assetCode, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Payment channels retrieved."));
    }

    [HttpGet("setup")]
    public async Task<IActionResult> Setup(CancellationToken ct) => Ok(ApiResponses.Ok(new
    {
        environment = service.EnvironmentName,
        providerDevelopment = service.IsProviderDevelopment,
        capabilities = service.Capabilities,
        countries = await db.Countries.AsNoTracking().Where(x => x.IsSupported).OrderBy(x => x.Name)
            .Select(x => new { x.Code, x.Name }).ToListAsync(ct),
        assets = await db.Assets.AsNoTracking().Where(x => x.IsSupported && x.Type == AssetType.Fiat).OrderBy(x => x.Code)
            .Select(x => new { x.Code, x.Name }).ToListAsync(ct),
        wallets = await db.ProviderWalletConfigurations.AsNoTracking().Where(x => !x.IsDeleted && x.Environment == service.EnvironmentName && x.ProviderCode == "BLAAIZ" && x.NetworkCode == "")
            .OrderBy(x => x.AssetCode).ThenBy(x => x.DisplayName)
            .Select(x => new { x.Id, x.DisplayName, x.AssetCode, x.IsActive, x.CollectionEnabled, x.PayoutEnabled, x.DefaultForCollection }).ToListAsync(ct)
    }, "Payment-channel setup retrieved. Provider credentials are not returned."));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePaymentChannelRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.SaveAsync(null, Actor(), request, ct), "Payment channel created."));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePaymentChannelRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.SaveAsync(id, Actor(), request, ct), "Payment channel updated."));

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromBody] SetPaymentChannelStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.SetStatusAsync(id, Actor(), request, ct), "Payment channel status updated."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
