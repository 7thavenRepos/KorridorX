using KorridorX.Data;
using KorridorX.Dtos.Providers;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Providers;

public class BankDirectoryService : IBankDirectoryService
{
    private readonly AppDbContext _db;
    private readonly IRemittanceProvider _provider;

    public BankDirectoryService(AppDbContext db, IRemittanceProvider provider)
    {
        _db = db;
        _provider = provider;
    }

    public async Task<ProviderBankSyncResultDto> SyncAsync(
        string? countryCode = null,
        string? currencyCode = null,
        CancellationToken ct = default)
    {
        var normalizedCountry = NormalizeOptionalCode(countryCode);
        var normalizedCurrency = NormalizeOptionalCode(currencyCode);
        var banks = await _provider.GetBanksAsync(normalizedCountry, normalizedCurrency, ct);
        var now = DateTime.UtcNow;

        var existing = await _db.ProviderBanks
            .Where(x => x.ProviderCode == _provider.ProviderCode && !x.IsDeleted)
            .ToListAsync(ct);

        var byProviderId = existing.ToDictionary(x => x.ProviderBankId, StringComparer.OrdinalIgnoreCase);
        var receivedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inserted = 0;
        var updated = 0;

        foreach (var bank in banks)
        {
            if (string.IsNullOrWhiteSpace(bank.ProviderBankId))
            {
                continue;
            }

            receivedIds.Add(bank.ProviderBankId);

            if (!byProviderId.TryGetValue(bank.ProviderBankId, out var entity))
            {
                entity = new ProviderBank
                {
                    ProviderCode = _provider.ProviderCode,
                    ProviderBankId = bank.ProviderBankId
                };

                _db.ProviderBanks.Add(entity);
                byProviderId[bank.ProviderBankId] = entity;
                inserted++;
            }
            else
            {
                updated++;
            }

            entity.Name = bank.Name?.Trim() ?? "";
            entity.Code = bank.Code?.Trim() ?? "";
            entity.NationalBankCode = Clean(bank.NationalBankCode);
            entity.ProviderCountryId = Clean(bank.ProviderCountryId);
            entity.CountryCode = NormalizeOptionalCode(bank.CountryCode) ?? normalizedCountry ?? "";
            entity.CountryName = Clean(bank.CountryName);
            entity.RawPayloadJson = bank.RawResponseJson;
            entity.IsActive = true;
            entity.LastSyncedAt = now;
            entity.LastUpdatedAt = now;
        }

        var deactivated = 0;
        var canDeactivateMissing = normalizedCurrency is null;
        if (canDeactivateMissing)
        {
            foreach (var bank in existing.Where(x =>
                         (normalizedCountry is null || x.CountryCode == normalizedCountry) &&
                         !receivedIds.Contains(x.ProviderBankId) &&
                         x.IsActive))
            {
                bank.IsActive = false;
                bank.LastUpdatedAt = now;
                deactivated++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ProviderBankSyncResultDto(
            banks.Count,
            inserted,
            updated,
            deactivated,
            now);
    }

    public async Task<PagedResult<ProviderBankDto>> GetBanksAsync(
        string? countryCode,
        string? search,
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ProviderBanks
            .AsNoTracking()
            .Where(x => x.ProviderCode == ProviderCode.Blaaiz && !x.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var normalizedCountry = NormalizeOptionalCode(countryCode);
        if (normalizedCountry is not null)
        {
            query = query.Where(x => x.CountryCode == normalizedCountry);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x =>
                x.Name.ToLower().Contains(value) ||
                x.Code.ToLower().Contains(value) ||
                (x.NationalBankCode != null && x.NationalBankCode.ToLower().Contains(value)));
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new ProviderBankDto(
                x.Id,
                x.ProviderCode,
                x.ProviderBankId,
                x.Name,
                x.Code,
                x.NationalBankCode,
                x.CountryCode,
                x.CountryName,
                x.IsActive,
                x.LastSyncedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<RecipientBankVerificationDto> VerifyRecipientBankAccountAsync(
        Guid userId,
        Guid recipientId,
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        var account = await _db.RecipientBankAccounts
            .Include(x => x.Recipient)
            .FirstOrDefaultAsync(x =>
                x.Id == bankAccountId &&
                x.RecipientId == recipientId &&
                !x.IsDeleted &&
                x.IsActive &&
                !x.Recipient.IsDeleted &&
                x.Recipient.CustomerProfile.UserId == userId,
                ct);

        if (account is null)
        {
            throw new InvalidOperationException("Recipient bank account not found.");
        }

        if (string.IsNullOrWhiteSpace(account.ProviderBankId))
        {
            throw new InvalidOperationException(
                "Select a bank from the synchronized provider bank directory before verification.");
        }

        var bank = await _db.ProviderBanks
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == _provider.ProviderCode &&
                x.ProviderBankId == account.ProviderBankId &&
                x.IsActive &&
                !x.IsDeleted,
                ct);

        if (bank is null)
        {
            throw new InvalidOperationException("The selected provider bank is no longer active.");
        }

        account.VerificationAttemptedAt = DateTime.UtcNow;
        account.LastVerificationError = null;

        try
        {
            var result = await _provider.ResolveBankAccountAsync(
                bank.ProviderBankId,
                account.AccountNumber,
                ct);

            var now = DateTime.UtcNow;
            account.BankName = bank.Name;
            account.BankCode = bank.Code;
            account.ProviderVerifiedAccountName = result.AccountName;
            account.ProviderVerificationReference = result.ProviderRequestLogId.ToString();
            account.IsVerified = true;
            account.VerifiedAt = now;
            account.LastVerificationError = null;
            account.LastUpdatedAt = now;
            account.LastUpdatedByUserId = userId;

            var mapping = await GetOrCreateProviderMappingAsync(
                PayoutDestinationType.RecipientBankAccount,
                account.Id,
                _provider.ProviderCode,
                userId,
                ct);

            mapping.ProviderBankId = bank.ProviderBankId;
            mapping.ProviderVerifiedAccountName = result.AccountName;
            mapping.ProviderVerificationReference = result.ProviderRequestLogId.ToString();
            mapping.IsVerified = true;
            mapping.VerificationAttemptedAt = account.VerificationAttemptedAt;
            mapping.VerifiedAt = now;
            mapping.LastVerificationError = null;
            mapping.IsActive = true;
            mapping.LastUpdatedAt = now;
            mapping.LastUpdatedByUserId = userId;

            await _db.SaveChangesAsync(ct);

            return new RecipientBankVerificationDto(
                recipientId,
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

            var mapping = await GetOrCreateProviderMappingAsync(
                PayoutDestinationType.RecipientBankAccount,
                account.Id,
                _provider.ProviderCode,
                userId,
                CancellationToken.None);

            mapping.ProviderBankId = account.ProviderBankId;
            mapping.IsVerified = false;
            mapping.VerificationAttemptedAt = account.VerificationAttemptedAt;
            mapping.VerifiedAt = null;
            mapping.ProviderVerifiedAccountName = null;
            mapping.ProviderVerificationReference = null;
            mapping.LastVerificationError = account.LastVerificationError;
            mapping.IsActive = true;
            mapping.LastUpdatedAt = DateTime.UtcNow;
            mapping.LastUpdatedByUserId = userId;

            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<PayoutDestinationProviderMapping> GetOrCreateProviderMappingAsync(
        PayoutDestinationType destinationType,
        Guid destinationId,
        ProviderCode providerCode,
        Guid userId,
        CancellationToken ct)
    {
        var mapping = await _db.PayoutDestinationProviderMappings
            .FirstOrDefaultAsync(x =>
                x.DestinationType == destinationType &&
                x.DestinationId == destinationId &&
                x.ProviderCode == providerCode &&
                !x.IsDeleted,
                ct);

        if (mapping is not null)
        {
            return mapping;
        }

        mapping = new PayoutDestinationProviderMapping
        {
            DestinationType = destinationType,
            DestinationId = destinationId,
            ProviderCode = providerCode,
            IsActive = true,
            CreatedByUserId = userId
        };

        _db.PayoutDestinationProviderMappings.Add(mapping);
        return mapping;
    }

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string MaskAccountNumber(string value) =>
        value.Length <= 4 ? "****" : $"***{value[^4..]}";
}
