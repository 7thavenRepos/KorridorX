using KorridorX.Models.Common;

namespace KorridorX.Models.Notifications;

public class MobilePushDevice : AuditableEntity
{
    public Guid UserId { get; set; }

    public string Platform { get; set; } = "";

    public string PushToken { get; set; } = "";

    public string PushTokenHash { get; set; } = "";

    public string DeviceFingerprint { get; set; } = "";

    public string DeviceName { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime? DisabledAt { get; set; }
}
