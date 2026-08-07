using KorridorX.Models.Enums;
using KorridorX.Services.Compliance;

namespace KorridorX.Tests;

public sealed class RetentionEligibilityPolicyTests
{
    [Fact]
    public void CanSoftDelete_ReturnsFalseWhenLegalHoldIsActive()
    {
        var result = RetentionEligibilityPolicy.CanSoftDelete(
            RetentionRecordType.RegulatoryReport,
            isTerminal: true,
            hasActiveLegalHold: true);

        Assert.False(result);
    }

    [Fact]
    public void CanSoftDelete_ReturnsFalseForNonTerminalRecord()
    {
        var result = RetentionEligibilityPolicy.CanSoftDelete(
            RetentionRecordType.ComplianceCase,
            isTerminal: false,
            hasActiveLegalHold: false);

        Assert.False(result);
    }

    [Fact]
    public void CanSoftDelete_ReturnsTrueForTerminalUnheldRecord()
    {
        var result = RetentionEligibilityPolicy.CanSoftDelete(
            RetentionRecordType.NotificationMessage,
            isTerminal: true,
            hasActiveLegalHold: false);

        Assert.True(result);
    }
}
