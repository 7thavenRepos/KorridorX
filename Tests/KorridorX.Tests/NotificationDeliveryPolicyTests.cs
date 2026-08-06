using KorridorX.Services.Notifications;

namespace KorridorX.Tests;

public class NotificationDeliveryPolicyTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    public void CalculateNextAttemptAt_UsesExponentialBackoff(
        int attemptCount,
        int expectedMinutes)
    {
        var attemptedAt = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

        var result = NotificationDeliveryPolicy.CalculateNextAttemptAt(
            attemptedAt,
            attemptCount,
            retryBaseMinutes: 2);

        Assert.Equal(attemptedAt.AddMinutes(expectedMinutes), result);
    }

    [Fact]
    public void CalculateNextAttemptAt_CapsDelayAtTwentyFourHours()
    {
        var attemptedAt = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

        var result = NotificationDeliveryPolicy.CalculateNextAttemptAt(
            attemptedAt,
            attemptCount: 20,
            retryBaseMinutes: 60);

        Assert.Equal(attemptedAt.AddHours(24), result);
    }
}
