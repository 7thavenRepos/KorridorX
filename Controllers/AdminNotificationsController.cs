using KorridorX.Dtos.Notifications;
using KorridorX.Infrastructure;
using KorridorX.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize(Roles = "Admin,SuperAdmin,Operations")]
[ApiController]
[Route("api/admin/notifications")]
public class AdminNotificationsController : ControllerBase
{
    private readonly INotificationOperationsService _service;

    public AdminNotificationsController(INotificationOperationsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetAsync(status, page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Notifications retrieved successfully."));
    }

    [HttpPost("{notificationId:guid}/retry")]
    public async Task<IActionResult> Retry(
        Guid notificationId,
        [FromBody] RetryNotificationRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.RetryAsync(notificationId, request.ResetAttemptCount, ct);
        return Ok(ApiResponses.Ok(result, "Notification queued for retry."));
    }
}
