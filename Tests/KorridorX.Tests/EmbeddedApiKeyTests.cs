using KorridorX.Services.EmbeddedFinance;
namespace KorridorX.Tests;
public sealed class EmbeddedApiKeyTests
{
    [Fact] public void Generated_key_can_be_parsed_and_verified(){var g=EmbeddedApiKey.Generate();Assert.StartsWith(EmbeddedApiKey.Prefix,g.ApiKey);Assert.True(EmbeddedApiKey.TryParse(g.ApiKey,out var id,out var secret));Assert.Equal(g.KeyId,id);Assert.True(EmbeddedApiKey.Verify(secret,g.SecretHash));Assert.Equal(secret[^4..],g.SecretLastFour);}
    [Fact] public void Modified_secret_does_not_verify(){var g=EmbeddedApiKey.Generate();Assert.True(EmbeddedApiKey.TryParse(g.ApiKey,out _,out var secret));var m=secret[..^1]+(secret[^1]=='A'?"B":"A");Assert.False(EmbeddedApiKey.Verify(m,g.SecretHash));}
    [Theory,InlineData(""),InlineData("abc"),InlineData("kx_live_bad"),InlineData("kx_live_01234567890123456789012345678901.")] public void Invalid_key_format_is_rejected(string value)=>Assert.False(EmbeddedApiKey.TryParse(value,out _,out _));
}
