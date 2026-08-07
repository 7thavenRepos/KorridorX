using KorridorX.Models.Enums;
using KorridorX.Services.Security;

namespace KorridorX.Tests;

public class TransferRiskPolicyTests
{
    [Theory]
    [InlineData(0, RiskLevel.Low, RiskDecision.Allow)]
    [InlineData(20, RiskLevel.Medium, RiskDecision.Allow)]
    [InlineData(40, RiskLevel.High, RiskDecision.Review)]
    [InlineData(69, RiskLevel.High, RiskDecision.Review)]
    [InlineData(70, RiskLevel.Critical, RiskDecision.Block)]
    [InlineData(100, RiskLevel.Critical, RiskDecision.Block)]
    public void Resolve_UsesConfiguredBoundaries(
        int score,
        RiskLevel expectedLevel,
        RiskDecision expectedDecision)
    {
        var result = TransferRiskPolicy.Resolve(score, reviewScore: 40, blockScore: 70);

        Assert.Equal(expectedLevel, result.Level);
        Assert.Equal(expectedDecision, result.Decision);
    }

    [Fact]
    public void Resolve_ClampsScoresAboveOneHundred()
    {
        var result = TransferRiskPolicy.Resolve(250, 40, 70);

        Assert.Equal(RiskDecision.Block, result.Decision);
        Assert.Equal(RiskLevel.Critical, result.Level);
    }
}
