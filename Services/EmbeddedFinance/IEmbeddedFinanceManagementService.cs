using KorridorX.Dtos.EmbeddedFinance;
namespace KorridorX.Services.EmbeddedFinance;
public interface IEmbeddedFinanceManagementService
{
    Task<IReadOnlyList<ApiApplicationDto>> GetApplicationsAsync(Guid userId, CancellationToken ct = default);
    Task<ApiApplicationDto> CreateApplicationAsync(Guid userId, CreateApiApplicationRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<ApiCredentialDto>> GetCredentialsAsync(Guid userId, Guid apiApplicationId, CancellationToken ct = default);
    Task<ApiCredentialCreatedDto> CreateCredentialAsync(Guid userId, Guid apiApplicationId, CreateApiCredentialRequestDto request, CancellationToken ct = default);
    Task RevokeCredentialAsync(Guid userId, Guid apiApplicationId, Guid credentialId, CancellationToken ct = default);
}
