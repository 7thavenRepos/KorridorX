using KorridorX.Dtos.Finance;

namespace KorridorX.Services.Finance;

public interface IFinanceReportingService
{
    Task<FinanceSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
}
