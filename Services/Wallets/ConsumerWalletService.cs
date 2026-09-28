using KorridorX.Data;
using KorridorX.Dtos.Wallets;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Wallets;

public sealed class ConsumerWalletService : IConsumerWalletService
{
    private readonly AppDbContext _db;

    public ConsumerWalletService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ConsumerWalletDto>> GetWalletsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var profile = await GetProfileAsync(userId, ct);

        var mappings = await _db.CountryAssets
            .AsNoTracking()
            .Include(x => x.Asset)
            .Where(x => x.CountryCode == profile.CountryCode)
            .ToListAsync(ct);

        var mappingByAsset = mappings.ToDictionary(
            x => x.AssetCode,
            StringComparer.OrdinalIgnoreCase);

        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Include(x => x.Asset)
            .Where(x =>
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                x.AccountType == FinancialAccountType.Customer &&
                !x.IsDeleted)
            .ToListAsync(ct);

        return accounts
            .OrderByDescending(x =>
                mappingByAsset.TryGetValue(x.AssetCode, out var mapping) &&
                mapping.IsDefault)
            .ThenBy(x => x.AssetCode)
            .Select(x =>
            {
                mappingByAsset.TryGetValue(x.AssetCode, out var mapping);
                return ToDto(x, mapping);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<AvailableConsumerWalletAssetDto>> GetAvailableAssetsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var profile = await GetProfileAsync(userId, ct);

        var mappings = await EligibleMappings(profile.CountryCode)
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Asset.Code)
            .ToListAsync(ct);

        var existingAssets = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(x =>
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                x.AccountType == FinancialAccountType.Customer &&
                !x.IsDeleted)
            .Select(x => x.AssetCode)
            .ToListAsync(ct);

        var existing = existingAssets.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return mappings
            .Select(x => new AvailableConsumerWalletAssetDto(
                x.Asset.Code,
                x.Asset.Name,
                x.Asset.Symbol,
                x.Asset.DecimalPlaces,
                x.IsDefault,
                x.CanDeposit,
                x.CanWithdraw,
                x.CanSend,
                x.CanReceive,
                x.CanTrade,
                x.CanUseInstant,
                existing.Contains(x.AssetCode)))
            .ToList();
    }

    public async Task<ConsumerWalletDto> CreateWalletAsync(
        Guid userId,
        CreateConsumerWalletRequestDto request,
        CancellationToken ct = default)
    {
        var assetCode = NormalizeAssetCode(request.AssetCode);
        var profile = await GetProfileAsync(userId, ct);

        var mapping = await EligibleMappings(profile.CountryCode)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssetCode == assetCode, ct)
            ?? throw new InvalidOperationException(
                $"{assetCode} is not available as a consumer wallet in {profile.CountryCode}.");

        var existing = await _db.FinancialAccounts
            .Include(x => x.Asset)
            .SingleOrDefaultAsync(x =>
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == userId &&
                x.AssetCode == assetCode &&
                x.AccountType == FinancialAccountType.Customer &&
                !x.IsDeleted,
                ct);

        if (existing is not null)
        {
            return ToDto(existing, mapping);
        }

        var account = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.User,
            OwnerId = userId,
            AccountCode = BuildAccountCode(userId, assetCode),
            AssetCode = assetCode,
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = 0m,
            AvailableBalance = 0m,
            HeldBalance = 0m,
            CreatedByUserId = userId
        };

        _db.FinancialAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        await _db.Entry(account).Reference(x => x.Asset).LoadAsync(ct);

        return ToDto(account, mapping);
    }

    private IQueryable<CountryAsset> EligibleMappings(string countryCode) =>
        _db.CountryAssets
            .Include(x => x.Country)
            .Include(x => x.Asset)
            .Where(x =>
                x.CountryCode == countryCode &&
                x.Country.IsSupported &&
                x.Asset.IsSupported &&
                x.Asset.Type == AssetType.Fiat &&
                (x.CanDeposit ||
                 x.CanWithdraw ||
                 x.CanSend ||
                 x.CanReceive ||
                 x.CanTrade ||
                 x.CanUseInstant));

    private async Task<KorridorX.Models.Customers.CustomerProfile> GetProfileAsync(
        Guid userId,
        CancellationToken ct)
    {
        return await _db.CustomerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Customer profile not found.");
    }

    private static ConsumerWalletDto ToDto(
        FinancialAccount account,
        CountryAsset? mapping) =>
        new(
            account.Id,
            account.AssetCode,
            account.Asset.Name,
            account.Asset.Symbol,
            account.Asset.DecimalPlaces,
            account.Status.ToString(),
            account.SettledBalance,
            account.AvailableBalance,
            account.HeldBalance,
            mapping?.IsDefault ?? false,
            mapping?.CanDeposit ?? false,
            mapping?.CanWithdraw ?? false,
            mapping?.CanSend ?? false,
            mapping?.CanReceive ?? false,
            mapping?.CanTrade ?? false,
            mapping?.CanUseInstant ?? false,
            account.CreatedAt);

    private static string BuildAccountCode(Guid userId, string assetCode) =>
        $"KXW-{assetCode}-{userId:N}";

    private static string NormalizeAssetCode(string? value)
    {
        var code = value?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Asset code is required.");

        if (code.Length > 20)
            throw new InvalidOperationException("Asset code is invalid.");

        return code;
    }
}
