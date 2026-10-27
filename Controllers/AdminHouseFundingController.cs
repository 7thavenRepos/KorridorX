using System.Security.Claims;
using KorridorX.Dtos.Treasury;
using KorridorX.Infrastructure;
using KorridorX.Services.Treasury;
using KorridorX.Services.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/treasury/house-funding")]
public sealed class AdminHouseFundingController(HouseFundingService service, IProviderWalletResolver connection) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Positions(CancellationToken ct) => Ok(ApiResponses.Ok(
        new HouseFundingOverviewDto(DateTime.UtcNow, User.IsInRole("Admin") || User.IsInRole("SuperAdmin"),
            connection.EnvironmentName, await service.PositionsAsync(ct)), "House funding position retrieved successfully."));

    [HttpPost("preview")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Preview([FromBody] PreviewHouseFundingRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.PreviewAsync(Actor(), request, ct), "Review the company-owned funding amount before confirming."));

    [HttpPost("confirm")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmHouseFundingRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await service.ConfirmAsync(Actor(), request, ct), "Company house funding recorded successfully."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
        ? id : throw new UnauthorizedAccessException("An authenticated administrator is required.");
}
