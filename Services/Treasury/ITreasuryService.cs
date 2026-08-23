using KorridorX.Dtos.Treasury;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Treasury;

public interface ITreasuryService
{
    Task<IReadOnlyList<ProviderWalletDto>> SyncProviderWalletsAsync(Guid userId, CancellationToken ct = default);
    Task<TreasuryDashboardDto> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TreasuryLiquidityPositionDto>> GetLiquidityPositionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TreasuryAssetExposureDto>> GetAssetExposureAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TreasuryLiquidityAlertDto>> GetLiquidityAlertsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TreasuryUnifiedRebalanceSuggestionDto>> GetUnifiedRebalanceSuggestionsAsync(CancellationToken ct = default);
    Task<InternalLiquidityTransferResultDto> ExecuteInternalLiquidityTransferAsync(
        Guid userId,
        ExecuteInternalLiquidityTransferRequestDto request,
        CancellationToken ct = default);
    Task<PagedResult<ProviderWalletDto>> GetProviderWalletsAsync(string? currencyCode, int page, int pageSize, CancellationToken ct = default);
    Task<LiquidityThresholdDto> UpsertThresholdAsync(Guid userId, UpsertLiquidityThresholdRequestDto request, CancellationToken ct = default);
    Task<PagedResult<LiquidityThresholdDto>> GetThresholdsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<SettlementBatchDto> CreateSettlementBatchAsync(Guid userId, CreateSettlementBatchRequestDto request, CancellationToken ct = default);
    Task<SettlementBatchDto> ReconcileSettlementBatchAsync(Guid userId, Guid batchId, ReconcileSettlementBatchRequestDto request, CancellationToken ct = default);
    Task<PagedResult<SettlementBatchDto>> GetSettlementBatchesAsync(int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<TreasuryRebalanceSuggestionDto>> GetRebalanceSuggestionsAsync(CancellationToken ct = default);
    Task<TreasuryRebalanceDto> CreateRebalanceAsync(Guid userId, CreateTreasuryRebalanceRequestDto request, CancellationToken ct = default);
    Task<TreasuryRebalanceDto> ReviewRebalanceAsync(Guid userId, Guid rebalanceId, ReviewTreasuryRebalanceRequestDto request, CancellationToken ct = default);
    Task<TreasuryRebalanceDto> ExecuteRebalanceAsync(Guid userId, Guid rebalanceId, CancellationToken ct = default);
    Task<PagedResult<TreasuryRebalanceDto>> GetRebalancesAsync(int page, int pageSize, CancellationToken ct = default);
    Task<SettlementStatementImportDto> ImportSettlementStatementAsync(Guid userId, Guid batchId, ImportSettlementStatementFormDto request, CancellationToken ct = default);
}
