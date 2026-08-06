using KorridorX.Infrastructure;
using KorridorX.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[ApiController]
[Route("api/admin/operations")]
public class AdminOperationalHealthController : ControllerBase
{
    private readonly IOperationalHealthService _service;

    public AdminOperationalHealthController(IOperationalHealthService service)
    {
        _service = service;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var result = await _service.GetAsync(ct);
        return Ok(ApiResponses.Ok(result, "Operational health retrieved successfully."));
    }
}
