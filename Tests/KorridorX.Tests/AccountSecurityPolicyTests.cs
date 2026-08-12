using KorridorX.Configuration;
using KorridorX.Models.Notifications;
using KorridorX.Services.Auth;
using KorridorX.Services.Notifications;
using KorridorX.Services.Security;

namespace KorridorX.Tests;

public sealed class AccountSecurityPolicyTests
{
    [Fact]
    public void Identity_token_codec_round_trips_transport_unsafe_tokens()
    {
        const string raw = "token+/= with spaces and unicode ✓";

        var encoded = IdentityTokenCodec.Encode(raw);

        Assert.DoesNotContain("+", encoded);
        Assert.DoesNotContain("/", encoded);
        Assert.True(IdentityTokenCodec.TryDecode(encoded, out var decoded));
        Assert.Equal(raw, decoded);
        Assert.False(IdentityTokenCodec.TryDecode("%%%", out _));
    }

    [Fact]
    public void Security_stamp_hash_comparison_rejects_missing_malformed_and_changed_values()
    {
        var hash = SecurityStampSecurity.Hash("current-stamp");

        Assert.True(SecurityStampSecurity.Matches(hash, "current-stamp"));
        Assert.False(SecurityStampSecurity.Matches(hash, "changed-stamp"));
        Assert.False(SecurityStampSecurity.Matches(null, "current-stamp"));
        Assert.False(SecurityStampSecurity.Matches("not-hex", "current-stamp"));
    }

    [Fact]
    public void Account_security_notification_body_is_never_returned_through_an_api_dto()
    {
        var sensitive = new NotificationMessage
        {
            Body = "https://app.example.test/auth/reset-password#token=secret",
            RelatedEntityType = NotificationSecurityPolicy.AccountSecurityEntityType
        };
        var ordinary = new NotificationMessage
        {
            Body = "Transfer completed",
            RelatedEntityType = "Transfer"
        };

        Assert.Equal(
            NotificationSecurityPolicy.RedactedBody,
            NotificationSecurityPolicy.BodyForApi(sensitive));
        Assert.Equal("Transfer completed", NotificationSecurityPolicy.BodyForApi(ordinary));
    }

    [Fact]
    public void Email_factory_html_encodes_names_and_builds_frontend_links()
    {
        var message = AccountSecurityEmailFactory.CreatePasswordReset(
            "https://app.example.test/",
            Guid.Parse("6b7be493-08ca-47e9-8c28-20de50fa829c"),
            "safe-token",
            "<script>alert(1)</script>");

        Assert.DoesNotContain("<script>", message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://app.example.test/auth/reset-password", message.Body);
        Assert.Contains("6b7be493-08ca-47e9-8c28-20de50fa829c", message.Body);
        Assert.Contains("safe-token", message.Body);
        Assert.Contains("#userId=", message.Body);
    }

    [Fact]
    public void Security_options_reject_unsafe_account_link_configuration()
    {
        var options = new SecurityOptions();
        options.Accounts.FrontendBaseUrl = "javascript:alert(1)";
        options.Accounts.TokenLifespanMinutes = 2;

        var result = new SecurityOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("FrontendBaseUrl", StringComparison.Ordinal));
        Assert.Contains(result.Failures, x => x.Contains("TokenLifespanMinutes", StringComparison.Ordinal));
    }
}
