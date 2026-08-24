using KorridorX.Dtos.Compliance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Compliance;

public interface IAdminKycService
{
    Task<PagedResult<AdminKycApplicationListItemDto>> GetApplicationsAsync(
        KycStatus? status,
        string? countryCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AdminKycApplicationDetailsDto> GetApplicationAsync(
        Guid applicationId,
        CancellationToken ct = default);
}