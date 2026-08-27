using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Business,BusinessAdmin")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/business-kyb")]
public class BusinessKybController : ControllerBase
{
    private readonly IBusinessKybService _businessKybService;

    public BusinessKybController(IBusinessKybService businessKybService)
    {
        _businessKybService = businessKybService;
    }

    [HttpPost("applications/start")]
    public async Task<IActionResult> StartApplication(
        [FromBody] StartBusinessKybRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.StartAsync(GetUserId(), request, ct);
        return Ok(ApiResponses.Ok(result, "Business KYB application started successfully."));
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var result = await _businessKybService.GetStatusAsync(GetUserId(), ct);
        return Ok(ApiResponses.Ok(result, "Business KYB status retrieved successfully."));
    }

    [HttpGet("applications/{applicationId:guid}")]
    public async Task<IActionResult> GetApplication(
        [FromRoute] Guid applicationId,
        CancellationToken ct)
    {
        var result = await _businessKybService.GetApplicationAsync(GetUserId(), applicationId, ct);
        return Ok(ApiResponses.Ok(result, "Business KYB application retrieved successfully."));
    }

    [HttpPut("applications/{applicationId:guid}/profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromRoute] Guid applicationId,
        [FromBody] UpdateBusinessKybProfileRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.UpdateProfileAsync(
            GetUserId(), applicationId, request, ct);

        return Ok(ApiResponses.Ok(result, "Business profile updated successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/owners")]
    public async Task<IActionResult> AddOwner(
        [FromRoute] Guid applicationId,
        [FromBody] CreateBusinessOwnerRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.AddOwnerAsync(
            GetUserId(), applicationId, request, ct);

        return Ok(ApiResponses.Ok(result, "Beneficial owner added successfully."));
    }

    [HttpPut("applications/{applicationId:guid}/owners/{ownerId:guid}")]
    public async Task<IActionResult> UpdateOwner(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid ownerId,
        [FromBody] UpdateBusinessOwnerRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.UpdateOwnerAsync(
            GetUserId(), applicationId, ownerId, request, ct);

        return Ok(ApiResponses.Ok(result, "Beneficial owner updated successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/owners/{ownerId:guid}/documents/upload-url")]
    public async Task<IActionResult> CreateOwnerDocumentUploadUrl(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid ownerId,
        [FromBody] BusinessOwnerUploadUrlRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.RequestOwnerUploadUrlAsync(
            GetUserId(), applicationId, ownerId, request, ct);

        return Ok(ApiResponses.Ok(result, "Owner document upload URL created successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/owners/{ownerId:guid}/documents/confirm")]
    public async Task<IActionResult> ConfirmOwnerDocumentUpload(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid ownerId,
        [FromBody] ConfirmBusinessOwnerUploadRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.ConfirmOwnerUploadAsync(
            GetUserId(), applicationId, ownerId, request, ct);

        return Ok(ApiResponses.Ok(result, "Owner document upload confirmed successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/documents/upload-url")]
    public async Task<IActionResult> CreateBusinessDocumentUploadUrl(
        [FromRoute] Guid applicationId,
        [FromBody] BusinessDocumentUploadUrlRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.RequestDocumentUploadUrlAsync(
            GetUserId(), applicationId, request, ct);

        return Ok(ApiResponses.Ok(result, "Business document upload URL created successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/documents/{documentId:guid}/confirm")]
    public async Task<IActionResult> ConfirmBusinessDocumentUpload(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid documentId,
        [FromBody] ConfirmBusinessDocumentUploadRequestDto request,
        CancellationToken ct)
    {
        var result = await _businessKybService.ConfirmDocumentUploadAsync(
            GetUserId(), applicationId, documentId, request, ct);

        return Ok(ApiResponses.Ok(result, "Business document upload confirmed successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/submit")]
    public async Task<IActionResult> SubmitApplication(
        [FromRoute] Guid applicationId,
        CancellationToken ct)
    {
        var result = await _businessKybService.SubmitAsync(GetUserId(), applicationId, ct);
        return Ok(ApiResponses.Ok(result, "Business KYB application submitted successfully."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}
