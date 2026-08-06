using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public interface IAdminBusinessKybService
{
    Task<PagedResult<AdminBusinessKybApplicationListItemDto>> GetApplicationsAsync(
        KybStatus? status,
        string? countryCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AdminBusinessKybApplicationDetailsDto> GetApplicationAsync(
        Guid applicationId,
        CancellationToken ct = default);
}
