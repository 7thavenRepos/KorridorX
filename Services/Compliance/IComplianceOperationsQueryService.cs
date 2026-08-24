using KorridorX.Dtos.Compliance;

namespace KorridorX.Services.Compliance;

public interface IComplianceOperationsQueryService
{
    Task<ComplianceOperationsSummaryDto> GetSummaryAsync(
        CancellationToken ct = default);
}