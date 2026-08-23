using KorridorX.Models.Payments;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedPayoutSettlementService
{
    Task ApplyStatusEffectAsync(
        Payout payout,
        bool statusChanged,
        CancellationToken ct = default);
}
