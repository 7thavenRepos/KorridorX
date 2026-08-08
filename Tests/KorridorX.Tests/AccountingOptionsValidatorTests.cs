using KorridorX.Configuration;

namespace KorridorX.Tests;

public class AccountingOptionsValidatorTests
{
    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        var result = new AccountingOptionsValidator().Validate(null, new AccountingOptions());
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MissingBaseCurrency_Fails()
    {
        var result = new AccountingOptionsValidator().Validate(null, new AccountingOptions { BaseCurrencyCode = "" });
        Assert.True(result.Failed);
    }
}
