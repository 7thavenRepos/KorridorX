using System.Text;
using System.Text.Json;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Services.Auth;

namespace KorridorX.Tests;

public sealed class ConsumerExternalIdentityPolicyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
    private const string Client = "owned.apps.googleusercontent.com";
    private const string Nonce = "server-issued-nonce-which-is-long-enough";

    private static Dictionary<string, object?> Claims() => new()
    {
        ["iss"] = "https://accounts.google.com", ["aud"] = Client, ["sub"] = "stable-provider-subject",
        ["nonce"] = Nonce, ["iat"] = Now.ToUnixTimeSeconds(), ["exp"] = Now.AddMinutes(5).ToUnixTimeSeconds()
    };

    private static string Subject(Dictionary<string, object?> values, string provider = "google")
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(values));
        return ExternalIdentityPolicy.Subject(doc.RootElement, provider, Nonce, [Client], [], Now);
    }

    [Theory]
    [InlineData("google"), InlineData(" Google "), InlineData("GOOGLE")]
    public void Provider_is_normalized(string value) => Assert.Equal("google", ExternalIdentityPolicy.Provider(value));

    [Theory]
    [InlineData("facebook"), InlineData(""), InlineData("google/../apple")]
    public void Unknown_provider_is_rejected(string value) => Assert.Throws<InvalidOperationException>(() => ExternalIdentityPolicy.Provider(value));

    [Fact]
    public void Consumer_role_and_type_are_both_required()
    {
        ExternalIdentityPolicy.RequireConsumer(UserType.Consumer, ["Consumer"]);
        Assert.Throws<UnauthorizedApiException>(() => ExternalIdentityPolicy.RequireConsumer(UserType.Business, ["Consumer"]));
        Assert.Throws<UnauthorizedApiException>(() => ExternalIdentityPolicy.RequireConsumer(UserType.Consumer, ["Consumer", "Admin"]));
        Assert.Throws<UnauthorizedApiException>(() => ExternalIdentityPolicy.RequireConsumer(UserType.Consumer, ["Business"]));
        Assert.Throws<UnauthorizedApiException>(() => ExternalIdentityPolicy.RequireConsumer(UserType.Consumer, []));
    }

    [Fact]
    public void Email_name_and_Apple_relay_never_replace_subject_or_resolve_accounts()
    {
        var value = Claims();
        value["email"] = "someone-elses-account@example.test";
        value["email_verified"] = true; value["name"] = "Changed name";
        Assert.Equal("stable-provider-subject", Subject(value));
        value["iss"] = "https://appleid.apple.com"; value["email"] = "relay@privaterelay.appleid.com";
        Assert.Equal("stable-provider-subject", Subject(value, "apple"));
        value.Remove("email"); value.Remove("email_verified"); value.Remove("name");
        Assert.Equal("stable-provider-subject", Subject(value, "apple"));
    }

    [Theory]
    [InlineData("iss", "https://evil.example"), InlineData("aud", "other.apps.googleusercontent.com")]
    [InlineData("nonce", "different-challenge"), InlineData("sub", ""), InlineData("sub", " padded ")]
    [InlineData("azp", "unowned.apps.googleusercontent.com")]
    public void Different_provider_audience_nonce_or_identity_is_rejected(string key, string value)
    { var data = Claims(); data[key] = value; Assert.Throws<UnauthorizedApiException>(() => Subject(data)); }

    [Theory]
    [InlineData("nonce"), InlineData("sub"), InlineData("iat"), InlineData("exp"), InlineData("iss"), InlineData("aud")]
    public void Required_claim_cannot_be_missing(string key)
    { var data = Claims(); data.Remove(key); Assert.Throws<UnauthorizedApiException>(() => Subject(data)); }

    [Theory]
    [InlineData(-301, 300), InlineData(31, 300), InlineData(0, 0), InlineData(0, -1)]
    public void Expired_future_or_old_proofs_are_rejected(long issuedOffset, long expiryOffset)
    { var data = Claims(); data["iat"] = Now.ToUnixTimeSeconds() + issuedOffset; data["exp"] = Now.ToUnixTimeSeconds() + expiryOffset; Assert.Throws<UnauthorizedApiException>(() => Subject(data)); }

    [Fact]
    public void Wrong_claim_types_or_multiple_audiences_are_rejected()
    {
        var data = Claims(); data["aud"] = new[] { Client, "other" };
        Assert.Throws<UnauthorizedApiException>(() => Subject(data));
        data = Claims(); data["iat"] = Now.ToUnixTimeSeconds().ToString();
        Assert.Throws<UnauthorizedApiException>(() => Subject(data));
        data = Claims(); data["nbf"] = Now.AddMinutes(1).ToUnixTimeSeconds();
        Assert.Throws<UnauthorizedApiException>(() => Subject(data));
    }

    [Theory]
    [InlineData("{\"aud\":\"a\",\"aud\":\"b\"}"), InlineData("{\"alg\":\"RS256\",\"alg\":\"none\"}"), InlineData("[]")]
    public void Ambiguous_JSON_is_rejected(string json)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Throws<UnauthorizedApiException>(() => ExternalIdentityPolicy.ReadObject(encoded));
    }

    [Theory]
    [InlineData("%%%"), InlineData("a"), InlineData(""), InlineData("YWJj=")]
    public void Malformed_base64url_is_rejected(string value) => Assert.Throws<UnauthorizedApiException>(() => ExternalIdentityPolicy.ReadObject(value));

    [Fact]
    public void Configured_presenter_is_accepted_without_widening_audience()
    {
        var values = Claims(); values["azp"] = "android.apps.googleusercontent.com";
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(values));
        Assert.Equal("stable-provider-subject", ExternalIdentityPolicy.Subject(doc.RootElement, "google", Nonce,
            [Client], ["android.apps.googleusercontent.com"], Now));
        values["aud"] = "android.apps.googleusercontent.com";
        Assert.Throws<UnauthorizedApiException>(() => Subject(values));
    }
}
