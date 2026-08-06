namespace KorridorX.Services.BusinessTransfers;

public interface IBusinessReportExportService
{
    Task<byte[]> ExportTransfersCsvAsync(
        Guid userId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);

    Task<byte[]> ExportBatchCsvAsync(
        Guid userId,
        Guid batchId,
        CancellationToken ct = default);
}
