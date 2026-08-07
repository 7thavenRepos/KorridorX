using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Services.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/kyc")]
public class KycController : ControllerBase
{
    private readonly IKycService _kycService;

    public KycController(IKycService kycService)
    {
        _kycService = kycService;
    }

    [HttpPost("applications/start")]
    public async Task<IActionResult> StartApplication(CancellationToken ct)
    {
        var result = await _kycService.StartMyApplicationAsync(GetUserId(), ct);

        return Ok(ApiResponses.Ok(result, "KYC application started successfully."));
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var result = await _kycService.GetMyStatusAsync(GetUserId(), ct);

        return Ok(ApiResponses.Ok(result, "KYC status retrieved successfully."));
    }

    [HttpPut("applications/{applicationId:guid}/identity")]
    public async Task<IActionResult> SaveIdentity(
        [FromRoute] Guid applicationId,
        [FromBody] SaveKycIdentityRequestDto request,
        CancellationToken ct)
    {
        var result = await _kycService.SaveIdentityAsync(
            GetUserId(),
            applicationId,
            request,
            ct);

        return Ok(ApiResponses.Ok(result, "KYC identity information saved successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/documents/upload-url")]
    public async Task<IActionResult> CreateDocumentUploadUrl(
        [FromRoute] Guid applicationId,
        [FromBody] CreateKycDocumentUploadUrlRequestDto request,
        CancellationToken ct)
    {
        var result = await _kycService.CreateDocumentUploadUrlAsync(
            GetUserId(),
            applicationId,
            request,
            ct);

        return Ok(ApiResponses.Ok(result, "KYC document upload URL created successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/documents/{documentId:guid}/confirm")]
    public async Task<IActionResult> ConfirmDocumentUpload(
        [FromRoute] Guid applicationId,
        [FromRoute] Guid documentId,
        [FromBody] ConfirmKycDocumentUploadRequestDto request,
        CancellationToken ct)
    {
        var result = await _kycService.ConfirmDocumentUploadAsync(
            GetUserId(),
            applicationId,
            documentId,
            request,
            ct);

        return Ok(ApiResponses.Ok(result, "KYC document upload confirmed successfully."));
    }

    [HttpPost("applications/{applicationId:guid}/submit")]
    public async Task<IActionResult> SubmitApplication(
        [FromRoute] Guid applicationId,
        CancellationToken ct)
    {
        var result = await _kycService.SubmitApplicationAsync(
            GetUserId(),
            applicationId,
            ct);

        return Ok(ApiResponses.Ok(result, "KYC application submitted for review successfully."));
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
