using KorridorX.Services.EmbeddedFinance;

namespace KorridorX.Tests;

public sealed class EmbeddedWebhookSecretTests
{
    [Fact]
    public void Generated_webhook_secret_has_expected_prefix_and_entropy()
    {
        var one = EmbeddedWebhookSecret.Generate();
        var two = EmbeddedWebhookSecret.Generate();

        Assert.StartsWith(EmbeddedWebhookSecret.Prefix, one);
        Assert.StartsWith(EmbeddedWebhookSecret.Prefix, two);
        Assert.NotEqual(one, two);
        Assert.True(one.Length > 40);
    }
}
