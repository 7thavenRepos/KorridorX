using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Compliance;

public interface IComplianceLimitService
{
    Task<ComplianceLimitDecision> EvaluateAsync(
        Transfer transfer,
        CancellationToken ct = default);

    Task EnsureWithinLimitsAsync(
        Transfer transfer,
        CancellationToken ct = default);

    Task<PagedResult<ComplianceLimitDto>> GetLimitsAsync(
        string? countryCode,
        string? currencyCode,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<ComplianceLimitDto> UpsertAsync(
        Guid userId,
        UpsertComplianceLimitRequestDto request,
        CancellationToken ct = default);
}
