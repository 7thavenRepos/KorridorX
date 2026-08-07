using KorridorX.Configuration;

namespace KorridorX.Tests;

public class TreasuryOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var result = new TreasuryOptionsValidator().Validate(null, new TreasuryOptions());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NegativeVarianceTolerance_Fails()
    {
        var result = new TreasuryOptionsValidator().Validate(null, new TreasuryOptions
        {
            SettlementVarianceTolerance = -0.01m
        });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_SwapAmountBelowProviderMinimum_Fails()
    {
        var result = new TreasuryOptionsValidator().Validate(null, new TreasuryOptions
        {
            MinimumSwapAmount = 0.01m
        });
        Assert.True(result.Failed);
    }
}
