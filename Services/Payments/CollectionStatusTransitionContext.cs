namespace KorridorX.Services.Payments;

public sealed record CollectionStatusTransitionContext
(
    string Source,
    string? Reason = null,
    Guid? ChangedByUserId = null,
    string? ProviderCollectionId = null,
    string? ProviderReference = null,
    string? CheckoutUrl = null,
    DateTime? ProviderExpiresAt = null,
    string? VirtualAccountNumber = null,
    string? VirtualAccountBankName = null,
    string? VirtualAccountName = null,
    string? ProviderRequestId = null,
    string? ProviderResponseId = null,
    string? RequestPayloadJson = null,
    string? ResponsePayloadJson = null,
    string? MetadataJson = null,
    DateTime? OccurredAt = null
);
