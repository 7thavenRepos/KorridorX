using KorridorX.Dtos.EmbeddedFinance;

namespace KorridorX.Services.EmbeddedFinance;

public interface ICollectionAccountProvisioningService
{
    Task<ProviderAccountMappingDto> ProvisionAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        ProvisionCollectionAccountRequestDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<ProviderAccountMappingDto>> GetMappingsAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CancellationToken ct = default);
}
