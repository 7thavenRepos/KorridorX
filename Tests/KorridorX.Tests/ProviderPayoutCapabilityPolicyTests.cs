using KorridorX.Models.Providers;
using KorridorX.Services.Payments;

namespace KorridorX.Tests;

public sealed class ProviderPayoutCapabilityPolicyTests
{
    [Theory]
    [InlineData(PayoutDestinationType.RecipientBankAccount)]
    [InlineData(PayoutDestinationType.BusinessBeneficiaryBankAccount)]
    public void Blaaiz_SupportsImplementedBankDestinations(PayoutDestinationType destinationType)
    {
        Assert.True(PayoutDestinationCapabilityPolicy.IsSupported("Blaaiz", destinationType));
    }

    [Theory]
    [InlineData(PayoutDestinationType.RecipientMobileWallet)]
    [InlineData(PayoutDestinationType.BusinessBeneficiaryMobileWallet)]
    public void Blaaiz_DoesNotAdvertiseUnimplementedMobileWalletDestinations(PayoutDestinationType destinationType)
    {
        Assert.False(PayoutDestinationCapabilityPolicy.IsSupported("Blaaiz", destinationType));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PayoutDestinationCapabilityPolicy.EnsureSupported("Blaaiz", destinationType));

        Assert.Contains("Mobile-wallet payouts are not supported", exception.Message);
    }

    [Fact]
    public void Policy_NormalizesProviderCode()
    {
        Assert.True(PayoutDestinationCapabilityPolicy.IsSupported(
            " blaaiz ",
            PayoutDestinationType.RecipientBankAccount));
    }

    [Fact]
    public void UnknownProvider_DoesNotSupportDestinations()
    {
        Assert.False(PayoutDestinationCapabilityPolicy.IsSupported(
            "Unknown",
            PayoutDestinationType.RecipientBankAccount));
    }
}
