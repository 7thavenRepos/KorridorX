using KorridorX.Configuration;
using KorridorX.Services.Auth;

namespace KorridorX.Tests;

public sealed class MobileAccountRecoveryLinkTests
{
    private static readonly Guid UserId =
        Guid.Parse("6b7be493-08ca-47e9-8c28-20de50fa829c");

    [Theory]
    [InlineData(false, "confirm-email")]
    [InlineData(true, "reset-password")]
    public void Mobile_email_uses_https_fragment_and_a_copyable_fallback(bool reset, string action)
    {
        var token = "safe+token/with&characters=";
        var message = reset
            ? AccountSecurityEmailFactory.CreateMobilePasswordReset("https://app.staging.korridorx.com/", UserId, token, "Kay <test>")
            : AccountSecurityEmailFactory.CreateMobileEmailConfirmation("https://app.staging.korridorx.com/", UserId, token, "Kay <test>");
        var matches = System.Text.RegularExpressions.Regex.Matches(message.Body, "href=\"([^\"]+)\"");
        Assert.Equal(2, matches.Count);
        var link = System.Net.WebUtility.HtmlDecode(matches[0].Groups[1].Value);
        Assert.Equal(link, System.Net.WebUtility.HtmlDecode(matches[1].Groups[1].Value));
        var uri = new Uri(link);
        Assert.Equal("https", uri.Scheme);
        Assert.Equal("app.staging.korridorx.com", uri.Host);
        Assert.Equal($"/auth/{action}", uri.AbsolutePath);
        Assert.Empty(uri.Query);
        var fragment = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Fragment[1..]);
        Assert.Equal(UserId.ToString(), fragment["userId"].ToString());
        Assert.Equal(token, fragment["token"].ToString());
        Assert.Equal("mobile", fragment["client"].ToString());
        Assert.Equal(3, fragment.Count);
        Assert.DoesNotContain("korridorx://", message.Body);
        Assert.DoesNotContain("Kay <test>", message.Body);
        Assert.Contains("Kay &lt;test&gt;", message.Body);
        Assert.Contains("copy and paste", message.Body);
        Assert.Contains("background:", message.Body);
        Assert.Contains(link, System.Net.WebUtility.HtmlDecode(message.Body));
    }

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("http://127.0.0.1:4200")]
    public void Mobile_email_supports_local_web_development(string frontendBaseUrl)
    {
        var message = AccountSecurityEmailFactory.CreateMobileEmailConfirmation(frontendBaseUrl, UserId, "token", "Kay");
        Assert.Contains(frontendBaseUrl + "/auth/confirm-email#", message.Body);
    }

    [Theory]
    [InlineData("korridorx://account-security")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://app.staging.korridorx.com")]
    [InlineData("https://user:password@app.staging.korridorx.com")]
    [InlineData("https://app.staging.korridorx.com?redirect=elsewhere")]
    [InlineData("https://app.staging.korridorx.com#fragment")]
    public void Mobile_email_rejects_non_web_or_unsafe_base_urls(string baseUrl)
    {
        Assert.Throws<ArgumentException>(() => AccountSecurityEmailFactory.CreateMobileEmailConfirmation(baseUrl, UserId, "token", "Kay"));
        Assert.Throws<ArgumentException>(() => AccountSecurityEmailFactory.CreateMobilePasswordReset(baseUrl, UserId, "token", "Kay"));
    }

    [Fact]
    public void Business_email_retains_its_web_flow_without_a_mobile_marker()
    {
        var confirmation = AccountSecurityEmailFactory.CreateEmailConfirmation("https://app.staging.korridorx.com", UserId, "token", "Kay");
        var reset = AccountSecurityEmailFactory.CreatePasswordReset("https://app.staging.korridorx.com", UserId, "token", "Kay");
        Assert.Contains("/auth/confirm-email#", confirmation.Body);
        Assert.Contains("/auth/reset-password#", reset.Body);
        Assert.DoesNotContain("client=mobile", confirmation.Body);
        Assert.DoesNotContain("client=mobile", reset.Body);
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
