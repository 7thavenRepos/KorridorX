using KorridorX.Models.Enums;

namespace KorridorX.Providers.Remittance;

public interface IRemittanceProvider
{
    string ProviderName { get; }
    ProviderCode ProviderCode { get; }

    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    Task<IReadOnlyList<RemittanceBank>> GetBanksAsync(
        string? countryCode = null,
        string? currencyCode = null,
        CancellationToken ct = default);

    Task<RemittanceBankAccountResolutionResult> ResolveBankAccountAsync(
        string providerBankId,
        string accountNumber,
        CancellationToken ct = default);

    Task<RemittanceWebhookReplayResult> ReplayWebhookAsync(
        string providerTransactionId,
        CancellationToken ct = default);

    Task<RemittanceRefundResult> InitiateRefundAsync(
        string providerCollectionTransactionId,
        string reference,
        string? reason = null,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default);

    Task<RemittanceRefundResult> GetRefundAsync(
        string providerRefundId,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default);

    Task<RemittanceProviderCustomerResult> SyncIndividualCustomerAsync(
        RemittanceProviderCustomerRequest request,
        CancellationToken ct = default);

    Task<RemittanceBusinessCustomerResult> SyncBusinessCustomerAsync(
        RemittanceBusinessCustomerRequest request,
        CancellationToken ct = default);

    Task<RemittanceBusinessUploadUrlResult> RequestBusinessOwnerUploadUrlAsync(
        RemittanceBusinessOwnerUploadUrlRequest request,
        CancellationToken ct = default);

    Task<RemittanceBusinessOwnerFilesResult> SubmitBusinessOwnerFilesAsync(
        RemittanceBusinessOwnerFilesRequest request,
        CancellationToken ct = default);

    Task<RemittanceBusinessUploadUrlResult> RequestBusinessDocumentUploadUrlAsync(
        RemittanceBusinessDocumentUploadUrlRequest request,
        CancellationToken ct = default);

    Task<RemittanceBusinessDocumentResult> RegisterBusinessDocumentAsync(
        RemittanceBusinessDocumentRegistrationRequest request,
        CancellationToken ct = default);

    Task<RemittanceBusinessKybSubmissionResult> SubmitBusinessKybAsync(
        Guid businessProfileId,
        string providerCustomerId,
        CancellationToken ct = default);

    Task<RemittanceBusinessCustomerResult> GetBusinessCustomerAsync(
        Guid businessProfileId,
        string providerCustomerId,
        CancellationToken ct = default);

    Task<RemittanceCollectionResult> InitiateCollectionAsync(
        RemittanceCollectionRequest request,
        CancellationToken ct = default);

    Task<RemittanceKycUploadUrlResult> RequestIndividualKycUploadUrlAsync(
        RemittanceKycUploadUrlRequest request,
        CancellationToken ct = default);

    Task<RemittanceKycDocumentSubmissionResult> SubmitIndividualKycDocumentsAsync(
        RemittanceKycDocumentSubmissionRequest request,
        CancellationToken ct = default);

    Task<RemittancePayoutResult> InitiatePayoutAsync(
        RemittancePayoutRequest request,
        CancellationToken ct = default);

    Task<RemittanceTransactionStatusResult> GetTransactionAsync(
        string providerTransactionIdOrReference,
        Guid? transferId = null,
        Guid? collectionId = null,
        Guid? payoutId = null,
        CancellationToken ct = default);
}
