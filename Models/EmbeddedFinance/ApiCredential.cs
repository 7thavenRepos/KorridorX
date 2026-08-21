using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.EmbeddedFinance;

public class ApiCredential : AuditableEntity
{
    public Guid ApiApplicationId { get; set; }
    public ApiApplication ApiApplication { get; set; } = null!;
    public string Name { get; set; } = "";
    public string KeyId { get; set; } = "";
    public string SecretHash { get; set; } = "";
    public string SecretLastFour { get; set; } = "";
    public ApiCredentialStatus Status { get; set; } = ApiCredentialStatus.Active;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIpAddress { get; set; }
}
