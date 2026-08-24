using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Models.Treasury;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Services.Audit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.DigitalAssets;

public sealed class DigitalAssetProviderOperationsService :
    IDigitalAssetProviderOperationsService
{
    private readonly AppDbContext _db;
    private readonly IDigitalAssetProviderRegistry _registry;
    private readonly IDataProtector _protector;
    private readonly IAuditService? _audit;

    public DigitalAssetProviderOperationsService(
        AppDbContext db,
        IDigitalAssetProviderRegistry registry,
        IDataProtectionProvider dataProtection,
        IAuditService? audit = null)
    {
        _db = db;
        _registry = registry;
        _protector = dataProtection.CreateProtector(
            "KorridorX.DigitalAssets.ProviderConfiguration.v1");
        _audit = audit;
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
        var reason = RequiredReason(request.Reason);
        var providerCode = Required(request.ProviderCode, 50).ToUpperInvariant();

        // Ensure a runtime adapter exists for this provider code.
        _registry.GetRequired(providerCode);

        var config = await _db.DigitalAssetProviderConfigurations
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == providerCode &&
                !x.IsDeleted,
                ct);

        var oldValues = config is null
            ? null
            : new
            {
                config.DisplayName,
                config.BaseUrl,
                config.WebhookSecretLastFour,
                config.IsActive,
                config.WebhooksEnabled,
                config.BalanceSyncEnabled,
                config.MetadataJson
            };

        var created = config is null;

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

        var webhookSecretChanged = false;

        if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
        {
            var secret = request.WebhookSecret.Trim();
            config.WebhookSecretProtected = _protector.Protect(secret);
            config.WebhookSecretLastFour =
                secret.Length <= 4 ? secret : secret[^4..];
            webhookSecretChanged = true;
        }

        _audit?.Stage(new AuditRecordRequest(
            Action: created
                ? "DIGITAL_ASSET_PROVIDER_CONFIGURATION_CREATED"
                : "DIGITAL_ASSET_PROVIDER_CONFIGURATION_UPDATED",
            Category: "DigitalAssets",
            EntityName: nameof(DigitalAssetProviderConfiguration),
            EntityId: config.Id.ToString(),
            OldValues: oldValues,
            NewValues: new
            {
                config.ProviderCode,
                config.DisplayName,
                config.BaseUrl,
                config.WebhookSecretLastFour,
                config.IsActive,
                config.WebhooksEnabled,
                config.BalanceSyncEnabled,
                config.MetadataJson
            },
            Metadata: new
            {
                Reason = reason,
                WebhookSecretChanged = webhookSecretChanged
            },
            UserId: userId));

        await _db.SaveChangesAsync(ct);

        return (await GetConfigurationsAsync(ct))
            .Single(x => x.Id == config.Id);
    }

    public Task<DigitalAssetProviderHealthDto> CheckHealthAsync(
        string providerCode,
        CancellationToken ct = default) =>
        CheckHealthCoreAsync(
            providerCode,
            userId: null,
            reason: null,
            auditOperatorAction: false,
            ct);

    public Task<DigitalAssetProviderHealthDto> CheckHealthAsync(
        string providerCode,
        Guid userId,
        string reason,
        CancellationToken ct = default) =>
        CheckHealthCoreAsync(
            providerCode,
            userId,
            RequiredReason(reason),
            auditOperatorAction: true,
            ct);

    private async Task<DigitalAssetProviderHealthDto> CheckHealthCoreAsync(
        string providerCode,
        Guid? userId,
        string? reason,
        bool auditOperatorAction,
        CancellationToken ct)
    {
        var config = await GetActiveConfigurationAsync(providerCode, ct);
        var provider = _registry.GetRequired(config.ProviderCode);

        if (provider is not IDigitalAssetHealthProvider health)
            throw new InvalidOperationException(
                $"Digital-asset provider '{config.ProviderCode}' does not expose health checks.");

        var old = new
        {
            config.LastHealthCheckAt,
            config.LastHealthCheckSucceeded,
            config.LastHealthCheckMessage
        };

        var result = await health.CheckHealthAsync(ct);
        var now = DateTime.UtcNow;

        config.LastHealthCheckAt = now;
        config.LastHealthCheckSucceeded = result.IsHealthy;
        config.LastHealthCheckMessage = Clean(result.Message, 1000);
        config.LastUpdatedAt = now;

        if (auditOperatorAction)
        {
            _audit?.Stage(new AuditRecordRequest(
                Action: "DIGITAL_ASSET_PROVIDER_HEALTH_CHECK",
                Category: "DigitalAssets",
                EntityName: nameof(DigitalAssetProviderConfiguration),
                EntityId: config.Id.ToString(),
                OldValues: old,
                NewValues: new
                {
                    config.LastHealthCheckAt,
                    config.LastHealthCheckSucceeded,
                    config.LastHealthCheckMessage
                },
                Metadata: new
                {
                    ProviderCode = config.ProviderCode,
                    Reason = reason
                },
                UserId: userId));
        }

        await _db.SaveChangesAsync(ct);

        return new DigitalAssetProviderHealthDto
        {
            ProviderCode = config.ProviderCode,
            IsHealthy = result.IsHealthy,
            Message = result.Message,
            CheckedAt = now
        };
    }

    public Task<DigitalAssetProviderSyncResultDto> SyncBalancesAsync(
        string providerCode,
        CancellationToken ct = default) =>
        SyncBalancesCoreAsync(
            providerCode,
            userId: null,
            reason: null,
            auditOperatorAction: false,
            ct);

    public Task<DigitalAssetProviderSyncResultDto> SyncBalancesAsync(
        string providerCode,
        Guid userId,
        string reason,
        CancellationToken ct = default) =>
        SyncBalancesCoreAsync(
            providerCode,
            userId,
            RequiredReason(reason),
            auditOperatorAction: true,
            ct);

    private async Task<DigitalAssetProviderSyncResultDto> SyncBalancesCoreAsync(
        string providerCode,
        Guid? userId,
        string? reason,
        bool auditOperatorAction,
        CancellationToken ct)
    {
        var config = await GetActiveConfigurationAsync(providerCode, ct);

        if (!config.BalanceSyncEnabled)
            throw new InvalidOperationException(
                "Balance synchronization is disabled for this provider.");

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

        if (auditOperatorAction)
        {
            _audit?.Stage(new AuditRecordRequest(
                Action: "DIGITAL_ASSET_PROVIDER_BALANCES_SYNCED",
                Category: "DigitalAssets",
                EntityName: nameof(DigitalAssetProviderConfiguration),
                EntityId: config.Id.ToString(),
                NewValues: new
                {
                    ProviderCode = config.ProviderCode,
                    BalanceCount = snapshots.Count,
                    SyncedAt = now
                },
                Metadata: new
                {
                    Reason = reason
                },
                UserId: userId));
        }

        await _db.SaveChangesAsync(ct);

        return new DigitalAssetProviderSyncResultDto
        {
            ProviderCode = config.ProviderCode,
            BalanceCount = snapshots.Count,
            SyncedAt = now
        };
    }

    public async Task<PagedResult<DigitalAssetProviderBalanceDto>> GetBalancesAsync(
        string? providerCode,
        string? assetCode,
        string? networkCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ProviderWalletBalances
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            var normalized = providerCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.ProviderCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(assetCode))
        {
            var normalized = assetCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.CurrencyCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(networkCode))
        {
            var normalized = networkCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.NetworkCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.ProviderCode.ToLower().Contains(value) ||
                x.ProviderWalletId.ToLower().Contains(value) ||
                x.CurrencyCode.ToLower().Contains(value) ||
                (x.NetworkCode != null &&
                 x.NetworkCode.ToLower().Contains(value)));
        }

        return await query
            .OrderBy(x => x.ProviderCode)
            .ThenBy(x => x.CurrencyCode)
            .ThenBy(x => x.NetworkCode)
            .ThenBy(x => x.ProviderWalletId)
            .Select(x => new DigitalAssetProviderBalanceDto
            {
                Id = x.Id,
                ProviderCode = x.ProviderCode,
                ProviderWalletId = x.ProviderWalletId,
                AssetCode = x.CurrencyCode,
                NetworkCode = x.NetworkCode,
                AssetNetworkId = x.AssetNetworkId,
                Balance = x.Balance,
                IsActive = x.IsActive,
                LastSyncedAt = x.LastSyncedAt,
                LastUpdatedAt = x.LastUpdatedAt
            })
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<PagedResult<DigitalAssetNetworkTransactionDto>>
        GetNetworkTransactionsAsync(
            string? providerCode,
            string? assetCode,
            string? networkCode,
            DigitalAssetTransactionDirection? direction,
            DigitalAssetTransactionStatus? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
    {
        var query = _db.DigitalAssetNetworkTransactions
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            var normalized = providerCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.ProviderCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(assetCode))
        {
            var normalized = assetCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.AssetCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(networkCode))
        {
            var normalized = networkCode.Trim().ToUpperInvariant();
            query = query.Where(
                x => x.AssetNetwork.NetworkCode == normalized);
        }

        if (direction.HasValue)
            query = query.Where(x => x.Direction == direction.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.ProviderTransactionId.ToLower().Contains(value) ||
                (x.ProviderReference != null &&
                 x.ProviderReference.ToLower().Contains(value)) ||
                (x.TransactionHash != null &&
                 x.TransactionHash.ToLower().Contains(value)) ||
                (x.FromAddress != null &&
                 x.FromAddress.ToLower().Contains(value)) ||
                (x.ToAddress != null &&
                 x.ToAddress.ToLower().Contains(value)));
        }

        return await query
            .OrderByDescending(x => x.ObservedAt)
            .Select(ToNetworkTransactionDto())
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<DigitalAssetNetworkTransactionDto>
        GetNetworkTransactionAsync(
            Guid transactionId,
            CancellationToken ct = default)
    {
        var item = await _db.DigitalAssetNetworkTransactions
            .AsNoTracking()
            .Where(x => x.Id == transactionId && !x.IsDeleted)
            .Select(ToNetworkTransactionDto())
            .FirstOrDefaultAsync(ct);

        return item
            ?? throw new InvalidOperationException(
                "Digital-asset network transaction not found.");
    }

    public async Task<PagedResult<DigitalAssetWebhookReceiptDto>>
        GetWebhookReceiptsAsync(
            string? providerCode,
            DigitalAssetWebhookReceiptStatus? status,
            string? eventType,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
    {
        var query = _db.DigitalAssetWebhookReceipts
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(providerCode))
        {
            var normalized = providerCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.ProviderCode == normalized);
        }

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalized = eventType.Trim();
            query = query.Where(x => x.EventType == normalized);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.ProviderEventId.ToLower().Contains(value) ||
                (x.EventType != null &&
                 x.EventType.ToLower().Contains(value)) ||
                (x.ErrorMessage != null &&
                 x.ErrorMessage.ToLower().Contains(value)));
        }

        return await query
            .OrderByDescending(x => x.ReceivedAt)
            .Select(ToWebhookReceiptDto())
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<DigitalAssetWebhookReceiptDto> GetWebhookReceiptAsync(
        Guid webhookReceiptId,
        CancellationToken ct = default)
    {
        var item = await _db.DigitalAssetWebhookReceipts
            .AsNoTracking()
            .Where(x => x.Id == webhookReceiptId && !x.IsDeleted)
            .Select(ToWebhookReceiptDto())
            .FirstOrDefaultAsync(ct);

        return item
            ?? throw new InvalidOperationException(
                "Digital-asset webhook receipt not found.");
    }

    public async Task<IReadOnlyList<DigitalAssetNetworkTransactionDto>>
        GetReconciliationExceptionsAsync(
            int take = 100,
            CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);

        return await _db.DigitalAssetNetworkTransactions
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (
                    x.Status == DigitalAssetTransactionStatus.Failed ||
                    (
                        x.Direction == DigitalAssetTransactionDirection.Inbound &&
                        x.Status == DigitalAssetTransactionStatus.Confirmed &&
                        (!x.CollectionId.HasValue ||
                         !x.LedgerTransactionId.HasValue)
                    ) ||
                    (
                        x.Direction == DigitalAssetTransactionDirection.Outbound &&
                        x.Status == DigitalAssetTransactionStatus.Confirmed &&
                        !x.PayoutId.HasValue
                    )
                ))
            .OrderByDescending(x => x.LastUpdatedAt ?? x.CreatedAt)
            .Take(take)
            .Select(ToNetworkTransactionDto())
            .ToListAsync(ct);
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
                (
                    x.NetworkFeeVariance.Value >= minimumAbsoluteVariance ||
                    x.NetworkFeeVariance.Value <= -minimumAbsoluteVariance
                ))
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
            throw new InvalidOperationException(
                "Digital-asset provider webhook secret is not configured.");

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

    private static System.Linq.Expressions.Expression<
        Func<DigitalAssetNetworkTransaction, DigitalAssetNetworkTransactionDto>>
        ToNetworkTransactionDto() =>
            x => new DigitalAssetNetworkTransactionDto
            {
                Id = x.Id,
                ProviderCode = x.ProviderCode,
                ProviderTransactionId = x.ProviderTransactionId,
                ProviderReference = x.ProviderReference,
                AssetNetworkId = x.AssetNetworkId,
                AssetCode = x.AssetCode,
                NetworkCode = x.AssetNetwork.NetworkCode,
                Direction = x.Direction,
                Status = x.Status,
                TransactionHash = x.TransactionHash,
                FromAddress = x.FromAddress,
                ToAddress = x.ToAddress,
                DestinationTag = x.DestinationTag,
                Amount = x.Amount,
                NetworkFee = x.NetworkFee,
                Confirmations = x.Confirmations,
                RequiredConfirmations = x.RequiredConfirmations,
                BlockNumber = x.BlockNumber,
                CollectionId = x.CollectionId,
                PayoutId = x.PayoutId,
                LedgerTransactionId = x.LedgerTransactionId,
                ObservedAt = x.ObservedAt,
                ConfirmedAt = x.ConfirmedAt,
                FailedAt = x.FailedAt,
                CreatedAt = x.CreatedAt,
                LastUpdatedAt = x.LastUpdatedAt
            };

    private static System.Linq.Expressions.Expression<
        Func<DigitalAssetWebhookReceipt, DigitalAssetWebhookReceiptDto>>
        ToWebhookReceiptDto() =>
            x => new DigitalAssetWebhookReceiptDto
            {
                Id = x.Id,
                ProviderCode = x.ProviderCode,
                ProviderEventId = x.ProviderEventId,
                Status = x.Status,
                EventType = x.EventType,
                ErrorMessage = x.ErrorMessage,
                ReceivedAt = x.ReceivedAt,
                ProcessedAt = x.ProcessedAt,
                CreatedAt = x.CreatedAt,
                LastUpdatedAt = x.LastUpdatedAt
            };

    private static string RequiredReason(string? value)
    {
        var clean = Clean(value, 1000);

        return string.IsNullOrWhiteSpace(clean)
            ? throw new InvalidOperationException(
                "A reason is required for this digital-asset administrative action.")
            : clean;
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