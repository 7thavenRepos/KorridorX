namespace KorridorX.Models.Enums;

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum RiskDecision
{
    Allow = 1,
    Review = 2,
    Block = 3
}

public enum RiskReviewDecision
{
    Release = 1,
    MaintainHold = 2
}
