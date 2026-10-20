using System.Security.Claims;
using KorridorX.Dtos.Fx;
using KorridorX.Infrastructure;
using KorridorX.Services.Fx;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[Route("api/admin/fx/transfer-fees")]
public sealed class AdminTransferFeesController(IAdminTransferFeeService fees) : ControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await fees.GetOptionsAsync(ct), "Transfer fee options retrieved."));

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Ok(ApiResponses.Ok(await fees.GetAsync(ct), "Transfer fee rules retrieved."));

    [HttpPost, Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] SaveTransferFeeRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await fees.SaveAsync(Actor(), null, request, ct), "Inactive transfer fee rule saved. Activate it when ready."));

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveTransferFeeRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await fees.SaveAsync(Actor(), id, request, ct), "Inactive transfer fee rule updated."));

    [HttpPost("{id:guid}/activate"), Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Activate(Guid id, [FromBody] TransferFeeRevisionRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await fees.SetActiveAsync(Actor(), id, request.Revision, true, ct), "Transfer fee rule activated for new quotes."));

    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] TransferFeeRevisionRequestDto request, CancellationToken ct) =>
        Ok(ApiResponses.Ok(await fees.SetActiveAsync(Actor(), id, request.Revision, false, ct), "Transfer fee rule deactivated for new quotes."));

    private Guid Actor() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException("Invalid authenticated user.");
}
