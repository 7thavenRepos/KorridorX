namespace KorridorX.Services.Payments;

public sealed record PayoutStatusTransitionContext
(
    string Source,
    string? Reason = null,
    Guid? ChangedByUserId = null,
    string? ProviderPayoutId = null,
    string? ProviderReference = null,
    string? InteracQuestion = null,
    string? InteracAnswer = null,
    string? ProviderRequestId = null,
    string? ProviderResponseId = null,
    string? RequestPayloadJson = null,
    string? ResponsePayloadJson = null,
    string? MetadataJson = null,
    DateTime? OccurredAt = null
);
