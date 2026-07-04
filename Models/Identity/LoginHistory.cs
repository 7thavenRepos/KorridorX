using KorridorX.Models.Common;

namespace KorridorX.Models.Identity;

public class LoginHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }

    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }

    public bool WasSuccessful { get; set; }
    public string? FailureReason { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}