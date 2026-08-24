using System.ComponentModel.DataAnnotations;
using KorridorX.Dtos.Notifications;

namespace KorridorX.Tests;

public sealed class NotificationAuditAdminContractTests
{
    [Fact]
    public void Notification_retry_requires_reason()
    {
        var request = new RetryNotificationRequestDto
        {
            ResetAttemptCount = false,
            Reason = ""
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void Notification_retry_accepts_reason()
    {
        var request = new RetryNotificationRequestDto
        {
            ResetAttemptCount = true,
            Reason = "Provider outage has been resolved."
        };

        Assert.True(IsValid(request));
    }

    private static bool IsValid(object value)
    {
        var context = new ValidationContext(value);
        var results = new List<ValidationResult>();

        return Validator.TryValidateObject(
            value,
            context,
            results,
            validateAllProperties: true);
    }
}