using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Dtos.Notifications;
using KorridorX.Infrastructure;
using KorridorX.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
[ApiController]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController : ControllerBase
{
    private readonly INotificationOperationsService _service;

    public AdminNotificationsController(
        INotificationOperationsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? status = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? search = null,
        [FromQuery] string? relatedEntityType = null,
        [FromQuery] string? relatedEntityId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetAsync(
            status,
            channel,
            search,
            relatedEntityType,
            relatedEntityId,
            from,
            to,
            page,
            pageSize,
            ct);

        return Ok(ApiResponses.OkPaged(
            result.Items,
            result.Meta,
            "Notifications retrieved successfully."));
    }

    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid notificationId,
        CancellationToken ct) =>
        Ok(ApiResponses.Ok(
            await _service.GetByIdAsync(notificationId, ct),
            "Notification retrieved successfully."));

    [HttpPost("{notificationId:guid}/retry")]
    public async Task<IActionResult> Retry(
        Guid notificationId,
        [FromBody] RetryNotificationRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.RetryAsync(
            notificationId,
            GetUserId(),
            request.ResetAttemptCount,
            request.Reason,
            ct);

        return Ok(ApiResponses.Ok(
            result,
            "Notification queued for retry."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException(
                "Invalid authenticated user.");
    }
}