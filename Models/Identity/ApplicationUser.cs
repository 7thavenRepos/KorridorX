using KorridorX.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace KorridorX.Models.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    public string? CountryCode { get; set; }

    public UserType UserType { get; set; } = UserType.Consumer;
    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();
}