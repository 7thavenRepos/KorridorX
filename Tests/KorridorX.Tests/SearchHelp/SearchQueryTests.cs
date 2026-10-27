using System.Globalization;
using KorridorX.Services.SearchHelp;
namespace KorridorX.Tests.SearchHelp;
public sealed class SearchQueryTests
{
    [Fact] public void CollapsesWhitespace() => Assert.Equal("USD transfers",new SearchQuery(" USD  transfers ").Text);
    [Theory] [InlineData("")] [InlineData("a")] public void ShortInputsDoNotSearch(string text) => Assert.False(new SearchQuery(text).CanSearch);
    [Fact] public void RejectsOversize() => Assert.Throws<ArgumentException>(()=>new SearchQuery(new string('x',501)));
    [Fact] public void RejectsTooManyWords() => Assert.Throws<ArgumentException>(()=>new SearchQuery(string.Join(' ',Enumerable.Repeat("ab",81))));
    [Fact] public void RejectsControls() => Assert.Throws<ArgumentException>(()=>new SearchQuery("USD\0x"));
    [Fact] public void RecognizesIdentifier() { var id=Guid.NewGuid(); Assert.Equal(id,new SearchQuery(id.ToString()).ExactId); }
    [Fact] public void ExactReferenceWins() { var q=new SearchQuery("AB123"); Assert.Equal(1000,q.ScoreText("Transfer","AB123","USD")); }
    [Fact] public void SynonymsFindGuide() => Assert.True(new SearchQuery("add recipient").ScoreText("Add a beneficiary","","Manage suppliers")>0);
    [Fact] public void RequiresAllGuideTerms() => Assert.Equal(0,new SearchQuery("USD unfindable").ScoreText("USD wallet","","Funds"));
    [Fact] public void MatchesLiteralWildcardCharacters()
    {
        var row=new SearchRecordRow { Title="100% invoice", Reference="R_1", Id=Guid.NewGuid() };
        var source=new SearchRecordSource("invoices","Invoices","/admin/finance",new[]{row}.AsQueryable());
        Assert.Single(source.Match(new SearchQuery("100%")));
        Assert.Empty(source.Match(new SearchQuery("R_2")));
    }
    [Fact] public void CategoryQualifiersDoNotHideRecords()
    {
        var source=new SearchRecordSource("transfers","Transfers","/business/transfers",new[]{new SearchRecordRow { Title="T1", AssetCode="USD", StateText="Pending" }}.AsQueryable());
        Assert.Single(source.Match(new SearchQuery("pending transfers")));
        Assert.Empty(source.Match(new SearchQuery("GBP transfers")));
    }
    [Fact] public void AmountRemainsExactInvariantText()
    {
        var source=new SearchRecordSource("wallets","Wallets","/business/funding",Array.Empty<SearchRecordRow>().AsQueryable());
        var row=new SearchRecordRow { Id=Guid.NewGuid(),Title="Token",Amount=1234567890.123456789012345678m };
        var dto=source.ToDto(row,"business");
        Assert.Equal(row.Amount.Value.ToString(CultureInfo.InvariantCulture),dto.Amount);
        Assert.StartsWith("/business/search/record/wallets/",dto.Route);
    }
    [Fact] public void ExactRowHasDeterministicRank()
    {
        var rows=new[]{new SearchRecordRow{Id=Guid.NewGuid(),Reference="USD123",CreatedAt=DateTime.UtcNow.AddDays(-1)},new SearchRecordRow{Id=Guid.NewGuid(),Reference="USD123-long",CreatedAt=DateTime.UtcNow}}.AsQueryable();
        Assert.Equal("USD123",SearchRecordSource.Ranked(rows,new SearchQuery("USD123")).First().Reference);
    }
}
