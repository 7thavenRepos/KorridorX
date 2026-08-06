namespace KorridorX.Services.Transfers;

public sealed record TransferStatusTransitionContext
(
    string Source,
    string? Reason = null,
    Guid? ChangedByUserId = null,
    string? EventType = null,
    string? Title = null,
    string? Description = null,
    string? MetadataJson = null,
    DateTime? OccurredAt = null
);
