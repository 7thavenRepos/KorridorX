namespace KorridorX.Services.Fx;

/// <summary>
/// FX prices are not Blaaiz payment instructions. A Business prefunded remittance
/// uses a live provider rate to set customer economics, then independently collects
/// the source currency and pays from an already-funded destination provider wallet.
/// Existing consumer/direct-FX and Instant capability rules remain unchanged.
/// </summary>
public static class FxRateOperationPolicy
{
    public static void EnsureAllowed(FxRateUse use, bool sendEnabled, bool swapEnabled)
    {
        switch (use)
        {
            case FxRateUse.PrefundedBusinessRemittance:
            case FxRateUse.Valuation:
                // Price-only reads do not call Blaaiz's FX send or swap endpoints.
                // Active rate, pair, connection, freshness and pricing validation
                // are enforced by the provider and FxProviderRateService.
                return;
            case FxRateUse.Transfer:
            case FxRateUse.Preview:
                if (!sendEnabled)
                    throw new InvalidOperationException(
                        "The provider has not enabled this FX rate for sending money.");
                return;
            case FxRateUse.Instant:
                if (!swapEnabled)
                    throw new InvalidOperationException(
                        "The provider has not enabled this FX rate for instant conversion.");
                return;
            default:
                throw new InvalidOperationException("Unsupported live FX rate purpose.");
        }
    }
}
