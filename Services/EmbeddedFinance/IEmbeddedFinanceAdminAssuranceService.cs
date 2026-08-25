using KorridorX.Dtos.EmbeddedFinance;

namespace KorridorX.Services.EmbeddedFinance;

public interface IEmbeddedFinanceAdminAssuranceService
{
    Task<EmbeddedFinanceAdminAssuranceDto> GetAsync(
        Guid? businessProfileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);

    Task<byte[]> ExportCsvAsync(
        Guid? businessProfileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);
}
