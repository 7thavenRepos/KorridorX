using KorridorX.Dtos.Recipients;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Recipients;

public interface IRecipientService
{
    Task<PagedResult<RecipientSummaryDto>> GetRecipientsAsync(Guid userId, string? search, string? countryCode, bool includeInactive, int page, int pageSize, CancellationToken ct = default);
    Task<RecipientDto> GetRecipientAsync(Guid userId, Guid recipientId, CancellationToken ct = default);
    Task<RecipientDto> CreateRecipientAsync(Guid userId, CreateRecipientRequestDto request, CancellationToken ct = default);
    Task<RecipientDto> UpdateRecipientAsync(Guid userId, Guid recipientId, UpdateRecipientRequestDto request, CancellationToken ct = default);
    Task DeleteRecipientAsync(Guid userId, Guid recipientId, CancellationToken ct = default);

    Task<RecipientBankAccountDto> AddBankAccountAsync(Guid userId, Guid recipientId, AddRecipientBankAccountRequestDto request, CancellationToken ct = default);
    Task<RecipientBankAccountDto> UpdateBankAccountAsync(Guid userId, Guid recipientId, Guid bankAccountId, UpdateRecipientBankAccountRequestDto request, CancellationToken ct = default);
    Task DeleteBankAccountAsync(Guid userId, Guid recipientId, Guid bankAccountId, CancellationToken ct = default);

    Task<RecipientMobileWalletDto> AddMobileWalletAsync(Guid userId, Guid recipientId, AddRecipientMobileWalletRequestDto request, CancellationToken ct = default);
    Task<RecipientMobileWalletDto> UpdateMobileWalletAsync(Guid userId, Guid recipientId, Guid mobileWalletId, UpdateRecipientMobileWalletRequestDto request, CancellationToken ct = default);
    Task DeleteMobileWalletAsync(Guid userId, Guid recipientId, Guid mobileWalletId, CancellationToken ct = default);
}
