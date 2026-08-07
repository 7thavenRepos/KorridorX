using KorridorX.Models.Common;

namespace KorridorX.Models.Identity;

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }

    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? RevokedReason { get; set; }

    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiresAt;
}