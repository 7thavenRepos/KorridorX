using KorridorX.Models.Notifications;

namespace KorridorX.Services.Notifications;

public interface INotificationDeliveryProvider
{
    Task<NotificationDeliveryResult> SendAsync(
        NotificationMessage notification,
        CancellationToken ct = default);
}

public sealed record NotificationDeliveryResult(
    bool Success,
    string? ProviderMessageId = null,
    string? ErrorMessage = null);
