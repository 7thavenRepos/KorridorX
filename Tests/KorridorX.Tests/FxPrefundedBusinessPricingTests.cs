using KorridorX.Services.Fx;

namespace KorridorX.Tests;

public sealed class FxPrefundedBusinessPricingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Prefunded_business_remittance_prices_without_provider_FX_execution_permission(
        bool sendEnabled, bool swapEnabled)
    {
        FxRateOperationPolicy.EnsureAllowed(FxRateUse.PrefundedBusinessRemittance,
            sendEnabled, swapEnabled);
    }

    [Fact]
    public void Other_operation_capabilities_are_not_bypassed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FxRateOperationPolicy.EnsureAllowed(FxRateUse.Transfer, false, true));
        Assert.Throws<InvalidOperationException>(() =>
            FxRateOperationPolicy.EnsureAllowed(FxRateUse.Preview, false, true));
        Assert.Throws<InvalidOperationException>(() =>
            FxRateOperationPolicy.EnsureAllowed(FxRateUse.Instant, true, false));
        FxRateOperationPolicy.EnsureAllowed(FxRateUse.Transfer, true, false);
        FxRateOperationPolicy.EnsureAllowed(FxRateUse.Instant, false, true);
        FxRateOperationPolicy.EnsureAllowed(FxRateUse.Valuation, false, false);
    }

    [Fact]
    public void Unknown_purpose_fails_closed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FxRateOperationPolicy.EnsureAllowed((FxRateUse)999, true, true));
    }
}
