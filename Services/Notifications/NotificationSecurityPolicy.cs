using KorridorX.Models.Notifications;

namespace KorridorX.Services.Notifications;

public static class NotificationSecurityPolicy
{
    public const string AccountSecurityEntityType = "AccountSecurity";
    public const string RedactedBody = "[Sensitive account security message redacted.]";

    public static string BodyForApi(NotificationMessage notification) =>
        string.Equals(
            notification.RelatedEntityType,
            AccountSecurityEntityType,
            StringComparison.Ordinal)
            ? RedactedBody
            : notification.Body;
}
