using KorridorX.Dtos.Providers;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Providers;

public interface IBankDirectoryService
{
    Task<ProviderBankSyncResultDto> SyncAsync(
        string? countryCode = null,
        string? currencyCode = null,
        CancellationToken ct = default);

    Task<PagedResult<ProviderBankDto>> GetBanksAsync(
        string? countryCode,
        string? search,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<RecipientBankVerificationDto> VerifyRecipientBankAccountAsync(
        Guid userId,
        Guid recipientId,
        Guid bankAccountId,
        CancellationToken ct = default);
}
