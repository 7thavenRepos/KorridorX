using KorridorX.Infrastructure;
using KorridorX.Services.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[ApiController]
[Route("api/admin/providers")]
public class AdminProviderOperationsController : ControllerBase
{
    private readonly IProviderReconciliationService _reconciliationService;

    public AdminProviderOperationsController(
        IProviderReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpPost("blaaiz/reconcile")]
    public async Task<IActionResult> ReconcileBlaaiz(
        [FromQuery] int batchSize = 50,
        CancellationToken ct = default)
    {
        var result = await _reconciliationService.ReconcilePendingAsync(batchSize, ct);
        return Ok(ApiResponses.Ok(result, "Blaaiz reconciliation completed."));
    }
}
