namespace KorridorX.Dtos.AccountClosure;

// No deletion operation is exposed in this release.
public sealed record AccountClosureRequestInput(bool Confirm);

public sealed record AccountClosureOverviewDto(
    Guid SubjectId,
    string Scope,
    Guid? OpenRequestId,
    string? RequestStatus,
    DateTime? RequestedAtUtc,
    bool MayRequest,
    bool ReadyForOperationsReview,
    bool FinalizationAvailable,
    IReadOnlyList<string> Blockers);

public sealed record AccountClosureQueueRowDto(
    Guid Id,
    string Scope,
    Guid SubjectId,
    Guid RequestedByUserId,
    string Status,
    DateTime RequestedAtUtc,
    DateTime? WithdrawnAtUtc);
