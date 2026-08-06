using System.Security.Claims;
using KorridorX.Dtos.BusinessTransfers;
using KorridorX.Infrastructure;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/business/users")]
public class BusinessUsersController : ControllerBase
{
    private readonly IBusinessUserService _service;

    public BusinessUsersController(IBusinessUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var result = await _service.GetUsersAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Business users retrieved successfully."));
    }

    [HttpPost]
    public async Task<IActionResult> AddUser(
        [FromBody] AddBusinessUserRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.AddUserAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Business user added successfully."));
    }

    [HttpPut("{businessUserId:guid}/access")]
    public async Task<IActionResult> UpdateAccess(
        Guid businessUserId,
        [FromBody] UpdateBusinessUserAccessRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAccessAsync(GetUserId(), businessUserId, request, ct);
        return Ok(ApiResponses.Ok(result, "Business user access updated successfully."));
    }

    [HttpGet("approval-policy")]
    public async Task<IActionResult> GetApprovalPolicy(CancellationToken ct)
    {
        var result = await _service.GetApprovalPolicyAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Business approval policy retrieved successfully."));
    }

    [HttpPut("approval-policy")]
    public async Task<IActionResult> UpdateApprovalPolicy(
        [FromBody] UpdateBusinessApprovalPolicyRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateApprovalPolicyAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Business approval policy updated successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
