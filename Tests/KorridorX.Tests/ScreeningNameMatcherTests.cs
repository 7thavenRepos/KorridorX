using KorridorX.Providers.Screening;

namespace KorridorX.Tests;

public sealed class ScreeningNameMatcherTests
{
    [Fact]
    public void CalculateScore_ReturnsOneHundredForNormalizedExactMatch()
    {
        var score = ScreeningNameMatcher.CalculateScore(
            "José  da-Silva",
            "JOSE DA SILVA");

        Assert.Equal(100m, score);
    }

    [Fact]
    public void CalculateScore_ReturnsStrongScoreWhenNameTokensAreReordered()
    {
        var score = ScreeningNameMatcher.CalculateScore(
            "Ada Nkem Okafor",
            "Okafor Ada Nkem");

        Assert.Equal(100m, score);
    }

    [Fact]
    public void CalculateScore_ReturnsLowScoreForUnrelatedNames()
    {
        var score = ScreeningNameMatcher.CalculateScore(
            "Ada Nkem Okafor",
            "Michael James Smith");

        Assert.True(score < 50m);
    }
}
