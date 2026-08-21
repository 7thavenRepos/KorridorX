using KorridorX.Services.EmbeddedFinance;

namespace KorridorX.Tests;

public sealed class EmbeddedWebhookSecurityTests
{
    private readonly EmbeddedWebhookUrlSecurityValidator _validator = new();

    [Theory]
    [InlineData("http://example.com/webhook")]
    [InlineData("https://localhost/webhook")]
    [InlineData("https://127.0.0.1/webhook")]
    [InlineData("https://10.0.0.1/webhook")]
    [InlineData("https://172.16.1.10/webhook")]
    [InlineData("https://192.168.1.10/webhook")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://user:pass@example.com/webhook")]
    public async Task Restricted_webhook_urls_are_rejected(string url) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => _validator.ValidateAsync(url));

    [Fact]
    public async Task Public_https_ip_is_allowed()
    {
        var result = await _validator.ValidateAsync("https://8.8.8.8/webhook");
        Assert.StartsWith("https://8.8.8.8", result, StringComparison.OrdinalIgnoreCase);
    }
}
