using KorridorX.Dtos.Compliance;

namespace KorridorX.Services.Compliance;

public interface IComplianceManagementReportService
{
    Task<RegulatoryComplianceManagementSummaryDto> GetSummaryAsync(
        CancellationToken ct = default);
}
