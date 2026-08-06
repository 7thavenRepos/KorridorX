using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Compliance")]
[ApiController]
[Route("api/admin/business-kyb/applications")]
public class AdminBusinessKybController : ControllerBase
{
    private readonly IAdminBusinessKybService _service;

    public AdminBusinessKybController(IAdminBusinessKybService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetApplications(
        [FromQuery] KybStatus? status,
        [FromQuery] string? countryCode,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetApplicationsAsync(
            status, countryCode, search, page, pageSize, ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Business KYB applications retrieved successfully."));
    }

    [HttpGet("{applicationId:guid}")]
    public async Task<IActionResult> GetApplication(
        [FromRoute] Guid applicationId,
        CancellationToken ct)
    {
        var result = await _service.GetApplicationAsync(applicationId, ct);
        return Ok(ApiResponses.Ok(result, "Business KYB application retrieved successfully."));
    }
}
