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
    private readonly IBusinessInvitationService _invitations;

    public BusinessUsersController(
        IBusinessUserService service,
        IBusinessInvitationService invitations)
    {
        _service = service;
        _invitations = invitations;
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(CancellationToken ct)
    {
        var result = await _invitations.GetInvitationsAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Business invitations retrieved successfully."));
    }

    [HttpPost("invitations")]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateBusinessInvitationRequestDto request,
        CancellationToken ct)
    {
        var result = await _invitations.CreateAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Business invitation sent successfully."));
    }

    [HttpPost("invitations/{invitationId:guid}/resend")]
    public async Task<IActionResult> ResendInvitation(Guid invitationId, CancellationToken ct)
    {
        var result = await _invitations.ResendAsync(GetUserId(), invitationId, ct);
        return Ok(ApiResponses.Ok(result, "Business invitation resent successfully."));
    }

    [HttpDelete("invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid invitationId, CancellationToken ct)
    {
        await _invitations.RevokeAsync(GetUserId(), invitationId, ct);
        return Ok(ApiResponses.Ok(new { revoked = true }, "Business invitation revoked successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var result = await _service.GetUsersAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Business users retrieved successfully."));
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
