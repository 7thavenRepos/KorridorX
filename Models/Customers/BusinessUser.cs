using KorridorX.Models.Common;
using KorridorX.Models.Identity;

namespace KorridorX.Models.Customers;

public class BusinessUser : AuditableEntity
{
    public Guid BusinessProfileId { get; set; }
    public BusinessProfile BusinessProfile { get; set; } = null!;

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public BusinessUserRole Role { get; set; } = BusinessUserRole.Member;

    public bool IsActive { get; set; } = true;
}

public enum BusinessUserRole
{
    Owner = 1,
    Admin = 2,
    Finance = 3,
    Compliance = 4,
    Member = 5
}