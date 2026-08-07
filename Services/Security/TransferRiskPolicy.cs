using KorridorX.Models.Enums;

namespace KorridorX.Services.Security;

public static class TransferRiskPolicy
{
    public static (RiskLevel Level, RiskDecision Decision) Resolve(
        int score,
        int reviewScore,
        int blockScore)
    {
        score = Math.Clamp(score, 0, 100);

        if (score >= blockScore)
            return (RiskLevel.Critical, RiskDecision.Block);
        if (score >= reviewScore)
            return (RiskLevel.High, RiskDecision.Review);
        if (score >= Math.Max(1, reviewScore / 2))
            return (RiskLevel.Medium, RiskDecision.Allow);
        return (RiskLevel.Low, RiskDecision.Allow);
    }
}
