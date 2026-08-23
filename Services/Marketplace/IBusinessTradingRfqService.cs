using KorridorX.Dtos.Marketplace;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Marketplace;

public interface IBusinessTradingRfqService
{
    Task<BusinessTradingRfqDto> CreateAsync(FinancialAccountOwnerType requesterOwnerType, Guid requesterOwnerId, FinancialAccountOwnerType counterpartyOwnerType, Guid counterpartyOwnerId, Guid? actionedByUserId, Guid marketplacePairId, TradeOrderSide side, decimal quantity, DateTime? expiresAt, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> QuoteAsync(FinancialAccountOwnerType responderOwnerType, Guid responderOwnerId, Guid? actionedByUserId, Guid rfqId, decimal price, DateTime? expiresAt, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> AcceptAsync(FinancialAccountOwnerType requesterOwnerType, Guid requesterOwnerId, Guid? actionedByUserId, Guid rfqId, Guid quoteId, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> CancelAsync(FinancialAccountOwnerType requesterOwnerType, Guid requesterOwnerId, Guid? actionedByUserId, Guid rfqId, CancellationToken ct = default);
    Task<BusinessTradingRfqDto> GetAsync(FinancialAccountOwnerType ownerType, Guid ownerId, Guid rfqId, CancellationToken ct = default);
    Task<PagedResult<BusinessTradingRfqDto>> GetForOwnerAsync(FinancialAccountOwnerType ownerType, Guid ownerId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
