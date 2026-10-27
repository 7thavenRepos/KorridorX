using KorridorX.Services.Recipients;

namespace KorridorX.Tests;

public sealed class RecipientDeliveryModePolicyTests
{
    [Theory]
    [InlineData("CA", "CAD", RecipientDeliveryPolicy.EmailTransfer)]
    [InlineData(" ca ", " cad ", RecipientDeliveryPolicy.EmailTransfer)]
    [InlineData("NG", "NGN", RecipientDeliveryPolicy.BankAccount)]
    [InlineData("ng", "ngn", RecipientDeliveryPolicy.BankAccount)]
    [InlineData("US", "USD", RecipientDeliveryPolicy.BankAccount)]
    [InlineData("GB", "GBP", RecipientDeliveryPolicy.BankAccount)]
    [InlineData("DE", "EUR", RecipientDeliveryPolicy.BankAccount)]
    [InlineData("FR", "EUR", RecipientDeliveryPolicy.BankAccount)]
    public void ActiveDeliveryRails_AreExplicit(string country, string asset, string expected) =>
        Assert.Equal(expected, RecipientDeliveryPolicy.Resolve(country, asset));

    [Theory]
    [InlineData("CA", "NGN")]
    [InlineData("NG", "CAD")]
    [InlineData("GB", "USD")]
    [InlineData("CA", "USDT")]
    [InlineData("", "CAD")]
    public void UnsupportedRails_DoNotPretendToWork(string country, string asset) =>
        Assert.Null(RecipientDeliveryPolicy.Resolve(country, asset));

    [Fact]
    public void DeliveryCatalog_AdvertisesEmailAndBankSeparately()
    {
        Assert.Contains(RecipientDeliveryPolicy.Available, x => x.Mode == RecipientDeliveryPolicy.EmailTransfer && x.AssetCode == "CAD");
        Assert.Contains(RecipientDeliveryPolicy.Available, x => x.Mode == RecipientDeliveryPolicy.BankAccount && x.AssetCode == "NGN");
    }
}
