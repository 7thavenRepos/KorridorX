using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Treasury;
using KorridorX.Infrastructure;
using KorridorX.Services.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace KorridorX.Controllers;

[ApiController]
[Route("api/admin/provider-wallet-configurations")]
[Authorize(Roles = "SuperAdmin")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class AdminProviderWalletConfigurationsController(
    ProviderWalletAdminService service, IProviderWalletResolver resolver, IOptions<BlaaizOptions> options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponses.Ok(new
    {
        environment = resolver.EnvironmentName, providerEnabled = options.Value.IsEnabled,
        wallets = await service.ListAsync(ct)
    }, "Provider wallet configurations retrieved."));

    [HttpGet("discover")]
    public async Task<IActionResult> Discover([FromQuery] bool crypto, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.DiscoverAsync(crypto, ct), "Provider wallets discovered."));

    [HttpPost]
    public async Task<IActionResult> Register(RegisterProviderWalletRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.RegisterAsync(Actor(), request, ct), "Wallet registered as an inactive draft."));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Configure(Guid id, ConfigureProviderWalletRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.ConfigureAsync(id, Actor(), request, ct), "Wallet configuration saved."));

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, VerifyProviderWalletRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.VerifyAsync(id, Actor(), request, ct), "Wallet verification completed. Review its verification status before activation."));

    [HttpPost("import-legacy")]
    public async Task<IActionResult> ImportLegacy(ImportProviderWalletsRequest request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.ImportLegacyAsync(Actor(), request.Reason, ct), "Existing environment mappings imported as inactive drafts."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
