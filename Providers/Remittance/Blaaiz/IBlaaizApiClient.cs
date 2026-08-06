using KorridorX.Providers.Remittance.Blaaiz.Models;

namespace KorridorX.Providers.Remittance.Blaaiz;

public interface IBlaaizApiClient
{
    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> CreateCustomerAsync(
        BlaaizCreateCustomerRequest request,
        Guid customerProfileId,
        CancellationToken ct = default);

    Task<BlaaizApiResult<BlaaizCustomerEnvelope>> GetCustomerAsync(
        string providerCustomerId,
        Guid customerProfileId,
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
}
