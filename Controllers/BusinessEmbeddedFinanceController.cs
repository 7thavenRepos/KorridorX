using System.Security.Claims;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Services.EmbeddedFinance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;
[Authorize, ApiController, Route("api/business/embedded")]
public sealed class BusinessEmbeddedFinanceController : ControllerBase
{
    private readonly IEmbeddedFinanceManagementService _service;
    public BusinessEmbeddedFinanceController(IEmbeddedFinanceManagementService service)=>_service=service;
    [HttpGet("applications")] public async Task<IActionResult> Applications(CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.GetApplicationsAsync(GetUserId(),ct),"API applications retrieved successfully."));
    [HttpPost("applications")] public async Task<IActionResult> CreateApplication([FromBody] CreateApiApplicationRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateApplicationAsync(GetUserId(),request,ct),"API application created successfully."));
    [HttpGet("applications/{apiApplicationId:guid}/credentials")] public async Task<IActionResult> Credentials(Guid apiApplicationId,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.GetCredentialsAsync(GetUserId(),apiApplicationId,ct),"API credentials retrieved successfully."));
    [HttpPost("applications/{apiApplicationId:guid}/credentials")] public async Task<IActionResult> CreateCredential(Guid apiApplicationId,[FromBody] CreateApiCredentialRequestDto request,CancellationToken ct)=>Ok(ApiResponses.Ok(await _service.CreateCredentialAsync(GetUserId(),apiApplicationId,request,ct),"API credential created. Store the API key now because it cannot be retrieved again."));
    [HttpPost("applications/{apiApplicationId:guid}/credentials/{credentialId:guid}/revoke")] public async Task<IActionResult> RevokeCredential(Guid apiApplicationId,Guid credentialId,CancellationToken ct){await _service.RevokeCredentialAsync(GetUserId(),apiApplicationId,credentialId,ct);return Ok(ApiResponses.Ok(new{apiApplicationId,credentialId},"API credential revoked successfully."));}
    private Guid GetUserId(){var v=User.FindFirstValue(ClaimTypes.NameIdentifier);return Guid.TryParse(v,out var id)?id:throw new UnauthorizedAccessException("Invalid authenticated user.");}
}
