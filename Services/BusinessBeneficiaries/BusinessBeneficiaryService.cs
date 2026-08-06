using KorridorX.Data;
using KorridorX.Dtos.BusinessBeneficiaries;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.BusinessTransfers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessBeneficiaries;

public class BusinessBeneficiaryService : IBusinessBeneficiaryService
{
    private readonly AppDbContext _db;
    private readonly IRemittanceProvider _provider;
    private readonly IBusinessAccessService _accessService;

    public BusinessBeneficiaryService(
        AppDbContext db,
        IRemittanceProvider provider,
        IBusinessAccessService accessService)
    {
        _db = db;
        _provider = provider;
        _accessService = accessService;
    }

    public async Task<PagedResult<BusinessBeneficiarySummaryDto>> GetAsync(
        Guid userId,
        string? search,
        string? countryCode,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewBeneficiaries, ct);
        var businessProfileId = await GetBusinessProfileIdAsync(userId, ct);

        var query = _db.BusinessBeneficiaries
            .AsNoTracking()
            .Where(x => x.BusinessProfileId == businessProfileId && !x.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var normalizedCountry = NormalizeCode(countryCode);
            query = query.Where(x => x.CountryCode == normalizedCountry);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(value) ||
                (x.Nickname != null && x.Nickname.ToLower().Contains(value)) ||
                (x.Email != null && x.Email.ToLower().Contains(value)) ||
                (x.PhoneNumber != null && x.PhoneNumber.ToLower().Contains(value)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BusinessBeneficiarySummaryDto(
                x.Id,
                x.BeneficiaryType,
                x.Name,
                x.Nickname,
                x.CountryCode,
                x.PhoneNumber,
                x.Email,
                x.IsActive,
                x.BankAccounts.Count(a => !a.IsDeleted),
                x.MobileWallets.Count(w => !w.IsDeleted),
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<BusinessBeneficiaryDto> GetAsync(
        Guid userId,
        Guid beneficiaryId,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewBeneficiaries, ct);
        var beneficiary = await OwnedQuery(userId)
            .AsNoTracking()
            .Include(x => x.BankAccounts.Where(a => !a.IsDeleted))
            .Include(x => x.MobileWallets.Where(w => !w.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == beneficiaryId && !x.IsDeleted, ct);

        return beneficiary is null
            ? throw new InvalidOperationException("Business beneficiary not found.")
            : ToDto(beneficiary);
    }

    public async Task<BusinessBeneficiaryDto> CreateAsync(
        Guid userId,
        CreateBusinessBeneficiaryRequestDto request,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        ValidateBeneficiary(request.Name, request.CountryCode, request.Email);
        var businessProfileId = await GetBusinessProfileIdAsync(userId, ct);
        var countryCode = NormalizeCode(request.CountryCode);
        await EnsureReceiveCountryAsync(countryCode, ct);

        var entity = new BusinessBeneficiary
        {
            BusinessProfileId = businessProfileId,
            BeneficiaryType = request.BeneficiaryType,
            Name = request.Name.Trim(),
            ContactFirstName = Clean(request.ContactFirstName),
            ContactLastName = Clean(request.ContactLastName),
            Nickname = Clean(request.Nickname),
            CountryCode = countryCode,
            PhoneNumber = Clean(request.PhoneNumber),
            Email = Clean(request.Email)?.ToLowerInvariant(),
            RelationshipOrPurpose = Clean(request.RelationshipOrPurpose),
            CreatedByUserId = userId
        };

        _db.BusinessBeneficiaries.Add(entity);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(userId, entity.Id, ct);
    }

    public async Task<BusinessBeneficiaryDto> UpdateAsync(
        Guid userId,
        Guid beneficiaryId,
        UpdateBusinessBeneficiaryRequestDto request,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        ValidateBeneficiary(request.Name, request.CountryCode, request.Email);
        var entity = await OwnedQuery(userId)
            .FirstOrDefaultAsync(x => x.Id == beneficiaryId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business beneficiary not found.");

        await EnsureBeneficiaryNotInActiveTransferAsync(beneficiaryId, ct);

        var countryCode = NormalizeCode(request.CountryCode);
        await EnsureReceiveCountryAsync(countryCode, ct);

        entity.BeneficiaryType = request.BeneficiaryType;
        entity.Name = request.Name.Trim();
        entity.ContactFirstName = Clean(request.ContactFirstName);
        entity.ContactLastName = Clean(request.ContactLastName);
        entity.Nickname = Clean(request.Nickname);
        entity.CountryCode = countryCode;
        entity.PhoneNumber = Clean(request.PhoneNumber);
        entity.Email = Clean(request.Email)?.ToLowerInvariant();
        entity.RelationshipOrPurpose = Clean(request.RelationshipOrPurpose);
        entity.IsActive = request.IsActive;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        return await GetAsync(userId, entity.Id, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid beneficiaryId, CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        var entity = await OwnedQuery(userId)
            .Include(x => x.BankAccounts)
            .Include(x => x.MobileWallets)
            .FirstOrDefaultAsync(x => x.Id == beneficiaryId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business beneficiary not found.");

        await EnsureBeneficiaryNotInActiveTransferAsync(beneficiaryId, ct);

        var now = DateTime.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.DeletedByUserId = userId;
        entity.IsActive = false;

        foreach (var account in entity.BankAccounts.Where(x => !x.IsDeleted))
        {
            account.IsDeleted = true;
            account.DeletedAt = now;
            account.DeletedByUserId = userId;
            account.IsActive = false;
            account.IsDefault = false;
        }

        foreach (var wallet in entity.MobileWallets.Where(x => !x.IsDeleted))
        {
            wallet.IsDeleted = true;
            wallet.DeletedAt = now;
            wallet.DeletedByUserId = userId;
            wallet.IsActive = false;
            wallet.IsDefault = false;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<BusinessBeneficiaryBankAccountDto> AddBankAccountAsync(
        Guid userId,
        Guid beneficiaryId,
        AddBusinessBeneficiaryBankAccountRequestDto request,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        ValidateBankAccount(request.CountryCode, request.CurrencyCode, request.BankName,
            request.AccountName, request.AccountNumber);

        var beneficiary = await OwnedQuery(userId)
            .Include(x => x.BankAccounts)
            .FirstOrDefaultAsync(x => x.Id == beneficiaryId && !x.IsDeleted && x.IsActive, ct)
            ?? throw new InvalidOperationException("Business beneficiary not found or inactive.");

        var countryCode = NormalizeCode(request.CountryCode);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        await EnsureCountryCurrencyAsync(countryCode, currencyCode, ct);
        await EnsureBankAccountUniqueAsync(beneficiaryId, request.AccountNumber, request.BankCode, null, ct);
        var providerBank = await ResolveProviderBankAsync(request.ProviderBankId, countryCode, ct);

        if (request.IsDefault)
        {
            ClearDefaultBankAccounts(beneficiary.BankAccounts);
        }

        var account = new BusinessBeneficiaryBankAccount
        {
            BusinessBeneficiaryId = beneficiaryId,
            CountryCode = countryCode,
            CurrencyCode = currencyCode,
            BankName = providerBank?.Name ?? request.BankName.Trim(),
            BankCode = providerBank?.Code ?? Clean(request.BankCode),
            BranchCode = Clean(request.BranchCode),
            AccountName = request.AccountName.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            Iban = Clean(request.Iban),
            SwiftBic = Clean(request.SwiftBic),
            RoutingNumber = Clean(request.RoutingNumber),
            SortCode = Clean(request.SortCode),
            ProviderBankId = providerBank?.ProviderBankId,
            IsDefault = request.IsDefault || !beneficiary.BankAccounts.Any(x => !x.IsDeleted && x.IsActive),
            CreatedByUserId = userId
        };

        beneficiary.BankAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    public async Task<BusinessBeneficiaryBankAccountDto> UpdateBankAccountAsync(
        Guid userId,
        Guid beneficiaryId,
        Guid bankAccountId,
        UpdateBusinessBeneficiaryBankAccountRequestDto request,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        ValidateBankAccount(request.CountryCode, request.CurrencyCode, request.BankName,
            request.AccountName, request.AccountNumber);

        var beneficiary = await OwnedQuery(userId)
            .Include(x => x.BankAccounts)
            .FirstOrDefaultAsync(x => x.Id == beneficiaryId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business beneficiary not found.");

        var account = beneficiary.BankAccounts.FirstOrDefault(x => x.Id == bankAccountId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Business beneficiary bank account not found.");

        await EnsureBankAccountNotInActiveTransferAsync(bankAccountId, ct);

        var countryCode = NormalizeCode(request.CountryCode);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        await EnsureCountryCurrencyAsync(countryCode, currencyCode, ct);
        await EnsureBankAccountUniqueAsync(beneficiaryId, request.AccountNumber, request.BankCode, bankAccountId, ct);
        var providerBank = await ResolveProviderBankAsync(request.ProviderBankId, countryCode, ct);

        var bankChanged = account.ProviderBankId != providerBank?.ProviderBankId ||
                          account.AccountNumber != request.AccountNumber.Trim();

        if (request.IsDefault)
        {
            ClearDefaultBankAccounts(beneficiary.BankAccounts);
        }

        account.CountryCode = countryCode;
        account.CurrencyCode = currencyCode;
        account.BankName = providerBank?.Name ?? request.BankName.Trim();
        account.BankCode = providerBank?.Code ?? Clean(request.BankCode);
        account.BranchCode = Clean(request.BranchCode);
        account.AccountName = request.AccountName.Trim();
        account.AccountNumber = request.AccountNumber.Trim();
        account.Iban = Clean(request.Iban);
        account.SwiftBic = Clean(request.SwiftBic);
        account.RoutingNumber = Clean(request.RoutingNumber);
        account.SortCode = Clean(request.SortCode);
        account.ProviderBankId = providerBank?.ProviderBankId;
        account.IsDefault = request.IsDefault;
        account.IsActive = request.IsActive;
        account.LastUpdatedAt = DateTime.UtcNow;
        account.LastUpdatedByUserId = userId;

        if (bankChanged)
        {
            ResetVerification(account);
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    public async Task<BusinessBeneficiaryBankVerificationDto> VerifyBankAccountAsync(
        Guid userId,
        Guid beneficiaryId,
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        var account = await _db.BusinessBeneficiaryBankAccounts
            .Include(x => x.BusinessBeneficiary)
            .ThenInclude(x => x.BusinessProfile)
            .ThenInclude(x => x.Users)
            .FirstOrDefaultAsync(x =>
                x.Id == bankAccountId &&
                x.BusinessBeneficiaryId == beneficiaryId &&
                !x.IsDeleted && x.IsActive &&
                !x.BusinessBeneficiary.IsDeleted &&
                (x.BusinessBeneficiary.BusinessProfile.OwnerUserId == userId ||
                 x.BusinessBeneficiary.BusinessProfile.Users.Any(u =>
                     u.UserId == userId && u.IsActive && !u.IsDeleted)),
                ct)
            ?? throw new InvalidOperationException("Business beneficiary bank account not found.");

        if (string.IsNullOrWhiteSpace(account.ProviderBankId))
        {
            throw new InvalidOperationException(
                "Select a bank from the synchronized provider bank directory before verification.");
        }

        var bank = await _db.ProviderBanks.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ProviderCode == _provider.ProviderCode &&
            x.ProviderBankId == account.ProviderBankId &&
            x.IsActive && !x.IsDeleted,
            ct) ?? throw new InvalidOperationException("The selected provider bank is inactive.");

        account.VerificationAttemptedAt = DateTime.UtcNow;
        account.LastVerificationError = null;

        try
        {
            var result = await _provider.ResolveBankAccountAsync(
                bank.ProviderBankId, account.AccountNumber, ct);

            var now = DateTime.UtcNow;
            account.BankName = bank.Name;
            account.BankCode = bank.Code;
            account.ProviderVerifiedAccountName = result.AccountName;
            account.ProviderVerificationReference = result.ProviderRequestLogId.ToString();
            account.IsVerified = true;
            account.VerifiedAt = now;
            account.LastUpdatedAt = now;
            account.LastUpdatedByUserId = userId;
            await _db.SaveChangesAsync(ct);

            return new BusinessBeneficiaryBankVerificationDto(
                beneficiaryId,
                account.Id,
                bank.ProviderBankId,
                bank.Name,
                MaskAccountNumber(account.AccountNumber),
                account.AccountName,
                result.AccountName,
                true,
                now);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            account.IsVerified = false;
            account.VerifiedAt = null;
            account.LastVerificationError = Truncate(ex.Message, 1000);
            account.LastUpdatedAt = DateTime.UtcNow;
            account.LastUpdatedByUserId = userId;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task DeleteBankAccountAsync(
        Guid userId,
        Guid beneficiaryId,
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        var account = await _db.BusinessBeneficiaryBankAccounts
            .Include(x => x.BusinessBeneficiary)
            .ThenInclude(x => x.BusinessProfile)
            .ThenInclude(x => x.Users)
            .FirstOrDefaultAsync(x =>
                x.Id == bankAccountId && x.BusinessBeneficiaryId == beneficiaryId &&
                !x.IsDeleted &&
                (x.BusinessBeneficiary.BusinessProfile.OwnerUserId == userId ||
                 x.BusinessBeneficiary.BusinessProfile.Users.Any(u =>
                     u.UserId == userId && u.IsActive && !u.IsDeleted)), ct)
            ?? throw new InvalidOperationException("Business beneficiary bank account not found.");

        await EnsureBankAccountNotInActiveTransferAsync(bankAccountId, ct);

        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;
        account.DeletedByUserId = userId;
        account.IsActive = false;
        account.IsDefault = false;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<BusinessBeneficiaryMobileWalletDto> AddMobileWalletAsync(
        Guid userId,
        Guid beneficiaryId,
        AddBusinessBeneficiaryMobileWalletRequestDto request,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        ValidateMobileWallet(request.CountryCode, request.CurrencyCode, request.ProviderName,
            request.WalletNumber, request.AccountName);

        var beneficiary = await OwnedQuery(userId)
            .Include(x => x.MobileWallets)
            .FirstOrDefaultAsync(x => x.Id == beneficiaryId && !x.IsDeleted && x.IsActive, ct)
            ?? throw new InvalidOperationException("Business beneficiary not found or inactive.");

        var countryCode = NormalizeCode(request.CountryCode);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        await EnsureCountryCurrencyAsync(countryCode, currencyCode, ct);
        await EnsureWalletUniqueAsync(beneficiaryId, request.ProviderName, request.WalletNumber, null, ct);

        if (request.IsDefault)
        {
            ClearDefaultWallets(beneficiary.MobileWallets);
        }

        var wallet = new BusinessBeneficiaryMobileWallet
        {
            BusinessBeneficiaryId = beneficiaryId,
            CountryCode = countryCode,
            CurrencyCode = currencyCode,
            ProviderName = request.ProviderName.Trim(),
            WalletNumber = request.WalletNumber.Trim(),
            AccountName = request.AccountName.Trim(),
            IsDefault = request.IsDefault || !beneficiary.MobileWallets.Any(x => !x.IsDeleted && x.IsActive),
            CreatedByUserId = userId
        };

        beneficiary.MobileWallets.Add(wallet);
        await _db.SaveChangesAsync(ct);
        return ToDto(wallet);
    }

    public async Task<BusinessBeneficiaryMobileWalletDto> UpdateMobileWalletAsync(
        Guid userId,
        Guid beneficiaryId,
        Guid mobileWalletId,
        UpdateBusinessBeneficiaryMobileWalletRequestDto request,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        ValidateMobileWallet(request.CountryCode, request.CurrencyCode, request.ProviderName,
            request.WalletNumber, request.AccountName);

        var beneficiary = await OwnedQuery(userId)
            .Include(x => x.MobileWallets)
            .FirstOrDefaultAsync(x => x.Id == beneficiaryId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Business beneficiary not found.");

        var wallet = beneficiary.MobileWallets.FirstOrDefault(x => x.Id == mobileWalletId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Business beneficiary mobile wallet not found.");

        await EnsureMobileWalletNotInActiveTransferAsync(mobileWalletId, ct);

        var countryCode = NormalizeCode(request.CountryCode);
        var currencyCode = NormalizeCode(request.CurrencyCode);
        await EnsureCountryCurrencyAsync(countryCode, currencyCode, ct);
        await EnsureWalletUniqueAsync(beneficiaryId, request.ProviderName, request.WalletNumber, mobileWalletId, ct);

        if (request.IsDefault)
        {
            ClearDefaultWallets(beneficiary.MobileWallets);
        }

        var changed = !string.Equals(wallet.ProviderName, request.ProviderName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                      wallet.WalletNumber != request.WalletNumber.Trim();

        wallet.CountryCode = countryCode;
        wallet.CurrencyCode = currencyCode;
        wallet.ProviderName = request.ProviderName.Trim();
        wallet.WalletNumber = request.WalletNumber.Trim();
        wallet.AccountName = request.AccountName.Trim();
        wallet.IsDefault = request.IsDefault;
        wallet.IsActive = request.IsActive;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = userId;

        if (changed)
        {
            wallet.IsVerified = false;
            wallet.VerifiedAt = null;
            wallet.ProviderBeneficiaryId = null;
            wallet.ProviderWalletId = null;
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(wallet);
    }

    public async Task DeleteMobileWalletAsync(
        Guid userId,
        Guid beneficiaryId,
        Guid mobileWalletId,
        CancellationToken ct = default)
    {
        await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageBeneficiaries, ct);
        var wallet = await _db.BusinessBeneficiaryMobileWallets
            .Include(x => x.BusinessBeneficiary)
            .ThenInclude(x => x.BusinessProfile)
            .ThenInclude(x => x.Users)
            .FirstOrDefaultAsync(x =>
                x.Id == mobileWalletId && x.BusinessBeneficiaryId == beneficiaryId &&
                !x.IsDeleted &&
                (x.BusinessBeneficiary.BusinessProfile.OwnerUserId == userId ||
                 x.BusinessBeneficiary.BusinessProfile.Users.Any(u =>
                     u.UserId == userId && u.IsActive && !u.IsDeleted)), ct)
            ?? throw new InvalidOperationException("Business beneficiary mobile wallet not found.");

        await EnsureMobileWalletNotInActiveTransferAsync(mobileWalletId, ct);

        wallet.IsDeleted = true;
        wallet.DeletedAt = DateTime.UtcNow;
        wallet.DeletedByUserId = userId;
        wallet.IsActive = false;
        wallet.IsDefault = false;
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureBeneficiaryNotInActiveTransferAsync(Guid beneficiaryId, CancellationToken ct)
    {
        var inUse = await _db.Transfers.AsNoTracking().AnyAsync(x =>
            x.BusinessBeneficiaryId == beneficiaryId &&
            !x.IsDeleted &&
            x.Status != TransferStatus.Completed &&
            x.Status != TransferStatus.Failed &&
            x.Status != TransferStatus.Cancelled &&
            x.Status != TransferStatus.Refunded &&
            x.Status != TransferStatus.Rejected,
            ct);

        if (inUse)
            throw new InvalidOperationException(
                "This beneficiary cannot be changed while an active transfer is using it.");
    }

    private async Task EnsureBankAccountNotInActiveTransferAsync(Guid bankAccountId, CancellationToken ct)
    {
        var inUse = await _db.Transfers.AsNoTracking().AnyAsync(x =>
            x.BusinessBeneficiaryBankAccountId == bankAccountId &&
            !x.IsDeleted &&
            x.Status != TransferStatus.Completed &&
            x.Status != TransferStatus.Failed &&
            x.Status != TransferStatus.Cancelled &&
            x.Status != TransferStatus.Refunded &&
            x.Status != TransferStatus.Rejected,
            ct);

        if (inUse)
            throw new InvalidOperationException(
                "This bank account cannot be changed while an active transfer is using it.");
    }

    private async Task EnsureMobileWalletNotInActiveTransferAsync(Guid mobileWalletId, CancellationToken ct)
    {
        var inUse = await _db.Transfers.AsNoTracking().AnyAsync(x =>
            x.BusinessBeneficiaryMobileWalletId == mobileWalletId &&
            !x.IsDeleted &&
            x.Status != TransferStatus.Completed &&
            x.Status != TransferStatus.Failed &&
            x.Status != TransferStatus.Cancelled &&
            x.Status != TransferStatus.Refunded &&
            x.Status != TransferStatus.Rejected,
            ct);

        if (inUse)
            throw new InvalidOperationException(
                "This mobile wallet cannot be changed while an active transfer is using it.");
    }

    private IQueryable<BusinessBeneficiary> OwnedQuery(Guid userId) =>
        _db.BusinessBeneficiaries.Where(x =>
            x.BusinessProfile.OwnerUserId == userId ||
            x.BusinessProfile.Users.Any(u => u.UserId == userId && u.IsActive && !u.IsDeleted));

    private async Task<Guid> GetBusinessProfileIdAsync(Guid userId, CancellationToken ct)
    {
        var access = await _accessService.GetAccessAsync(userId, ct);
        return access.BusinessProfileId;
    }

    private async Task EnsureReceiveCountryAsync(string countryCode, CancellationToken ct)
    {
        var exists = await _db.Countries.AsNoTracking().AnyAsync(x =>
            x.Code == countryCode && x.IsSupported && x.IsReceiveCountry, ct);
        if (!exists) throw new InvalidOperationException($"Country '{countryCode}' is not supported for receiving.");
    }

    private async Task EnsureCountryCurrencyAsync(string countryCode, string currencyCode, CancellationToken ct)
    {
        var exists = await _db.CountryCurrencies.AsNoTracking().AnyAsync(x =>
            x.CountryCode == countryCode && x.CurrencyCode == currencyCode && x.CanReceive &&
            x.Country.IsSupported && x.Country.IsReceiveCountry && x.Currency.IsSupported, ct);
        if (!exists) throw new InvalidOperationException(
            $"Currency '{currencyCode}' is not supported for receiving in country '{countryCode}'.");
    }

    private async Task<ProviderBank?> ResolveProviderBankAsync(
        string? providerBankId, string countryCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerBankId)) return null;
        var id = providerBankId.Trim();
        return await _db.ProviderBanks.AsNoTracking().FirstOrDefaultAsync(x =>
            x.ProviderCode == _provider.ProviderCode && x.ProviderBankId == id &&
            x.CountryCode == countryCode && x.IsActive && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("The selected provider bank is invalid or inactive.");
    }

    private async Task EnsureBankAccountUniqueAsync(
        Guid beneficiaryId, string accountNumber, string? bankCode,
        Guid? excludeId, CancellationToken ct)
    {
        var number = accountNumber.Trim();
        var code = Clean(bankCode);
        var exists = await _db.BusinessBeneficiaryBankAccounts.AnyAsync(x =>
            x.BusinessBeneficiaryId == beneficiaryId && !x.IsDeleted &&
            x.AccountNumber == number && x.BankCode == code &&
            (!excludeId.HasValue || x.Id != excludeId.Value), ct);
        if (exists) throw new InvalidOperationException("This bank account already exists for the beneficiary.");
    }

    private async Task EnsureWalletUniqueAsync(
        Guid beneficiaryId, string providerName, string walletNumber,
        Guid? excludeId, CancellationToken ct)
    {
        var provider = providerName.Trim().ToLower();
        var number = walletNumber.Trim();
        var exists = await _db.BusinessBeneficiaryMobileWallets.AnyAsync(x =>
            x.BusinessBeneficiaryId == beneficiaryId && !x.IsDeleted &&
            x.ProviderName.ToLower() == provider && x.WalletNumber == number &&
            (!excludeId.HasValue || x.Id != excludeId.Value), ct);
        if (exists) throw new InvalidOperationException("This mobile wallet already exists for the beneficiary.");
    }

    private static void ClearDefaultBankAccounts(IEnumerable<BusinessBeneficiaryBankAccount> accounts)
    {
        foreach (var account in accounts.Where(x => !x.IsDeleted))
        {
            account.IsDefault = false;
            account.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    private static void ClearDefaultWallets(IEnumerable<BusinessBeneficiaryMobileWallet> wallets)
    {
        foreach (var wallet in wallets.Where(x => !x.IsDeleted))
        {
            wallet.IsDefault = false;
            wallet.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    private static void ResetVerification(BusinessBeneficiaryBankAccount account)
    {
        account.IsVerified = false;
        account.VerifiedAt = null;
        account.ProviderVerifiedAccountName = null;
        account.ProviderVerificationReference = null;
        account.VerificationAttemptedAt = null;
        account.LastVerificationError = null;
        account.ProviderBeneficiaryId = null;
        account.ProviderBankAccountId = null;
    }

    private static BusinessBeneficiaryDto ToDto(BusinessBeneficiary x) => new(
        x.Id, x.BusinessProfileId, x.BeneficiaryType, x.Name, x.ContactFirstName,
        x.ContactLastName, x.Nickname, x.CountryCode, x.PhoneNumber, x.Email,
        x.RelationshipOrPurpose, x.IsActive, x.CreatedAt, x.LastUpdatedAt,
        x.BankAccounts.Where(a => !a.IsDeleted).OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt).Select(ToDto).ToList(),
        x.MobileWallets.Where(w => !w.IsDeleted).OrderByDescending(w => w.IsDefault)
            .ThenByDescending(w => w.CreatedAt).Select(ToDto).ToList());

    private static BusinessBeneficiaryBankAccountDto ToDto(BusinessBeneficiaryBankAccount x) => new(
        x.Id, x.BusinessBeneficiaryId, x.CountryCode, x.CurrencyCode, x.BankName,
        x.BankCode, x.BranchCode, x.AccountName, x.AccountNumber, x.Iban, x.SwiftBic,
        x.RoutingNumber, x.SortCode, x.ProviderBankId, x.ProviderVerifiedAccountName,
        x.IsVerified, x.VerifiedAt, x.LastVerificationError, x.IsDefault, x.IsActive);

    private static BusinessBeneficiaryMobileWalletDto ToDto(BusinessBeneficiaryMobileWallet x) => new(
        x.Id, x.BusinessBeneficiaryId, x.CountryCode, x.CurrencyCode, x.ProviderName,
        x.WalletNumber, x.AccountName, x.IsVerified, x.VerifiedAt, x.IsDefault, x.IsActive);

    private static void ValidateBeneficiary(string name, string countryCode, string? email)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Beneficiary name is required.");
        if (string.IsNullOrWhiteSpace(countryCode)) throw new InvalidOperationException("Country code is required.");
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@'))
            throw new InvalidOperationException("Beneficiary email address is invalid.");
    }

    private static void ValidateBankAccount(
        string countryCode, string currencyCode, string bankName,
        string accountName, string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) throw new InvalidOperationException("Country code is required.");
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new InvalidOperationException("Currency code is required.");
        if (string.IsNullOrWhiteSpace(bankName)) throw new InvalidOperationException("Bank name is required.");
        if (string.IsNullOrWhiteSpace(accountName)) throw new InvalidOperationException("Account name is required.");
        if (string.IsNullOrWhiteSpace(accountNumber)) throw new InvalidOperationException("Account number is required.");
    }

    private static void ValidateMobileWallet(
        string countryCode, string currencyCode, string providerName,
        string walletNumber, string accountName)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) throw new InvalidOperationException("Country code is required.");
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new InvalidOperationException("Currency code is required.");
        if (string.IsNullOrWhiteSpace(providerName)) throw new InvalidOperationException("Provider name is required.");
        if (string.IsNullOrWhiteSpace(walletNumber)) throw new InvalidOperationException("Wallet number is required.");
        if (string.IsNullOrWhiteSpace(accountName)) throw new InvalidOperationException("Account name is required.");
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    private static string MaskAccountNumber(string value) => value.Length <= 4 ? "****" : $"***{value[^4..]}";
}
