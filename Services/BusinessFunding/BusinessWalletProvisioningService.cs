using System.Data;
using System.Text.RegularExpressions;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.BusinessFunding;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.Audit;
using KorridorX.Services.BusinessTransfers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessFunding;

/// <summary>
/// Creates internal customer-owned Business fiat wallets at zero balance.
/// Never provisions external bank details or records a funding credit.
/// </summary>
public sealed class BusinessWalletProvisioningService(
    AppDbContext db,
    IBusinessAccessService accessService,
    IAuditService audit)
{
    public async Task<IReadOnlyList<AvailableBusinessWalletAssetDto>> GetAvailableAssetsAsync(
        Guid userId, CancellationToken ct = default)
    {
        var access = await accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewWallets, ct);
        var countryCode = await db.BusinessProfiles.AsNoTracking()
            .Where(x => x.Id == access.BusinessProfileId && !x.IsDeleted)
            .Select(x => x.CountryCode)
            .SingleAsync(ct);

        var options = await db.CountryAssets.AsNoTracking()
            .Include(x => x.Asset)
            .Where(x => x.CountryCode == countryCode && x.Country.IsSupported &&
                x.Asset.IsSupported && x.Asset.BusinessEnabled && x.Asset.Type == AssetType.Fiat &&
                (x.CanDeposit || x.CanWithdraw || x.CanSend || x.CanReceive ||
                 x.CanTrade || x.CanUseInstant))
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.AssetCode)
            .ToListAsync(ct);

        // Include deleted rows because the unique account identity remains reserved.
        var existing = await db.FinancialAccounts.AsNoTracking()
            .Where(x => x.OwnerType == FinancialAccountOwnerType.Business &&
                x.OwnerId == access.BusinessProfileId &&
                x.AccountType == FinancialAccountType.Customer)
            .Select(x => new { x.AssetCode, x.IsDeleted })
            .ToListAsync(ct);

        return options.Select(x => new AvailableBusinessWalletAssetDto(
            x.Asset.Code, x.Asset.Name, x.Asset.Symbol, x.Asset.DecimalPlaces,
            x.IsDefault, x.CanDeposit && x.Asset.DepositEnabled,
            x.CanWithdraw && x.Asset.WithdrawalEnabled,
            x.CanSend, x.CanReceive,
            x.CanTrade && x.Asset.TradingEnabled,
            x.CanUseInstant && x.Asset.InstantEnabled,
            existing.Any(y => y.AssetCode == x.AssetCode && !y.IsDeleted),
            existing.Any(y => y.AssetCode == x.AssetCode && y.IsDeleted)))
            .ToArray();
    }

    public async Task<BusinessWalletDto> CreateAsync(
        Guid userId, CreateBusinessWalletRequestDto request, CancellationToken ct = default)
    {
        var access = await accessService.EnsurePermissionAsync(userId, BusinessPermission.ManageFunding, ct);
        var assetCode = BusinessWalletProvisioningRules.NormalizeAssetCode(request.AssetCode);

        // The business and country are resolved from authenticated context, never request JSON.
        var countryCode = await db.BusinessProfiles.AsNoTracking()
            .Where(x => x.Id == access.BusinessProfileId && !x.IsDeleted)
            .Select(x => x.CountryCode)
            .SingleAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // One stable advisory lock per business/asset guards repeated and concurrent create requests.
        var lockKey = $"KorridorX:BusinessWallet:{access.BusinessProfileId:D}:{assetCode}";
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);

        var eligible = await db.CountryAssets.AsNoTracking().AnyAsync(x =>
            x.CountryCode == countryCode && x.AssetCode == assetCode &&
            x.Country.IsSupported && x.Asset.IsSupported && x.Asset.BusinessEnabled &&
            x.Asset.Type == AssetType.Fiat &&
            (x.CanDeposit || x.CanWithdraw || x.CanSend || x.CanReceive ||
             x.CanTrade || x.CanUseInstant), ct);
        if (!eligible)
            throw new InvalidOperationException(
                $"{assetCode} is not available as a Business fiat wallet in {countryCode}.");

        var existing = await db.FinancialAccounts.FirstOrDefaultAsync(x =>
            x.OwnerType == FinancialAccountOwnerType.Business &&
            x.OwnerId == access.BusinessProfileId &&
            x.AccountType == FinancialAccountType.Customer &&
            x.AssetCode == assetCode, ct);
        if (existing is not null)
        {
            if (existing.IsDeleted)
                throw new InvalidOperationException(
                    "This Business wallet identity has been retired. Contact Operations; it cannot be reused.");
            // Existing frozen/closed wallets are returned without reactivation or balance changes.
            await transaction.CommitAsync(ct);
            return ToDto(existing);
        }

        var wallet = BusinessWalletProvisioningRules.CreateZeroBalanceAccount(
            access.BusinessProfileId, assetCode, userId);
        // Even a different account type may have reserved the deterministic account code.
        if (await db.FinancialAccounts.AnyAsync(x => x.AccountCode == wallet.AccountCode, ct))
            throw new InvalidOperationException(
                "This wallet account code is already reserved. Contact Operations.");

        db.FinancialAccounts.Add(wallet);
        audit.Stage(new AuditRecordRequest(
            "BUSINESS_WALLET_CREATED", "BusinessFunding", nameof(FinancialAccount),
            wallet.Id.ToString("D"),
            NewValues: new { wallet.OwnerId, wallet.OwnerType, wallet.AccountType,
                wallet.AssetCode, wallet.Status, wallet.SettledBalance,
                wallet.AvailableBalance, wallet.HeldBalance },
            Metadata: new { FundingPerformed = false, ExternalAccountCreated = false },
            UserId: userId));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(wallet);
    }

    private static BusinessWalletDto ToDto(FinancialAccount x) => new(
        x.Id, x.OwnerId, x.AssetCode, x.Status,
        x.SettledBalance, x.AvailableBalance, x.HeldBalance,
        x.CreatedAt, x.LastUpdatedAt);
}

public static class BusinessWalletProvisioningRules
{
    private static readonly Regex ValidFiatCode =
        new("^[A-Z]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeAssetCode(string? input)
    {
        var code = input?.Trim().ToUpperInvariant();
        if (code is null || !ValidFiatCode.IsMatch(code))
            throw new InvalidOperationException("Select a valid three-letter fiat asset from master data.");
        return code;
    }

    public static FinancialAccount CreateZeroBalanceAccount(
        Guid businessId, string assetCode, Guid actorId)
    {
        if (businessId == Guid.Empty || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid Business and authorized actor are required.");
        var code = NormalizeAssetCode(assetCode);
        return new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.Business,
            OwnerId = businessId,
            AccountCode = $"BUS-{businessId:N}-{code}",
            AssetCode = code,
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = 0m,
            AvailableBalance = 0m,
            HeldBalance = 0m,
            CreatedByUserId = actorId
        };
    }
}
