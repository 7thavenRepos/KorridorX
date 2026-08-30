using KorridorX.Dtos.Instant;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Instant;

public interface IInstantTradingService
{
    Task<IReadOnlyList<InstantPairDto>> GetPairsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InstantPairAdminDto>> GetAdminPairsAsync(
        InstantPairStatus? status = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<InstantHouseAccountDto>> GetHouseAccountsAsync(
        string? assetCode = null,
        CancellationToken ct = default);
    Task<InstantQuoteDto> CreateQuoteAsync(Guid userId, CreateInstantQuoteRequestDto request, CancellationToken ct = default);
    Task<InstantTradeDto> ExecuteQuoteAsync(Guid userId, Guid quoteId, CancellationToken ct = default);
    Task<PagedResult<InstantTradeDto>> GetMyTradesAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<InstantQuoteDto> CreateQuoteForOwnerAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        string countryCode,
        CreateInstantQuoteRequestDto request,
        Guid? businessProfileId = null,
        Guid? actionedByUserId = null,
        CancellationToken ct = default);
    Task<InstantTradeDto> ExecuteQuoteForOwnerAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        Guid quoteId,
        Guid? actionedByUserId = null,
        CancellationToken ct = default);
    Task<PagedResult<InstantTradeDto>> GetTradesForOwnerAsync(
        FinancialAccountOwnerType ownerType,
        Guid ownerId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<InstantPairDto> CreatePairAsync(Guid actionedByUserId, CreateInstantPairRequestDto request, CancellationToken ct = default);
    Task<InstantPairDto> UpdatePairAsync(Guid actionedByUserId, Guid pairId, UpdateInstantPairRequestDto request, CancellationToken ct = default);
    Task<InstantPairDto> SetPairStatusAsync(Guid actionedByUserId, Guid pairId, InstantPairStatus status, CancellationToken ct = default);

    Task<PagedResult<InstantQuoteAdminDto>> GetAdminQuotesAsync(
        InstantQuoteStatus? status = null,
        Guid? pairId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    Task<PagedResult<InstantTradeDto>> GetAdminTradesAsync(
        InstantTradeStatus? status = null,
        Guid? pairId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstantLiquidityDto>> GetLiquidityAsync(CancellationToken ct = default);

    Task<InstantQuoteMaintenanceResultDto> ExpireQuotesAsync(
        int take = 500,
        CancellationToken ct = default);

    Task<IReadOnlyList<InstantSettlementExceptionDto>> GetSettlementExceptionsAsync(
        int take = 100,
        CancellationToken ct = default);
}
