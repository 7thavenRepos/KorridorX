using KorridorX.Dtos.BusinessBeneficiaries;
using KorridorX.Infrastructure;

namespace KorridorX.Services.BusinessBeneficiaries;

public interface IBusinessBeneficiaryService
{
    Task<PagedResult<BusinessBeneficiarySummaryDto>> GetAsync(
        Guid userId, string? search, string? countryCode, bool includeInactive,
        int page, int pageSize, CancellationToken ct = default);

    Task<BusinessBeneficiaryDto> GetAsync(Guid userId, Guid beneficiaryId, CancellationToken ct = default);
    Task<BusinessBeneficiaryDto> CreateAsync(Guid userId, CreateBusinessBeneficiaryRequestDto request, CancellationToken ct = default);
    Task<BusinessBeneficiaryDto> UpdateAsync(Guid userId, Guid beneficiaryId, UpdateBusinessBeneficiaryRequestDto request, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid beneficiaryId, CancellationToken ct = default);

    Task<BusinessBeneficiaryBankAccountDto> AddBankAccountAsync(Guid userId, Guid beneficiaryId, AddBusinessBeneficiaryBankAccountRequestDto request, CancellationToken ct = default);
    Task<BusinessBeneficiaryBankAccountDto> UpdateBankAccountAsync(Guid userId, Guid beneficiaryId, Guid bankAccountId, UpdateBusinessBeneficiaryBankAccountRequestDto request, CancellationToken ct = default);
    Task<BusinessBeneficiaryBankVerificationDto> VerifyBankAccountAsync(Guid userId, Guid beneficiaryId, Guid bankAccountId, CancellationToken ct = default);
    Task DeleteBankAccountAsync(Guid userId, Guid beneficiaryId, Guid bankAccountId, CancellationToken ct = default);

    Task<BusinessBeneficiaryMobileWalletDto> AddMobileWalletAsync(Guid userId, Guid beneficiaryId, AddBusinessBeneficiaryMobileWalletRequestDto request, CancellationToken ct = default);
    Task<BusinessBeneficiaryMobileWalletDto> UpdateMobileWalletAsync(Guid userId, Guid beneficiaryId, Guid mobileWalletId, UpdateBusinessBeneficiaryMobileWalletRequestDto request, CancellationToken ct = default);
    Task DeleteMobileWalletAsync(Guid userId, Guid beneficiaryId, Guid mobileWalletId, CancellationToken ct = default);
}
