using KorridorX.Services.Auth;
using KorridorX.Services.Notifications;

namespace KorridorX.Tests;

public sealed class BrandedEmailTemplateTests
{
    [Fact]
    public void Security_email_retains_exact_link_and_expiry_inside_responsive_brand_shell()
    {
        var userId = Guid.NewGuid();
        var message = AccountSecurityEmailFactory.CreateMobilePasswordReset(
            "https://app.staging.korridorx.com", userId, "single-use-token", "Kay <test>");
        var result = BrandedEmailTemplate.Render(message.Subject, message.Body, "https://app.staging.korridorx.com/");
        Assert.Contains(message.Body, result.Html);
        Assert.Contains("background:#005b31", result.Html);
        Assert.Contains("#ffebef", result.Html);
        Assert.Contains("max-width:600px", result.Html);
        Assert.Contains("/assets/brand/korridorx-symbol.png", result.Html);
        Assert.Contains("/assets/brand/korridorx-logo-light.png", result.Html);
        Assert.Contains("#userId=" + userId, result.PlainText);
        Assert.Contains("token=single-use-token", result.PlainText);
        Assert.Contains("client=mobile", result.PlainText);
        Assert.Contains("single-use and expires", result.PlainText);
        Assert.Contains("Kay &lt;test&gt;", result.Html);
        Assert.DoesNotContain("<p>", result.PlainText);
    }

    [Fact]
    public void Verification_codes_are_preserved_and_mockup_sample_details_are_not_published()
    {
        const string body = "<p>Your verification code is <strong>123456</strong>.</p><p>It expires in 10 minutes.</p>";
        var result = BrandedEmailTemplate.Render("Verify your sending email", body, "https://app.staging.korridorx.com");
        Assert.Contains(body, result.Html);
        Assert.Contains("123456", result.PlainText);
        Assert.Contains("10 minutes", result.PlainText);
        Assert.DoesNotContain("Gideon", result.Html);
        Assert.DoesNotContain("thepelostudio", result.Html);
        Assert.DoesNotContain("Folsom", result.Html);
        Assert.DoesNotContain("unsubscribe", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plain_message_and_heading_are_encoded_and_line_breaks_are_retained()
    {
        var result = BrandedEmailTemplate.Render("Update <script>", "Amount < 10 & available\nOpen your wallet.", "https://app.staging.korridorx.com");
        Assert.Contains("Update &lt;script&gt;", result.Html);
        Assert.Contains("Amount &lt; 10 &amp; available<br />Open", result.Html);
        Assert.Contains("Amount < 10 & available\nOpen", result.PlainText);
        Assert.DoesNotContain("<script>", result.Html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://name:secret@example.test")]
    [InlineData("https://example.test/?token=secret")]
    public void Invalid_base_url_does_not_create_an_action_or_asset_url(string url)
    {
        var result = BrandedEmailTemplate.Render("Notice", "Hello", url);
        Assert.DoesNotContain(url, result.Html);
        Assert.DoesNotContain("/assets/brand/", result.Html);
        Assert.DoesNotContain("href=", result.Html);
        Assert.Contains("KorridorX", result.Html);
    }

    [Fact]
    public void Funding_notice_reports_only_the_amount_actually_credited_and_encodes_reference()
    {
        var message = WalletActivityNotificationFactory.FundingReceived("CAD", 18.43m, "receipt <example>");
        Assert.Contains("CAD 18.43", message.Body);
        Assert.Contains("receipt &lt;example&gt;", message.Body);
        Assert.DoesNotContain("20.00", message.Body);
        Assert.DoesNotContain("1.57", message.Body);
    }

    [Fact]
    public void Funding_notice_preserves_additional_credited_decimal_places()
    {
        var message = WalletActivityNotificationFactory.FundingReceived("CAD", 18.4312m, "receipt");
        Assert.Contains("CAD 18.4312", message.Body);
        Assert.DoesNotContain("CAD 18.43<", message.Body);
    }

    [Theory]
    [InlineData(false, "set")]
    [InlineData(true, "changed")]
    public void Pin_notice_distinguishes_setup_and_change_without_accepting_a_pin(bool replacing, string verb)
    {
        var message = WalletActivityNotificationFactory.PinConfigured("Kay <example>", replacing);
        Assert.Contains("PIN was " + verb, message.Subject);
        Assert.Contains("PIN was " + verb, message.Body);
        Assert.Contains("Kay &lt;example&gt;", message.Body);
        Assert.Contains("never ask", message.Body);
    }
}
