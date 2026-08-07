using KorridorX.Services.Security;

namespace KorridorX.Tests;

public class RefreshTokenSecurityTests
{
    [Fact]
    public void Hash_IsDeterministicAndDoesNotReturnRawToken()
    {
        const string token = "refresh-token-value";

        var first = RefreshTokenSecurity.Hash(token);
        var second = RefreshTokenSecurity.Hash(token);

        Assert.Equal(first, second);
        Assert.NotEqual(token, first);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void Hash_RejectsEmptyTokens()
    {
        Assert.Throws<ArgumentException>(() => RefreshTokenSecurity.Hash(" "));
    }
}
