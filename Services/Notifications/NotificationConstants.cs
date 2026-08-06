namespace KorridorX.Services.Notifications;

public static class NotificationChannels
{
    public const string Email = "Email";
}

public static class NotificationStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Retry = "Retry";
    public const string Sent = "Sent";
    public const string DeadLetter = "DeadLetter";
}
