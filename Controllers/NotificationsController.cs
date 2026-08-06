using System.Security.Claims;
using KorridorX.Infrastructure;
using KorridorX.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KorridorX.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationQueueService _service;

    public NotificationsController(INotificationQueueService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.GetMyNotificationsAsync(GetUserId(), page, pageSize, ct);
        return Ok(ApiResponses.OkPaged(result.Items, result.Meta, "Notifications retrieved successfully."));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        var result = await _service.MarkReadAsync(GetUserId(), notificationId, ct);
        return Ok(ApiResponses.Ok(result, "Notification marked as read."));
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Invalid authenticated user.");
    }
}
