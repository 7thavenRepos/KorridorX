using KorridorX.Dtos.Compliance;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Compliance;

public interface ITransactionMonitoringService
{
    Task<TransactionMonitoringDecisionDto> MonitorAsync(
        Transfer transfer,
        Guid initiatedByUserId,
        CancellationToken ct = default);
}
