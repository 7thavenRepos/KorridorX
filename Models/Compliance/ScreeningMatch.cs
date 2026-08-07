using KorridorX.Models.Common;
using KorridorX.Models.Enums;

namespace KorridorX.Models.Compliance;

public class ScreeningMatch : BaseEntity
{
    public Guid ScreeningRecordId { get; set; }
    public ScreeningRecord ScreeningRecord { get; set; } = null!;

    public WatchlistType WatchlistType { get; set; }
    public string ListName { get; set; } = "";
    public string MatchedName { get; set; } = "";
    public string? ProviderMatchId { get; set; }
    public decimal MatchScore { get; set; }
    public string MatchReason { get; set; } = "";
    public string? CountryCode { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool IsFalsePositive { get; set; }
    public string? RawJson { get; set; }
}
