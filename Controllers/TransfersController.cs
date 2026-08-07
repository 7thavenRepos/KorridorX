using KorridorX.Configuration;
using System.Security.Claims;
using KorridorX.Dtos.Transfers;
using KorridorX.Infrastructure;
using KorridorX.Services.Transfers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransfersController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateTransferRequestDto request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _transferService.CreateTransferAsync(userId, request, ct);

        return Ok(ApiResponses.Ok(
            result,
            "Transfer created successfully."));
    }

    [HttpGet("{transferId:guid}")]
    public async Task<IActionResult> GetTransferById(
        [FromRoute] Guid transferId,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _transferService.GetTransferByIdAsync(userId, transferId, ct);

        return Ok(ApiResponses.Ok(
            result,
            "Transfer retrieved successfully."));
    }

    [HttpGet]
    public async Task<IActionResult> GetMyTransfers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();

        var result = await _transferService.GetMyTransfersAsync(
            userId,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Transfers retrieved successfully."));
    }

    [HttpPost("{transferId:guid}/cancel")]
    public async Task<IActionResult> CancelTransfer(
        [FromRoute] Guid transferId,
        [FromBody] CancelTransferRequestDto request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await _transferService.CancelTransferAsync(
            userId,
            transferId,
            request,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Transfer cancelled successfully."));
    }

    private Guid GetUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userIdValue) || !Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid authenticated user.");
        }

        return userId;
    }
}