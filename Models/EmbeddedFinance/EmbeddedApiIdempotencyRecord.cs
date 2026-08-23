using KorridorX.Models.Common;

namespace KorridorX.Models.EmbeddedFinance;

public class EmbeddedApiIdempotencyRecord : AuditableEntity
{
    public Guid ApiApplicationId { get; set; }
    public ApiApplication ApiApplication { get; set; } = null!;
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public Guid ResourceId { get; set; }
}
