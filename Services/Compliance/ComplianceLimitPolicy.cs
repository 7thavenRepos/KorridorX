using KorridorX.Dtos.Compliance;
using KorridorX.Models.Compliance;

namespace KorridorX.Services.Compliance;

public static class ComplianceLimitPolicy
{
    public static ComplianceLimitDecision Evaluate(
        decimal transferAmount,
        decimal dailyUsed,
        decimal monthlyUsed,
        ComplianceLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);

        if (transferAmount > limit.PerTransferLimit)
        {
            return Denied(
                "PER_TRANSFER_LIMIT_EXCEEDED",
                $"The transfer amount exceeds the configured per-transfer limit of {limit.PerTransferLimit:N2} {limit.CurrencyCode}.",
                limit,
                dailyUsed,
                monthlyUsed);
        }

        if (dailyUsed + transferAmount > limit.DailyLimit)
        {
            return Denied(
                "DAILY_LIMIT_EXCEEDED",
                $"The transfer would exceed the configured daily limit of {limit.DailyLimit:N2} {limit.CurrencyCode}.",
                limit,
                dailyUsed,
                monthlyUsed);
        }

        if (monthlyUsed + transferAmount > limit.MonthlyLimit)
        {
            return Denied(
                "MONTHLY_LIMIT_EXCEEDED",
                $"The transfer would exceed the configured monthly limit of {limit.MonthlyLimit:N2} {limit.CurrencyCode}.",
                limit,
                dailyUsed,
                monthlyUsed);
        }

        return new ComplianceLimitDecision(
            true,
            null,
            null,
            limit.PerTransferLimit,
            limit.DailyLimit,
            dailyUsed,
            limit.MonthlyLimit,
            monthlyUsed);
    }

    private static ComplianceLimitDecision Denied(
        string code,
        string message,
        ComplianceLimit limit,
        decimal dailyUsed,
        decimal monthlyUsed) =>
        new(
            false,
            code,
            message,
            limit.PerTransferLimit,
            limit.DailyLimit,
            dailyUsed,
            limit.MonthlyLimit,
            monthlyUsed);
}
