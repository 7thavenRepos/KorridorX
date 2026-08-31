using KorridorX.Configuration;
using KorridorX.Services.Auth;

namespace KorridorX.Tests;

public sealed class MobileAccountRecoveryLinkTests
{
    private static readonly Guid UserId =
        Guid.Parse("6b7be493-08ca-47e9-8c28-20de50fa829c");

    [Fact]
    public void Mobile_password_reset_uses_the_guarded_app_link_and_fragment()
    {
        var message = AccountSecurityEmailFactory.CreateMobilePasswordReset(
            "korridorx://account-security/",
            UserId,
            "safe-token",
            "Consumer");

        Assert.Contains("korridorx://account-security/reset-password", message.Body);
        Assert.Contains("#userId=", message.Body);
        Assert.Contains(UserId.ToString(), message.Body);
        Assert.Contains("safe-token", message.Body);
        Assert.DoesNotContain("?userId=", message.Body);
    }

    [Fact]
    public void Mobile_email_confirmation_uses_the_guarded_app_link_and_fragment()
    {
        var message = AccountSecurityEmailFactory.CreateMobileEmailConfirmation(
            "korridorx://account-security",
            UserId,
            "safe-token",
            "Consumer");

        Assert.Contains("korridorx://account-security/confirm-email", message.Body);
        Assert.Contains("#userId=", message.Body);
        Assert.Contains(UserId.ToString(), message.Body);
        Assert.Contains("safe-token", message.Body);
    }

    [Theory]
    [InlineData("https://app.example.test/account-security")]
    [InlineData("korridorx://other")]
    [InlineData("korridorx://account-security/reset-password")]
    [InlineData("korridorx://account-security?token=secret")]
    [InlineData("korridorx://account-security#token=secret")]
    public void Security_options_reject_unsafe_mobile_deep_link_configuration(
        string mobileDeepLinkBaseUrl)
    {
        var options = new SecurityOptions();
        options.Accounts.MobileDeepLinkBaseUrl = mobileDeepLinkBaseUrl;

        var result = new SecurityOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("MobileDeepLinkBaseUrl", StringComparison.Ordinal));
    }

    [Fact]
    public void Security_options_accept_the_exact_mobile_deep_link_boundary()
    {
        var result = new SecurityOptionsValidator().Validate(
            null,
            new SecurityOptions());

        Assert.False(result.Failed);
    }
}
