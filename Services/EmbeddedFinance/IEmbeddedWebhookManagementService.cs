using KorridorX.Models.Enums;
using KorridorX.Dtos.EmbeddedFinance;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedWebhookManagementService
{
    Task<IReadOnlyList<BusinessWebhookEndpointDto>> GetEndpointsAsync(Guid userId, CancellationToken ct = default);
    Task<BusinessWebhookEndpointCreatedDto> CreateEndpointAsync(Guid userId, CreateBusinessWebhookEndpointRequestDto request, CancellationToken ct = default);
    Task<RotateBusinessWebhookSecretDto> RotateSecretAsync(Guid userId, Guid endpointId, CancellationToken ct = default);
    Task SetStatusAsync(Guid userId, Guid endpointId, bool enabled, CancellationToken ct = default);
    Task<IReadOnlyList<BusinessWebhookDeliveryDto>> GetDeliveriesAsync(Guid userId, BusinessWebhookDeliveryStatus? status, int take = 100, CancellationToken ct = default);
    Task RetryDeliveryAsync(Guid userId, Guid deliveryId, CancellationToken ct = default);
}
