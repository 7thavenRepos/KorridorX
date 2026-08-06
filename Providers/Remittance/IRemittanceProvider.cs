using KorridorX.Models.Enums;

namespace KorridorX.Providers.Remittance;

public interface IRemittanceProvider
{
    string ProviderName { get; }
    ProviderCode ProviderCode { get; }

    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    Task<RemittanceProviderCustomerResult> SyncIndividualCustomerAsync(
        RemittanceProviderCustomerRequest request,
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
}
