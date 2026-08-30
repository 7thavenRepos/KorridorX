using KorridorX.Services.BusinessTransfers;

namespace KorridorX.Tests;

public class BusinessInvitationTokenTests
{
    [Fact]
    public void Create_ProducesDistinctHighEntropyTokensAndStableHashes()
    {
        var first = BusinessInvitationToken.Create();
        var second = BusinessInvitationToken.Create();

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 40);
        Assert.Equal(64, BusinessInvitationToken.Hash(first).Length);
        Assert.Equal(BusinessInvitationToken.Hash(first), BusinessInvitationToken.Hash(first));
        Assert.NotEqual(BusinessInvitationToken.Hash(first), BusinessInvitationToken.Hash(second));
    }

    [Fact]
    public void Hash_RejectsMissingToken()
    {
        var error = Assert.Throws<InvalidOperationException>(() => BusinessInvitationToken.Hash(" "));
        Assert.Contains("invalid or has expired", error.Message);
    }
}
