using KorridorX.Models.Common;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;

namespace KorridorX.Models.Customers;

public class BusinessInvitation : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public BusinessUserRole Role { get; set; } = BusinessUserRole.Member;
    public BusinessPermission Permissions { get; set; } = BusinessPermission.None;

    // Only the SHA-256 digest is persisted. The bearer token is sent once by email.
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public int SendCount { get; set; }

    public DateTime? AcceptedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public ApplicationUser? AcceptedByUser { get; set; }

    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public ApplicationUser? RevokedByUser { get; set; }
}
