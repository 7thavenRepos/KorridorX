using KorridorX.Dtos.Providers;
using KorridorX.Infrastructure;
using KorridorX.Providers.Remittance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
[ApiController]
[Route("api/providers")]
public class ProvidersController : ControllerBase
{
    private readonly IRemittanceProvider _provider;

    public ProvidersController(IRemittanceProvider provider)
    {
        _provider = provider;
    }

    [HttpGet("blaaiz/health")]
    public async Task<IActionResult> GetBlaaizHealth(CancellationToken ct)
    {
        var available = await _provider.IsAvailableAsync(ct);

        return Ok(ApiResponses.Ok(
            new ProviderHealthDto(
                _provider.ProviderName,
                available,
                DateTime.UtcNow),
            available
                ? "Blaaiz authentication is available."
                : "Blaaiz authentication is unavailable or disabled."));
    }
}
