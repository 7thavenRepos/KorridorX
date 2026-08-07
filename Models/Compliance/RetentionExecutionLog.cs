using KorridorX.Models.Common;

namespace KorridorX.Models.Compliance;

public sealed class RetentionExecutionLog : BaseEntity
{
    public Guid DataRetentionPolicyId { get; set; }
    public DataRetentionPolicy DataRetentionPolicy { get; set; } = null!;
    public bool IsDryRun { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int CandidateCount { get; set; }
    public int ProcessedCount { get; set; }
    public int SkippedLegalHoldCount { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ExecutedByUserId { get; set; }
}
