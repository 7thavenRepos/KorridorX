using KorridorX.Models.Compliance;
using KorridorX.Services.Compliance;

namespace KorridorX.Tests;

public class ComplianceLimitPolicyTests
{
    private static readonly ComplianceLimit Limit = new()
    {
        CurrencyCode = "USD",
        PerTransferLimit = 5000m,
        DailyLimit = 10000m,
        MonthlyLimit = 50000m
    };

    [Fact]
    public void Evaluate_AllowsAmountWithinAllLimits()
    {
        var result = ComplianceLimitPolicy.Evaluate(1000m, 2000m, 10000m, Limit);

        Assert.True(result.IsAllowed);
        Assert.Null(result.FailureCode);
    }

    [Fact]
    public void Evaluate_RejectsPerTransferLimitFirst()
    {
        var result = ComplianceLimitPolicy.Evaluate(5001m, 0m, 0m, Limit);

        Assert.False(result.IsAllowed);
        Assert.Equal("PER_TRANSFER_LIMIT_EXCEEDED", result.FailureCode);
    }

    [Fact]
    public void Evaluate_RejectsDailyLimit()
    {
        var result = ComplianceLimitPolicy.Evaluate(3000m, 8000m, 8000m, Limit);

        Assert.False(result.IsAllowed);
        Assert.Equal("DAILY_LIMIT_EXCEEDED", result.FailureCode);
    }

    [Fact]
    public void Evaluate_RejectsMonthlyLimit()
    {
        var result = ComplianceLimitPolicy.Evaluate(3000m, 1000m, 49000m, Limit);

        Assert.False(result.IsAllowed);
        Assert.Equal("MONTHLY_LIMIT_EXCEEDED", result.FailureCode);
    }
}
