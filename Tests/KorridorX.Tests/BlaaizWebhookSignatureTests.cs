using KorridorX.Services.Webhooks;

namespace KorridorX.Tests;

public class BlaaizWebhookSignatureTests
{
    private const string Secret = "unit-test-webhook-secret";
    private const string Payload = "{\"event_id\":\"evt-1\",\"status\":\"COMPLETED\"}";

    [Fact]
    public void IsValid_AcceptsCorrectSignature()
    {
        var now = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var signature = BlaaizWebhookSignature.Create(timestamp, Payload, Secret);

        var valid = BlaaizWebhookSignature.IsValid(
            Payload,
            $"sha256={signature}",
            timestamp,
            Secret,
            TimeSpan.FromMinutes(5),
            now);

        Assert.True(valid);
    }

    [Fact]
    public void IsValid_RejectsIncorrectSignature()
    {
        var now = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var timestamp = now.ToUnixTimeSeconds().ToString();

        var valid = BlaaizWebhookSignature.IsValid(
            Payload,
            "bad-signature",
            timestamp,
            Secret,
            TimeSpan.FromMinutes(5),
            now);

        Assert.False(valid);
    }

    [Fact]
    public void IsValid_RejectsStaleTimestamp()
    {
        var now = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var stale = now.AddMinutes(-10);
        var timestamp = stale.ToUnixTimeSeconds().ToString();
        var signature = BlaaizWebhookSignature.Create(timestamp, Payload, Secret);

        var valid = BlaaizWebhookSignature.IsValid(
            Payload,
            signature,
            timestamp,
            Secret,
            TimeSpan.FromMinutes(5),
            now);

        Assert.False(valid);
    }
}
