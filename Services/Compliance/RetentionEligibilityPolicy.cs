using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public static class RetentionEligibilityPolicy
{
    public static bool CanSoftDelete(
        RetentionRecordType recordType,
        bool isTerminal,
        bool hasActiveLegalHold)
    {
        if (hasActiveLegalHold || !isTerminal)
            return false;

        return recordType is
            RetentionRecordType.NotificationMessage or
            RetentionRecordType.ProviderRequestLog or
            RetentionRecordType.WebhookEvent or
            RetentionRecordType.AuditLog or
            RetentionRecordType.ScreeningRecord or
            RetentionRecordType.ComplianceCase or
            RetentionRecordType.RegulatoryReport;
    }
}
