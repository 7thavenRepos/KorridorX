using KorridorX.Models.Enums;
using KorridorX.Providers.Screening;

namespace KorridorX.Tests;

public sealed class OpenSanctionsResultParserTests
{
    [Fact]
    public void Parse_MapsSanctionsMatchToBlockingPotentialResult()
    {
        const string json = """
        {
          "responses": {
            "korridorx": {
              "status": 200,
              "results": [
                {
                  "id": "entity-1",
                  "caption": "Example Listed Person",
                  "score": 0.98,
                  "datasets": ["us_ofac_sdn"],
                  "properties": {
                    "topics": ["sanction"],
                    "country": ["us"],
                    "birthDate": ["1980-01-01"]
                  }
                }
              ]
            }
          }
        }
        """;

        var result = OpenSanctionsResultParser.Parse(json, 80m, 95m, false);

        Assert.Equal(ScreeningStatus.PotentialMatch, result.Status);
        Assert.True(result.IsBlocking);
        Assert.Equal(98m, result.HighestMatchScore);
        var match = Assert.Single(result.Matches);
        Assert.Equal(WatchlistType.Sanctions, match.WatchlistType);
        Assert.Equal("entity-1", match.ProviderMatchId);
    }

    [Fact]
    public void Parse_MapsPepMatchToNonBlockingPotentialResultByDefault()
    {
        const string json = """
        {
          "responses": {
            "korridorx": {
              "status": 200,
              "results": [
                {
                  "id": "entity-2",
                  "caption": "Example PEP",
                  "score": 0.97,
                  "datasets": ["peps"],
                  "properties": { "topics": ["role.pep"] }
                }
              ]
            }
          }
        }
        """;

        var result = OpenSanctionsResultParser.Parse(json, 80m, 95m, false);

        Assert.Equal(ScreeningStatus.PotentialMatch, result.Status);
        Assert.False(result.IsBlocking);
        Assert.Equal(WatchlistType.Pep, Assert.Single(result.Matches).WatchlistType);
    }

    [Fact]
    public void Parse_ReturnsClearWhenNoResultMeetsThreshold()
    {
        const string json = """
        {
          "responses": {
            "korridorx": {
              "status": 200,
              "results": [
                {
                  "id": "entity-3",
                  "caption": "Low Confidence Candidate",
                  "score": 0.42,
                  "datasets": [],
                  "properties": {}
                }
              ]
            }
          }
        }
        """;

        var result = OpenSanctionsResultParser.Parse(json, 80m, 95m, false);

        Assert.Equal(ScreeningStatus.Clear, result.Status);
        Assert.Empty(result.Matches);
    }
}
