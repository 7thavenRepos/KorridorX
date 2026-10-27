using KorridorX.Services.SearchHelp;
namespace KorridorX.Tests.SearchHelp;
public sealed class ErrorHelpTests
{
    private readonly HelpCatalog catalog = new();
    [Theory]
    [InlineData("No recipient payout method is configured for this currency.")]
    [InlineData("NO RECIPIENT PAYOUT METHOD IS CONFIGURED FOR THIS CURRENCY!")]
    [InlineData("Reference: kx-payout-001")]
    public void UserMessageFindsTheCorrectAuthorizedExplanation(string message)
    {
        var matches = catalog.Allowed(SearchHelpTestScopes.Admin("Support"))
            .Select(x => (Entry: x, Score: new SearchQuery(message).ScoreHelp(x)))
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ToArray();
        Assert.NotEmpty(matches);
        Assert.Equal("error-recipient-delivery-unavailable", matches[0].Entry.Slug);
    }
    [Fact] public void AdminDiagnosticsNeverLeakIntoBusinessHelp()
    {
        var allowed = catalog.Allowed(SearchHelpTestScopes.Business());
        Assert.DoesNotContain(allowed, x => x.Slug == "error-insufficient-house-liquidity");
        Assert.DoesNotContain(allowed, x => x.ErrorCodes.Contains("KX-PAYOUT-001"));
    }
    [Fact] public void FullErrorSearchDoesNotExpandDatabaseRecordPredicates()
    {
        var query = new SearchQuery("No recipient payout rail is configured for this asset. Choose another receiving asset or My wallet for a supported exchange.");
        Assert.False(query.CanSearchRecords);
        var entry = catalog.Entries.Single(x => x.Portal == "admin" && x.Slug == "error-recipient-delivery-unavailable");
        Assert.True(query.ScoreHelp(entry) >= 2600);
        Assert.True(new SearchQuery("USD transfers").CanSearchRecords);
    }
    [Fact] public void SimilarButDifferentErrorCodeDoesNotMatch()
    {
        var entry = catalog.Entries.Single(x => x.Portal == "admin" && x.Slug == "error-recipient-delivery-unavailable");
        Assert.Equal(0, new SearchQuery("KX-PAYOUT-0010").ScoreHelp(entry));
    }
}
