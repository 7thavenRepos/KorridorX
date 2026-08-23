using KorridorX.Data;
using KorridorX.Dtos.Recipients;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Recipients;
using KorridorX.Services.Compliance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Recipients;

public class RecipientService : IRecipientService
{
    private readonly AppDbContext _db;
    private readonly IComplianceScreeningService _screeningService;

    public RecipientService(
        AppDbContext db,
        IComplianceScreeningService screeningService)
    {
        _db = db;
        _screeningService = screeningService;
    }

    public async Task<PagedResult<RecipientSummaryDto>> GetRecipientsAsync(
        Guid userId,
        string? search,
        string? countryCode,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var customerProfileId = await GetCustomerProfileIdAsync(userId, ct);

        var query = _db.Recipients
            .AsNoTracking()
            .Where(x => x.CustomerProfileId == customerProfileId && !x.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var normalizedCountryCode = NormalizeCountryCode(countryCode);
            query = query.Where(x => x.CountryCode == normalizedCountryCode);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x =>
                x.FirstName.ToLower().Contains(normalizedSearch) ||
                x.LastName.ToLower().Contains(normalizedSearch) ||
                (x.MiddleName != null && x.MiddleName.ToLower().Contains(normalizedSearch)) ||
                (x.Nickname != null && x.Nickname.ToLower().Contains(normalizedSearch)) ||
                (x.PhoneNumber != null && x.PhoneNumber.ToLower().Contains(normalizedSearch)) ||
                (x.Email != null && x.Email.ToLower().Contains(normalizedSearch)));
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RecipientSummaryDto(
                x.Id,
                BuildFullName(x.FirstName, x.MiddleName, x.LastName),
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

    public async Task<RecipientDto> GetRecipientAsync(Guid userId, Guid recipientId, CancellationToken ct = default)
    {
        var recipient = await GetOwnedRecipientQuery(userId)
            .AsNoTracking()
            .Include(x => x.BankAccounts.Where(a => !a.IsDeleted))
            .Include(x => x.MobileWallets.Where(w => !w.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        return ToDto(recipient);
    }

    public async Task<RecipientDto> CreateRecipientAsync(Guid userId, CreateRecipientRequestDto request, CancellationToken ct = default)
    {
        ValidateRecipient(request.FirstName, request.LastName, request.CountryCode, request.Email);

        var customerProfileId = await GetCustomerProfileIdAsync(userId, ct);
        var normalizedCountryCode = NormalizeCountryCode(request.CountryCode);

        await EnsureCountryExistsAsync(normalizedCountryCode, ct);

        var recipient = new Recipient
        {
            CustomerProfileId = customerProfileId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            MiddleName = Clean(request.MiddleName),
            Nickname = Clean(request.Nickname),
            CountryCode = normalizedCountryCode,
            PhoneNumber = Clean(request.PhoneNumber),
            Email = Clean(request.Email)?.ToLower(),
            RelationshipToSender = Clean(request.RelationshipToSender),
            CreatedByUserId = userId
        };

        _db.Recipients.Add(recipient);
        await _db.SaveChangesAsync(ct);
        await _screeningService.ScreenRecipientAsync(
            recipient.Id,
            ScreeningReason.Onboarding,
            userId,
            null,
            ct);
        await _db.SaveChangesAsync(ct);

        return await GetRecipientAsync(userId, recipient.Id, ct);
    }

    public async Task<RecipientDto> UpdateRecipientAsync(Guid userId, Guid recipientId, UpdateRecipientRequestDto request, CancellationToken ct = default)
    {
        ValidateRecipient(request.FirstName, request.LastName, request.CountryCode, request.Email);

        var recipient = await GetOwnedRecipientQuery(userId)
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        var normalizedCountryCode = NormalizeCountryCode(request.CountryCode);
        await EnsureCountryExistsAsync(normalizedCountryCode, ct);

        recipient.FirstName = request.FirstName.Trim();
        recipient.LastName = request.LastName.Trim();
        recipient.MiddleName = Clean(request.MiddleName);
        recipient.Nickname = Clean(request.Nickname);
        recipient.CountryCode = normalizedCountryCode;
        recipient.PhoneNumber = Clean(request.PhoneNumber);
        recipient.Email = Clean(request.Email)?.ToLower();
        recipient.RelationshipToSender = Clean(request.RelationshipToSender);
        recipient.IsActive = request.IsActive;
        recipient.LastUpdatedAt = DateTime.UtcNow;
        recipient.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);
        await _screeningService.ScreenRecipientAsync(
            recipient.Id,
            ScreeningReason.ProfileChanged,
            userId,
            null,
            ct);
        await _db.SaveChangesAsync(ct);

        return await GetRecipientAsync(userId, recipient.Id, ct);
    }

    public async Task DeleteRecipientAsync(Guid userId, Guid recipientId, CancellationToken ct = default)
    {
        var recipient = await GetOwnedRecipientQuery(userId)
            .Include(x => x.BankAccounts)
            .Include(x => x.MobileWallets)
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        var now = DateTime.UtcNow;
        recipient.IsDeleted = true;
        recipient.DeletedAt = now;
        recipient.DeletedByUserId = userId;
        recipient.IsActive = false;

        foreach (var account in recipient.BankAccounts.Where(x => !x.IsDeleted))
        {
            account.IsDeleted = true;
            account.DeletedAt = now;
            account.DeletedByUserId = userId;
            account.IsActive = false;
        }

        foreach (var wallet in recipient.MobileWallets.Where(x => !x.IsDeleted))
        {
            wallet.IsDeleted = true;
            wallet.DeletedAt = now;
            wallet.DeletedByUserId = userId;
            wallet.IsActive = false;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<RecipientBankAccountDto> AddBankAccountAsync(Guid userId, Guid recipientId, AddRecipientBankAccountRequestDto request, CancellationToken ct = default)
    {
        ValidateBankAccount(request.CountryCode, request.CurrencyCode, request.BankName, request.AccountName, request.AccountNumber);

        var recipient = await GetOwnedRecipientQuery(userId)
            .Include(x => x.BankAccounts)
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        var countryCode = NormalizeCountryCode(request.CountryCode);
        var currencyCode = NormalizeAssetCode(request.CurrencyCode);

        await EnsureCountryAndCurrencyAreLinkedAsync(countryCode, currencyCode, ct);
        var providerBank = await ResolveProviderBankAsync(request.ProviderBankId, countryCode, ct);
        await EnsureBankAccountDoesNotExistAsync(recipientId, request.AccountNumber, providerBank?.Code ?? request.BankCode, ct);

        if (request.IsDefault)
        {
            ClearDefaultBankAccounts(recipient.BankAccounts);
        }

        var account = new RecipientBankAccount
        {
            RecipientId = recipientId,
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
            IsDefault = request.IsDefault,
            CreatedByUserId = userId
        };

        _db.RecipientBankAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return ToDto(account);
    }

    public async Task<RecipientBankAccountDto> UpdateBankAccountAsync(Guid userId, Guid recipientId, Guid bankAccountId, UpdateRecipientBankAccountRequestDto request, CancellationToken ct = default)
    {
        ValidateBankAccount(request.CountryCode, request.CurrencyCode, request.BankName, request.AccountName, request.AccountNumber);

        var recipient = await GetOwnedRecipientQuery(userId)
            .Include(x => x.BankAccounts.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        var account = recipient.BankAccounts.FirstOrDefault(x => x.Id == bankAccountId && !x.IsDeleted);
        if (account is null)
        {
            throw new InvalidOperationException("Bank account not found.");
        }

        var countryCode = NormalizeCountryCode(request.CountryCode);
        var currencyCode = NormalizeAssetCode(request.CurrencyCode);

        await EnsureCountryAndCurrencyAreLinkedAsync(countryCode, currencyCode, ct);
        var providerBank = await ResolveProviderBankAsync(request.ProviderBankId, countryCode, ct);
        await EnsureBankAccountDoesNotExistAsync(recipientId, request.AccountNumber, providerBank?.Code ?? request.BankCode, ct, bankAccountId);

        if (request.IsDefault)
        {
            ClearDefaultBankAccounts(recipient.BankAccounts.Where(x => x.Id != bankAccountId));
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
        account.IsVerified = false;
        account.VerifiedAt = null;
        account.ProviderVerifiedAccountName = null;
        account.ProviderVerificationReference = null;
        account.VerificationAttemptedAt = null;
        account.LastVerificationError = null;
        account.IsDefault = request.IsDefault;
        account.IsActive = request.IsActive;
        account.LastUpdatedAt = DateTime.UtcNow;
        account.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        return ToDto(account);
    }

    public async Task DeleteBankAccountAsync(Guid userId, Guid recipientId, Guid bankAccountId, CancellationToken ct = default)
    {
        var account = await _db.RecipientBankAccounts
            .Include(x => x.Recipient)
            .FirstOrDefaultAsync(x =>
                x.Id == bankAccountId &&
                x.RecipientId == recipientId &&
                !x.IsDeleted &&
                !x.Recipient.IsDeleted &&
                x.Recipient.CustomerProfile.UserId == userId, ct);

        if (account is null)
        {
            throw new InvalidOperationException("Bank account not found.");
        }

        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;
        account.DeletedByUserId = userId;
        account.IsActive = false;
        account.IsDefault = false;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<RecipientMobileWalletDto> AddMobileWalletAsync(Guid userId, Guid recipientId, AddRecipientMobileWalletRequestDto request, CancellationToken ct = default)
    {
        ValidateMobileWallet(request.CountryCode, request.CurrencyCode, request.ProviderName, request.WalletNumber, request.AccountName);

        var recipient = await GetOwnedRecipientQuery(userId)
            .Include(x => x.MobileWallets)
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        var countryCode = NormalizeCountryCode(request.CountryCode);
        var currencyCode = NormalizeAssetCode(request.CurrencyCode);

        await EnsureCountryAndCurrencyAreLinkedAsync(countryCode, currencyCode, ct);
        await EnsureMobileWalletDoesNotExistAsync(recipientId, request.ProviderName, request.WalletNumber, ct);

        if (request.IsDefault)
        {
            ClearDefaultMobileWallets(recipient.MobileWallets);
        }

        var wallet = new RecipientMobileWallet
        {
            RecipientId = recipientId,
            CountryCode = countryCode,
            CurrencyCode = currencyCode,
            ProviderName = request.ProviderName.Trim(),
            WalletNumber = request.WalletNumber.Trim(),
            AccountName = request.AccountName.Trim(),
            IsDefault = request.IsDefault,
            CreatedByUserId = userId
        };

        _db.RecipientMobileWallets.Add(wallet);
        await _db.SaveChangesAsync(ct);

        return ToDto(wallet);
    }

    public async Task<RecipientMobileWalletDto> UpdateMobileWalletAsync(Guid userId, Guid recipientId, Guid mobileWalletId, UpdateRecipientMobileWalletRequestDto request, CancellationToken ct = default)
    {
        ValidateMobileWallet(request.CountryCode, request.CurrencyCode, request.ProviderName, request.WalletNumber, request.AccountName);

        var recipient = await GetOwnedRecipientQuery(userId)
            .Include(x => x.MobileWallets.Where(w => !w.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == recipientId && !x.IsDeleted, ct);

        if (recipient is null)
        {
            throw new InvalidOperationException("Recipient not found.");
        }

        var wallet = recipient.MobileWallets.FirstOrDefault(x => x.Id == mobileWalletId && !x.IsDeleted);
        if (wallet is null)
        {
            throw new InvalidOperationException("Mobile wallet not found.");
        }

        var countryCode = NormalizeCountryCode(request.CountryCode);
        var currencyCode = NormalizeAssetCode(request.CurrencyCode);

        await EnsureCountryAndCurrencyAreLinkedAsync(countryCode, currencyCode, ct);
        await EnsureMobileWalletDoesNotExistAsync(recipientId, request.ProviderName, request.WalletNumber, ct, mobileWalletId);

        if (request.IsDefault)
        {
            ClearDefaultMobileWallets(recipient.MobileWallets.Where(x => x.Id != mobileWalletId));
        }

        wallet.CountryCode = countryCode;
        wallet.CurrencyCode = currencyCode;
        wallet.ProviderName = request.ProviderName.Trim();
        wallet.WalletNumber = request.WalletNumber.Trim();
        wallet.AccountName = request.AccountName.Trim();
        wallet.IsDefault = request.IsDefault;
        wallet.IsActive = request.IsActive;
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = userId;

        await _db.SaveChangesAsync(ct);

        return ToDto(wallet);
    }

    public async Task DeleteMobileWalletAsync(Guid userId, Guid recipientId, Guid mobileWalletId, CancellationToken ct = default)
    {
        var wallet = await _db.RecipientMobileWallets
            .Include(x => x.Recipient)
            .FirstOrDefaultAsync(x =>
                x.Id == mobileWalletId &&
                x.RecipientId == recipientId &&
                !x.IsDeleted &&
                !x.Recipient.IsDeleted &&
                x.Recipient.CustomerProfile.UserId == userId, ct);

        if (wallet is null)
        {
            throw new InvalidOperationException("Mobile wallet not found.");
        }

        wallet.IsDeleted = true;
        wallet.DeletedAt = DateTime.UtcNow;
        wallet.DeletedByUserId = userId;
        wallet.IsActive = false;
        wallet.IsDefault = false;

        await _db.SaveChangesAsync(ct);
    }

    private IQueryable<Recipient> GetOwnedRecipientQuery(Guid userId)
    {
        return _db.Recipients.Where(x => x.CustomerProfile.UserId == userId);
    }

    private async Task<Guid> GetCustomerProfileIdAsync(Guid userId, CancellationToken ct)
    {
        var profileId = await _db.CustomerProfiles
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (profileId is null)
        {
            throw new InvalidOperationException("Customer profile not found. Complete customer profile setup first.");
        }

        return profileId.Value;
    }

    private async Task EnsureCountryExistsAsync(string countryCode, CancellationToken ct)
    {
        var exists = await _db.Countries
            .AsNoTracking()
            .AnyAsync(x =>
                x.Code == countryCode &&
                x.IsSupported &&
                x.IsReceiveCountry,
                ct);

        if (!exists)
        {
            throw new InvalidOperationException($"Country '{countryCode}' is not supported as a receive country.");
        }
    }

    private async Task EnsureCountryAndCurrencyAreLinkedAsync(string countryCode, string currencyCode, CancellationToken ct)
    {
        var exists = await _db.CountryAssets
            .AsNoTracking()
            .AnyAsync(x =>
                x.CountryCode == countryCode &&
                x.AssetCode == currencyCode &&
                x.CanReceive &&
                x.Country.IsSupported &&
                x.Country.IsReceiveCountry &&
                x.Asset.IsSupported,
                ct);

        if (!exists)
        {
            throw new InvalidOperationException($"Currency '{currencyCode}' is not supported for receiving in country '{countryCode}'.");
        }
    }

    private async Task EnsureBankAccountDoesNotExistAsync(Guid recipientId, string accountNumber, string? bankCode, CancellationToken ct, Guid? excludeAccountId = null)
    {
        var cleanAccountNumber = accountNumber.Trim();
        var cleanBankCode = Clean(bankCode);

        var exists = await _db.RecipientBankAccounts.AnyAsync(x =>
            x.RecipientId == recipientId &&
            !x.IsDeleted &&
            x.AccountNumber == cleanAccountNumber &&
            x.BankCode == cleanBankCode &&
            (!excludeAccountId.HasValue || x.Id != excludeAccountId.Value), ct);

        if (exists)
        {
            throw new InvalidOperationException("This bank account already exists for the recipient.");
        }
    }

    private async Task EnsureMobileWalletDoesNotExistAsync(Guid recipientId, string providerName, string walletNumber, CancellationToken ct, Guid? excludeWalletId = null)
    {
        var cleanProviderName = providerName.Trim().ToLower();
        var cleanWalletNumber = walletNumber.Trim();

        var exists = await _db.RecipientMobileWallets.AnyAsync(x =>
            x.RecipientId == recipientId &&
            !x.IsDeleted &&
            x.ProviderName.ToLower() == cleanProviderName &&
            x.WalletNumber == cleanWalletNumber &&
            (!excludeWalletId.HasValue || x.Id != excludeWalletId.Value), ct);

        if (exists)
        {
            throw new InvalidOperationException("This mobile wallet already exists for the recipient.");
        }
    }

    private static void ClearDefaultBankAccounts(IEnumerable<RecipientBankAccount> accounts)
    {
        foreach (var account in accounts.Where(x => !x.IsDeleted))
        {
            account.IsDefault = false;
            account.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    private static void ClearDefaultMobileWallets(IEnumerable<RecipientMobileWallet> wallets)
    {
        foreach (var wallet in wallets.Where(x => !x.IsDeleted))
        {
            wallet.IsDefault = false;
            wallet.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    private static RecipientDto ToDto(Recipient recipient)
    {
        return new RecipientDto(
            recipient.Id,
            recipient.FirstName,
            recipient.LastName,
            recipient.MiddleName,
            recipient.Nickname,
            recipient.CountryCode,
            recipient.PhoneNumber,
            recipient.Email,
            recipient.RelationshipToSender,
            recipient.IsActive,
            recipient.CreatedAt,
            recipient.LastUpdatedAt,
            recipient.BankAccounts.Where(x => !x.IsDeleted).OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreatedAt).Select(ToDto).ToList(),
            recipient.MobileWallets.Where(x => !x.IsDeleted).OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.CreatedAt).Select(ToDto).ToList());
    }

    private static RecipientBankAccountDto ToDto(RecipientBankAccount account)
    {
        return new RecipientBankAccountDto(
            account.Id,
            account.RecipientId,
            account.CountryCode,
            account.CurrencyCode,
            account.BankName,
            account.BankCode,
            account.BranchCode,
            account.AccountName,
            account.AccountNumber,
            account.Iban,
            account.SwiftBic,
            account.RoutingNumber,
            account.SortCode,
            account.IsVerified,
            account.VerifiedAt,
            account.IsDefault,
            account.IsActive,
            account.ProviderBankId,
            account.ProviderVerifiedAccountName,
            account.ProviderVerificationReference,
            account.VerificationAttemptedAt,
            account.LastVerificationError);
    }

    private static RecipientMobileWalletDto ToDto(RecipientMobileWallet wallet)
    {
        return new RecipientMobileWalletDto(
            wallet.Id,
            wallet.RecipientId,
            wallet.CountryCode,
            wallet.CurrencyCode,
            wallet.ProviderName,
            wallet.WalletNumber,
            wallet.AccountName,
            wallet.IsVerified,
            wallet.VerifiedAt,
            wallet.IsDefault,
            wallet.IsActive);
    }

    private async Task<KorridorX.Models.Providers.ProviderBank?> ResolveProviderBankAsync(
        string? providerBankId,
        string countryCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerBankId))
        {
            return null;
        }

        var normalizedId = providerBankId.Trim();
        var bank = await _db.ProviderBanks
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == ProviderCode.Blaaiz &&
                x.ProviderBankId == normalizedId &&
                x.CountryCode == countryCode &&
                x.IsActive &&
                !x.IsDeleted,
                ct);

        if (bank is null)
        {
            throw new InvalidOperationException("The selected provider bank is invalid or inactive for this country.");
        }

        return bank;
    }

    private static void ValidateRecipient(string firstName, string lastName, string countryCode, string? email)
    {
        if (string.IsNullOrWhiteSpace(firstName)) throw new InvalidOperationException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new InvalidOperationException("Last name is required.");
        if (string.IsNullOrWhiteSpace(countryCode)) throw new InvalidOperationException("Country code is required.");
        if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@')) throw new InvalidOperationException("Recipient email address is invalid.");
    }

    private static void ValidateBankAccount(string countryCode, string currencyCode, string bankName, string accountName, string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) throw new InvalidOperationException("Country code is required.");
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new InvalidOperationException("Currency code is required.");
        if (string.IsNullOrWhiteSpace(bankName)) throw new InvalidOperationException("Bank name is required.");
        if (string.IsNullOrWhiteSpace(accountName)) throw new InvalidOperationException("Account name is required.");
        if (string.IsNullOrWhiteSpace(accountNumber)) throw new InvalidOperationException("Account number is required.");
    }

    private static void ValidateMobileWallet(string countryCode, string currencyCode, string providerName, string walletNumber, string accountName)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) throw new InvalidOperationException("Country code is required.");
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new InvalidOperationException("Currency code is required.");
        if (string.IsNullOrWhiteSpace(providerName)) throw new InvalidOperationException("Provider name is required.");
        if (string.IsNullOrWhiteSpace(walletNumber)) throw new InvalidOperationException("Wallet number is required.");
        if (string.IsNullOrWhiteSpace(accountName)) throw new InvalidOperationException("Account name is required.");
    }

    private static string NormalizeCountryCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Country code is required.");

        var code = value.Trim().ToUpperInvariant();
        if (code.Length > 10)
            throw new InvalidOperationException("Country code cannot exceed 10 characters.");

        return code;
    }

    private static string NormalizeAssetCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Asset code is required.");

        var code = value.Trim().ToUpperInvariant();
        if (code.Length > 20)
            throw new InvalidOperationException("Asset code cannot exceed 20 characters.");

        return code;
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildFullName(string firstName, string? middleName, string lastName)
    {
        return string.Join(" ", new[] { firstName, middleName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
