namespace KorridorX.Services.Notifications;

public static class NotificationDeliveryPolicy
{
    public static DateTime CalculateNextAttemptAt(
        DateTime attemptedAt,
        int attemptCount,
        int retryBaseMinutes)
    {
        var safeAttempt = Math.Max(1, attemptCount);
        var multiplier = Math.Pow(2, Math.Min(safeAttempt - 1, 8));
        var delayMinutes = Math.Min(retryBaseMinutes * multiplier, 24 * 60);
        return attemptedAt.AddMinutes(delayMinutes);
    }
}
