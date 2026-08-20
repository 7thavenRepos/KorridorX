namespace KorridorX.Services.Marketplace;

public interface IMarketplaceMatchingEngine
{
    Task<int> MatchOrderAsync(Guid orderId, CancellationToken ct = default);
}
