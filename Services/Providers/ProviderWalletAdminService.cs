using System.Globalization;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace KorridorX.Services.Providers;

public sealed class ProviderWalletAdminService(
    AppDbContext db, IBlaaizApiClient api, IOptions<BlaaizOptions> options,
    IProviderWalletResolver resolver, IAuditService audit)
{
    public async Task<IReadOnlyList<ProviderWalletConfigurationDto>> ListAsync(CancellationToken ct) =>
        (await db.ProviderWalletConfigurations.AsNoTracking()
            .Where(x => !x.IsDeleted && x.Environment == resolver.EnvironmentName)
            .OrderBy(x => x.AssetCode).ThenBy(x => x.NetworkCode).ThenBy(x => x.DisplayName).ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<IReadOnlyList<DiscoveredProviderWallet>> DiscoverAsync(bool crypto, CancellationToken ct)
    {
        if (!options.Value.IsEnabled)
            throw new InvalidOperationException("Blaaiz is disabled. Wallets can be registered as drafts; provider verification needs an enabled sandbox connection.");
        if (!crypto)
        {
            var response = await api.ListWalletsAsync(ct);
            return response.Data.Select(x => new DiscoveredProviderWallet(x.Id, x.Currency.Trim().ToUpperInvariant(), false, x.Amount, x.IsActive)).ToList();
        }
        var wallets = await api.ListCryptoWalletsAsync(ct);
        return wallets.Data.Data.Select(x => new DiscoveredProviderWallet(x.Id, x.Asset.Symbol.Trim().ToUpperInvariant(), true,
            decimal.Parse(x.Balance, NumberStyles.Number, CultureInfo.InvariantCulture), x.IsActive)).ToList();
    }

    public async Task<ProviderWalletConfigurationDto> RegisterAsync(Guid actor, RegisterProviderWalletRequest request, CancellationToken ct)
    {
        var reason = Required(request.Reason, 500, "Reason");
        var asset = Required(request.AssetCode, 20, "Asset").ToUpperInvariant();
        var network = (request.NetworkCode ?? "").Trim().ToUpperInvariant();
        await ValidateAssetAsync(asset, network, ct);
        var wallet = new ProviderWalletConfiguration
        {
            Environment = resolver.EnvironmentName,
            ProviderWalletId = Required(request.ProviderWalletId, 150, "Provider wallet ID"),
            AssetCode = asset, NetworkCode = network,
            DisplayName = Required(request.DisplayName, 120, "Display name"), CreatedByUserId = actor
        };
        db.ProviderWalletConfigurations.Add(wallet);
        Record("REGISTERED", wallet, actor, reason);
        await SaveAsync(ct);
        return ToDto(wallet);
    }

    public async Task<ProviderWalletConfigurationDto> VerifyAsync(Guid id, Guid actor, VerifyProviderWalletRequest request, CancellationToken ct)
    {
        var reason = Required(request.Reason, 500, "Reason");
        var wallet = await GetAsync(id, request.Revision, ct);
        var isCrypto = await ValidateAssetAsync(wallet.AssetCode, wallet.NetworkCode, ct);
        var discovered = await DiscoverAsync(isCrypto, ct);
        var match = discovered.SingleOrDefault(x => x.ProviderWalletId == wallet.ProviderWalletId && x.AssetCode == wallet.AssetCode);
        var old = ToDto(wallet);
        // A provider response that no longer contains an active matching wallet revokes its
        // verification and use. An unavailable provider leaves the last observation intact.
        if (match is null || !match.IsActive)
        {
            wallet.VerifiedAt = null;
            wallet.VerifiedConnectionKey = null;
            wallet.IsActive = false;
            wallet.DefaultForCollection = wallet.DefaultForPayout = false;
        }
        else
        {
            wallet.VerifiedAt = DateTime.UtcNow;
            wallet.VerifiedConnectionKey = resolver.ConnectionKey;
            wallet.LastProviderBalance = match.Balance;
        }
        Touch(wallet, actor);
        Record(match is null || !match.IsActive ? "VERIFICATION_REJECTED" : "VERIFIED", wallet, actor, reason, old);
        await SaveAsync(ct);
        return ToDto(wallet);
    }

    public async Task<ProviderWalletConfigurationDto> ConfigureAsync(Guid id, Guid actor, ConfigureProviderWalletRequest request, CancellationToken ct)
    {
        var reason = Required(request.Reason, 500, "Reason");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var wallet = await GetAsync(id, request.Revision, ct);
        var old = ToDto(wallet);
        if (request.IsActive) await ValidateAssetAsync(wallet.AssetCode, wallet.NetworkCode, ct);
        if (request.IsActive && (wallet.VerifiedAt is null || wallet.VerifiedConnectionKey != resolver.ConnectionKey))
            throw new InvalidOperationException("Verify this wallet against the current provider connection before activating it.");
        if (request.IsActive && !request.CollectionEnabled && !request.PayoutEnabled)
            throw new InvalidOperationException("Select collection, payout or both before activating the wallet.");
        if (request.DefaultForCollection && (!request.IsActive || !request.CollectionEnabled) ||
            request.DefaultForPayout && (!request.IsActive || !request.PayoutEnabled))
            throw new InvalidOperationException("A default wallet must be active and enabled for the selected operation.");
        if (request.DefaultForCollection || request.DefaultForPayout)
        {
            var priorDefaults = await db.ProviderWalletConfigurations.Where(x => x.Id != id && !x.IsDeleted &&
                x.Environment == wallet.Environment && x.ProviderCode == wallet.ProviderCode &&
                x.AssetCode == wallet.AssetCode && x.NetworkCode == wallet.NetworkCode &&
                (request.DefaultForCollection && x.DefaultForCollection || request.DefaultForPayout && x.DefaultForPayout)).ToListAsync(ct);
            foreach (var previous in priorDefaults)
            {
                var prior = ToDto(previous);
                if (request.DefaultForCollection) previous.DefaultForCollection = false;
                if (request.DefaultForPayout) previous.DefaultForPayout = false;
                Touch(previous, actor);
                Record("DEFAULT_REPLACED", previous, actor, reason, prior);
            }
            // Clear old unique defaults inside the same transaction before setting the new one.
            await SaveAsync(ct);
        }
        wallet.DisplayName = Required(request.DisplayName, 120, "Display name");
        wallet.CollectionEnabled = request.CollectionEnabled;
        wallet.PayoutEnabled = request.PayoutEnabled;
        wallet.IsActive = request.IsActive;
        wallet.DefaultForCollection = request.DefaultForCollection;
        wallet.DefaultForPayout = request.DefaultForPayout;
        Touch(wallet, actor);
        Record("CONFIGURED", wallet, actor, reason, old);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(wallet);
    }

    public async Task<object> ImportLegacyAsync(Guid actor, string requestReason, CancellationToken ct)
    {
        var reason = Required(requestReason, 500, "Reason");
        var imported = 0;
        var skipped = new List<string>();
        var mappings = options.Value.CollectionWalletIds.Concat(options.Value.PayoutWalletIds)
            .Concat(options.Value.CryptoWalletIds).Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => (Asset: x.Key.Trim().ToUpperInvariant(), Wallet: x.Value.Trim())).Distinct().ToList();
        // Import is explicit and creates inactive, unverified records. It never activates rails
        // or replaces existing registrations and defaults.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var mapping in mappings)
        {
            var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Code == mapping.Asset && x.IsSupported, ct);
            if (asset is null) { skipped.Add(mapping.Asset + ": asset is not configured"); continue; }
            var networks = asset.Type == AssetType.Crypto
                ? await db.AssetNetworks.AsNoTracking().Where(x => x.AssetCode == asset.Code).Select(x => x.NetworkCode).ToListAsync(ct)
                : new List<string> { "" };
            foreach (var network in networks)
            {
                try { await ValidateAssetAsync(asset.Code, network, ct); }
                catch (InvalidOperationException) { skipped.Add(asset.Code + "/" + network + ": unsupported route"); continue; }
                if (await db.ProviderWalletConfigurations.AnyAsync(x => x.ProviderCode == "BLAAIZ" &&
                    x.Environment == resolver.EnvironmentName && x.AssetCode == asset.Code && x.NetworkCode == network &&
                    x.ProviderWalletId == mapping.Wallet, ct)) continue;
                var wallet = new ProviderWalletConfiguration
                {
                    Environment = resolver.EnvironmentName, AssetCode = asset.Code, NetworkCode = network,
                    ProviderWalletId = Required(mapping.Wallet, 150, "Provider wallet ID"),
                    DisplayName = asset.Code + " imported wallet", CreatedByUserId = actor
                };
                db.ProviderWalletConfigurations.Add(wallet);
                Record("LEGACY_IMPORTED", wallet, actor, reason);
                imported++;
            }
        }
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return new { imported, skipped };
    }

    private async Task<bool> ValidateAssetAsync(string assetCode, string networkCode, CancellationToken ct)
    {
        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(x => x.Code == assetCode && x.IsSupported, ct)
            ?? throw new InvalidOperationException("Select a supported asset from master data.");
        if (networkCode.Length > 50) throw new InvalidOperationException("Network code is too long.");
        if (asset.Type == AssetType.Fiat)
        {
            if (networkCode.Length != 0) throw new InvalidOperationException("Fiat wallets do not use a crypto network.");
            return false;
        }
        if (asset.Code is not ("USDT" or "USDC"))
            throw new InvalidOperationException("The Blaaiz crypto adapter currently supports USDT and USDC.");
        if (!await db.AssetNetworks.AnyAsync(x => x.AssetCode == assetCode && x.NetworkCode == networkCode, ct))
            throw new InvalidOperationException("Select a configured network for this asset.");
        if (!options.Value.CryptoNetworkMappings.TryGetValue(networkCode, out var mapped) || string.IsNullOrWhiteSpace(mapped))
            throw new InvalidOperationException("This network does not have a supported Blaaiz network mapping.");
        return true;
    }

    private async Task<ProviderWalletConfiguration> GetAsync(Guid id, Guid revision, CancellationToken ct)
    {
        var wallet = await db.ProviderWalletConfigurations.SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted &&
            x.ProviderCode == "BLAAIZ" && x.Environment == resolver.EnvironmentName, ct)
            ?? throw new InvalidOperationException("Provider wallet not found in this environment.");
        if (revision != wallet.Revision) throw new DbUpdateConcurrencyException("Refresh the wallet before changing it.");
        return wallet;
    }

    private ProviderWalletConfigurationDto ToDto(ProviderWalletConfiguration x) => new(x.Id, x.ProviderCode, x.Environment,
        x.ProviderWalletId, x.AssetCode, x.NetworkCode, x.DisplayName, x.CollectionEnabled, x.PayoutEnabled,
        x.DefaultForCollection, x.DefaultForPayout, x.IsActive, x.VerifiedAt.HasValue && x.VerifiedConnectionKey == resolver.ConnectionKey,
        x.VerifiedAt, x.LastProviderBalance, x.Revision);

    private void Record(string action, ProviderWalletConfiguration wallet, Guid actor, string reason, object? old = null) =>
        audit.Stage(new AuditRecordRequest("PROVIDER_WALLET_" + action, "Treasury", nameof(ProviderWalletConfiguration),
            wallet.Id.ToString(), OldValues: old, NewValues: ToDto(wallet), Metadata: new { Reason = reason }, UserId: actor));

    private static void Touch(ProviderWalletConfiguration wallet, Guid actor)
    {
        wallet.LastUpdatedAt = DateTime.UtcNow;
        wallet.LastUpdatedByUserId = actor;
        wallet.Revision = Guid.NewGuid();
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new DbUpdateConcurrencyException("This wallet or default route already exists. Refresh and try again.", ex); }
    }

    private static string Required(string? value, int length, string label)
    {
        var result = value?.Trim();
        if (string.IsNullOrEmpty(result) || result.Length > length) throw new InvalidOperationException($"{label} is required and must be at most {length} characters.");
        return result;
    }
}
