using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Compliance")]
[ApiController]
[Route("api/admin/kyc/applications")]
public class AdminKycController : ControllerBase
{
    private readonly IAdminKycService _adminKycService;

    public AdminKycController(IAdminKycService adminKycService)
    {
        _adminKycService = adminKycService;
    }

    [HttpGet]
    public async Task<IActionResult> GetApplications(
        [FromQuery] KycStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _adminKycService.GetApplicationsAsync(
            status,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "KYC applications retrieved successfully."));
    }

    [HttpGet("{applicationId:guid}")]
    public async Task<IActionResult> GetApplication(
        [FromRoute] Guid applicationId,
        CancellationToken ct)
    {
        var result = await _adminKycService.GetApplicationAsync(applicationId, ct);

        return Ok(ApiResponses.Ok(result, "KYC application retrieved successfully."));
    }
}
