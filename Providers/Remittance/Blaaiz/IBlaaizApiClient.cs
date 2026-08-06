using KorridorX.Providers.Remittance.Blaaiz.Models;

namespace KorridorX.Providers.Remittance.Blaaiz;

public interface IBlaaizApiClient
{

    Task<BlaaizApiResult<List<BlaaizBankData>>> ListBanksAsync(
        string? countryCode = null,
        string? currencyCode = null,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizResolveBankAccountResponse>> ResolveBankAccountAsync(
        BlaaizResolveBankAccountRequest request,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizWebhookReplayResponse>> ReplayWebhookAsync(
        BlaaizWebhookReplayRequest request,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizRefundEnvelope>> InitiateRefundAsync(
        BlaaizRefundRequest request,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizRefundEnvelope>> GetRefundAsync(
        string providerRefundId,
        Guid? transferId = null,
        Guid? collectionId = null,
        CancellationToken ct = default);
    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> CreateCustomerAsync(
        BlaaizCreateCustomerRequest request,
        Guid customerProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> GetCustomerAsync(
        string providerCustomerId,
        Guid customerProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizKycUploadUrlResponse>> RequestKycUploadUrlAsync(
        BlaaizKycUploadUrlRequest request,
        Guid customerProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizMessageResponse>> AttachCustomerFilesAsync(
        string providerCustomerId,
        BlaaizAttachCustomerFilesRequest request,
        Guid customerProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> CreateBusinessCustomerAsync(
        BlaaizCreateCustomerRequest request,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> UpdateBusinessCustomerAsync(
        string providerCustomerId,
        BlaaizCreateCustomerRequest request,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> GetBusinessCustomerAsync(
        string providerCustomerId,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizUploadUrlEnvelope>> RequestBusinessOwnerUploadUrlAsync(
        string providerCustomerId,
        string providerOwnerId,
        BlaaizOwnerUploadUrlRequest request,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizBusinessOwnerEnvelope>> AttachBusinessOwnerFilesAsync(
        string providerCustomerId,
        string providerOwnerId,
        BlaaizOwnerFilesRequest request,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizUploadUrlEnvelope>> RequestBusinessDocumentUploadUrlAsync(
        string providerCustomerId,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizBusinessDocumentEnvelope>> RegisterBusinessDocumentAsync(
        string providerCustomerId,
        BlaaizBusinessDocumentRequest request,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> SubmitBusinessCustomerAsync(
        string providerCustomerId,
        Guid businessProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizCardCollectionResponse>> InitiateCardCollectionAsync(
        BlaaizCardCollectionRequest request,
        Guid transferId,
        Guid collectionId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizInteracMoneyResponse>> InitiateInteracMoneyRequestAsync(
        BlaaizInteracMoneyRequest request,
        Guid transferId,
        Guid collectionId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizPayoutResponse>> InitiatePayoutAsync(
        BlaaizPayoutRequest request,
        Guid transferId,
        Guid payoutId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizTransactionEnvelope>> GetTransactionAsync(
        string providerTransactionIdOrReference,
        Guid? transferId = null,
        Guid? collectionId = null,
        Guid? payoutId = null,
        CancellationToken ct = default);
}
