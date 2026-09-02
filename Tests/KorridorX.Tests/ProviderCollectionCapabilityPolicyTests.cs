using KorridorX.Models.Enums;
using KorridorX.Services.Payments;

namespace KorridorX.Tests;

public sealed class ProviderCollectionCapabilityPolicyTests
{
    private readonly CollectionPaymentMethodPolicy _policy = new();

    [Fact]
    public void UsdCollection_AdvertisesOnlyImplementedCardInitiation()
    {
        var methods = _policy.GetSupportedMethods("US", "USD");

        Assert.Equal(new[] { PaymentMethod.Card }, methods);
    }

    [Fact]
    public void CadCollection_AdvertisesOnlyImplementedInteracInitiation()
    {
        var methods = _policy.GetSupportedMethods("CA", "CAD");

        Assert.Equal(new[] { PaymentMethod.Interac }, methods);
    }

    [Theory]
    [InlineData(PaymentMethod.Ach)]
    [InlineData(PaymentMethod.Wire)]
    [InlineData(PaymentMethod.BankTransfer)]
    [InlineData(PaymentMethod.VirtualAccount)]
    public void UsdCollection_DoesNotAdvertisePlannedButUnimplementedMethods(
        PaymentMethod paymentMethod)
    {
        Assert.False(_policy.IsSupported("US", "USD", paymentMethod));
    }

    [Fact]
    public void Policy_NormalizesCountryAndCurrencyCodes()
    {
        Assert.True(_policy.IsSupported(" us ", " usd ", PaymentMethod.Card));
        Assert.True(_policy.IsSupported(" ca ", " cad ", PaymentMethod.Interac));
    }

    [Fact]
    public void UnknownCorridor_ReturnsNoCollectionMethods()
    {
        Assert.Empty(_policy.GetSupportedMethods("NG", "NGN"));
    }
}
