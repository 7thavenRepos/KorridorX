using KorridorX.Dtos.Treasury;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Treasury;

public interface ITreasuryService
{
    Task<IReadOnlyList<ProviderWalletDto>> SyncProviderWalletsAsync(Guid userId, CancellationToken ct = default);
    Task<TreasuryDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<PagedResult<ProviderWalletDto>> GetProviderWalletsAsync(string? currencyCode, int page, int pageSize, CancellationToken ct = default);
    Task<LiquidityThresholdDto> UpsertThresholdAsync(Guid userId, UpsertLiquidityThresholdRequestDto request, CancellationToken ct = default);
    Task<PagedResult<LiquidityThresholdDto>> GetThresholdsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<SettlementBatchDto> CreateSettlementBatchAsync(Guid userId, CreateSettlementBatchRequestDto request, CancellationToken ct = default);
    Task<SettlementBatchDto> ReconcileSettlementBatchAsync(Guid userId, Guid batchId, ReconcileSettlementBatchRequestDto request, CancellationToken ct = default);
    Task<PagedResult<SettlementBatchDto>> GetSettlementBatchesAsync(int page, int pageSize, CancellationToken ct = default);
}
