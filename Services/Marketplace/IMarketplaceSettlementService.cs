namespace KorridorX.Services.Marketplace;

public interface IMarketplaceSettlementService
{
    Task SettleMatchAsync(Guid tradeMatchId, CancellationToken ct = default);
}
