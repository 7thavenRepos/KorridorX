using KorridorX.Data;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Treasury;
using KorridorX.Providers.DigitalAssets;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.DigitalAssets;

public sealed class DigitalAssetProviderOperationsService :
    IDigitalAssetProviderOperationsService
{
    private readonly AppDbContext _db;
    private readonly IDigitalAssetProviderRegistry _registry;
    private readonly IDataProtector _protector;

    public DigitalAssetProviderOperationsService(
        AppDbContext db,
        IDigitalAssetProviderRegistry registry,
        IDataProtectionProvider dataProtection)
    {
        _db = db;
        _registry = registry;
        _protector = dataProtection.CreateProtector(
            "KorridorX.DigitalAssets.ProviderConfiguration.v1");
    }

    public async Task<IReadOnlyList<DigitalAssetProviderConfigurationDto>>
        GetConfigurationsAsync(CancellationToken ct = default) =>
        await _db.DigitalAssetProviderConfigurations.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.ProviderCode)
            .Select(x => new DigitalAssetProviderConfigurationDto
            {
                Id = x.Id,
                ProviderCode = x.ProviderCode,
                DisplayName = x.DisplayName,
                BaseUrl = x.BaseUrl,
                WebhookSecretLastFour = x.WebhookSecretLastFour,
                IsActive = x.IsActive,
                WebhooksEnabled = x.WebhooksEnabled,
                BalanceSyncEnabled = x.BalanceSyncEnabled,
                LastHealthCheckAt = x.LastHealthCheckAt,
                LastHealthCheckSucceeded = x.LastHealthCheckSucceeded,
                LastHealthCheckMessage = x.LastHealthCheckMessage
            })
            .ToListAsync(ct);

    public async Task<DigitalAssetProviderConfigurationDto> UpsertConfigurationAsync(
        Guid userId,
        UpsertDigitalAssetProviderConfigurationRequestDto request,
        CancellationToken ct = default)
    {
        var providerCode = Required(request.ProviderCode, 50).ToUpperInvariant();

        // Ensure a runtime adapter exists for this provider code.
        _registry.GetRequired(providerCode);

        var config = await _db.DigitalAssetProviderConfigurations
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == providerCode &&
                !x.IsDeleted,
                ct);

        if (config is null)
        {
            config = new DigitalAssetProviderConfiguration
            {
                ProviderCode = providerCode,
                CreatedByUserId = userId
            };
            _db.DigitalAssetProviderConfigurations.Add(config);
        }

        config.DisplayName = Required(request.DisplayName, 120);
        config.BaseUrl = Clean(request.BaseUrl, 1000);
        config.MetadataJson = request.MetadataJson;
        config.IsActive = request.IsActive;
        config.WebhooksEnabled = request.WebhooksEnabled;
        config.BalanceSyncEnabled = request.BalanceSyncEnabled;
        config.LastUpdatedAt = DateTime.UtcNow;
        config.LastUpdatedByUserId = userId;

        if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
        {
            var secret = request.WebhookSecret.Trim();
            config.WebhookSecretProtected = _protector.Protect(secret);
            config.WebhookSecretLastFour =
                secret.Length <= 4 ? secret : secret[^4..];
        }

        await _db.SaveChangesAsync(ct);

        return (await GetConfigurationsAsync(ct))
            .Single(x => x.Id == config.Id);
    }

    public async Task<DigitalAssetProviderHealthDto> CheckHealthAsync(
        string providerCode,
        CancellationToken ct = default)
    {
        var config = await GetActiveConfigurationAsync(providerCode, ct);
        var provider = _registry.GetRequired(config.ProviderCode);

        if (provider is not IDigitalAssetHealthProvider health)
            throw new InvalidOperationException(
                $"Digital-asset provider '{config.ProviderCode}' does not expose health checks.");

        var result = await health.CheckHealthAsync(ct);
        var now = DateTime.UtcNow;

        config.LastHealthCheckAt = now;
        config.LastHealthCheckSucceeded = result.IsHealthy;
        config.LastHealthCheckMessage = Clean(result.Message, 1000);
        config.LastUpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return new DigitalAssetProviderHealthDto
        {
            ProviderCode = config.ProviderCode,
            IsHealthy = result.IsHealthy,
            Message = result.Message,
            CheckedAt = now
        };
    }

    public async Task<DigitalAssetProviderSyncResultDto> SyncBalancesAsync(
        string providerCode,
        CancellationToken ct = default)
    {
        var config = await GetActiveConfigurationAsync(providerCode, ct);
        if (!config.BalanceSyncEnabled)
            throw new InvalidOperationException("Balance synchronization is disabled for this provider.");

        var provider = _registry.GetRequired(config.ProviderCode);
        if (provider is not IDigitalAssetBalanceProvider balances)
            throw new InvalidOperationException(
                $"Digital-asset provider '{config.ProviderCode}' does not expose balance snapshots.");

        var snapshots = await balances.GetBalancesAsync(ct);
        var now = DateTime.UtcNow;

        foreach (var snapshot in snapshots)
        {
            var assetCode = Required(snapshot.AssetCode, 20).ToUpperInvariant();
            var networkCode = Clean(snapshot.NetworkCode, 50)?.ToUpperInvariant();

            Guid? assetNetworkId = null;
            if (!string.IsNullOrWhiteSpace(networkCode))
            {
                assetNetworkId = await _db.AssetNetworks.AsNoTracking()
                    .Where(x =>
                        x.AssetCode == assetCode &&
                        x.NetworkCode == networkCode)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefaultAsync(ct);
            }

            var wallet = await _db.ProviderWalletBalances.FirstOrDefaultAsync(x =>
                x.ProviderCode == config.ProviderCode &&
                x.ProviderWalletId == snapshot.ProviderWalletId &&
                x.CurrencyCode == assetCode &&
                x.NetworkCode == networkCode &&
                !x.IsDeleted,
                ct);

            if (wallet is null)
            {
                wallet = new ProviderWalletBalance
                {
                    ProviderCode = config.ProviderCode,
                    ProviderWalletId = Required(snapshot.ProviderWalletId, 150),
                    CurrencyCode = assetCode,
                    NetworkCode = networkCode
                };
                _db.ProviderWalletBalances.Add(wallet);
            }

            wallet.AssetNetworkId = assetNetworkId;
            wallet.Balance = snapshot.Balance;
            wallet.IsActive = snapshot.IsActive;
            wallet.LastSyncedAt = now;
            wallet.LastUpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        return new DigitalAssetProviderSyncResultDto
        {
            ProviderCode = config.ProviderCode,
            BalanceCount = snapshots.Count,
            SyncedAt = now
        };
    }

    public async Task<IReadOnlyList<DigitalAssetFeeExceptionDto>> GetFeeExceptionsAsync(
        decimal minimumAbsoluteVariance = 0.00000001m,
        int take = 100,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        minimumAbsoluteVariance = Math.Abs(minimumAbsoluteVariance);

        var rows = await _db.DigitalAssetWithdrawals.AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.ActualNetworkFee.HasValue &&
                x.NetworkFeeVariance.HasValue &&
                (x.NetworkFeeVariance.Value >= minimumAbsoluteVariance ||
                 x.NetworkFeeVariance.Value <= -minimumAbsoluteVariance))
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(x => new DigitalAssetFeeExceptionDto
        {
            WithdrawalId = x.Id,
            AssetCode = x.AssetCode,
            ProviderCode = x.ProviderCode,
            QuotedNetworkFee = x.NetworkFee,
            ActualNetworkFee = x.ActualNetworkFee!.Value,
            Variance = x.NetworkFeeVariance!.Value,
            CreatedAt = x.CreatedAt
        }).ToList();
    }

    internal string UnprotectWebhookSecret(
        DigitalAssetProviderConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.WebhookSecretProtected))
            throw new InvalidOperationException("Digital-asset provider webhook secret is not configured.");

        return _protector.Unprotect(config.WebhookSecretProtected);
    }

    internal async Task<DigitalAssetProviderConfiguration> GetActiveConfigurationAsync(
        string providerCode,
        CancellationToken ct)
    {
        var normalized = Required(providerCode, 50).ToUpperInvariant();

        return await _db.DigitalAssetProviderConfigurations
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == normalized &&
                x.IsActive &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                $"Active digital-asset provider configuration '{normalized}' was not found.");
    }

    private static string Required(string? value, int max)
    {
        var clean = Clean(value, max);
        return string.IsNullOrWhiteSpace(clean)
            ? throw new InvalidOperationException("Required value is missing.")
            : clean;
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim();
        return clean.Length <= max ? clean : clean[..max];
    }
}
