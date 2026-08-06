using System.ComponentModel.DataAnnotations;

namespace KorridorX.Configuration;

public class NotificationDeliveryOptions
{
    public const string SectionName = "NotificationDelivery";

    public bool WorkerEnabled { get; set; } = false;
    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int RetryBaseMinutes { get; set; } = 2;
    public int ProcessingTimeoutMinutes { get; set; } = 10;

    public SmtpDeliveryOptions Smtp { get; set; } = new();
}

public class SmtpDeliveryOptions
{
    public bool IsEnabled { get; set; } = false;

    [MaxLength(255)]
    public string Host { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    [MaxLength(255)]
    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    [MaxLength(255)]
    public string FromAddress { get; set; } = "";

    [MaxLength(255)]
    public string FromName { get; set; } = "KorridorX";
}
