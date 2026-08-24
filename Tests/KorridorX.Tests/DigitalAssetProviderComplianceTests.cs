using KorridorX.Data;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Services.DigitalAssets;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class DigitalAssetProviderComplianceTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public DigitalAssetProviderComplianceTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Safe_address_risk_assessment_is_cached_and_provider_is_not_called_twice()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var network = await CreateNetworkAsync(db, "RSC");
        var riskProvider = new FakeAddressRiskProvider(
            new DigitalAssetAddressRiskResult(
                0.20m,
                DigitalAssetAddressRiskLevel.Low,
                false,
                new[] { "No material risk indicators." },
                "risk-safe",
                "{\"risk\":\"low\"}",
                DateTime.UtcNow.AddHours(1)));

        var gate = new DigitalAssetComplianceGate(
            db,
            new[] { riskProvider },
            Options.Create(new DigitalAssetComplianceOptions
            {
                RequireAddressScreening = true,
                BlockingRiskScore = 0.80m,
                AddressRiskCacheMinutes = 60
            }));

        var businessProfileId = Guid.NewGuid();
        var businessCustomerId = Guid.NewGuid();
        var address = $"0x{Guid.NewGuid():N}";

        await gate.EnsureWithdrawalAllowedAsync(
            businessProfileId,
            businessCustomerId,
            network.AssetCode,
            network.NetworkCode,
            25m,
            address);

        db.ChangeTracker.Clear();

        await gate.EnsureWithdrawalAllowedAsync(
            businessProfileId,
            businessCustomerId,
            network.AssetCode,
            network.NetworkCode,
            25m,
            address);

        Assert.Equal(1, riskProvider.ScreenCalls);

        var assessment = await db.DigitalAssetAddressRiskAssessments.AsNoTracking()
            .SingleAsync(x =>
                x.BusinessProfileId == businessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                x.AssetNetworkId == network.NetworkId &&
                x.Address == address);

        Assert.False(assessment.IsBlocking);
        Assert.Equal(0.20m, assessment.RiskScore);
        Assert.Equal(DigitalAssetAddressRiskLevel.Low, assessment.RiskLevel);
        Assert.Equal(riskProvider.RiskProviderCode, assessment.ProviderCode);
    }

    [DatabaseIntegrationFact]
    public async Task Blocking_address_risk_is_persisted_and_cached_block_remains_enforced()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var network = await CreateNetworkAsync(db, "RSB");
        var riskProvider = new FakeAddressRiskProvider(
            new DigitalAssetAddressRiskResult(
                0.95m,
                DigitalAssetAddressRiskLevel.High,
                false,
                new[] { "Exposure to a high-risk address cluster." },
                "risk-block",
                "{\"risk\":\"high\"}",
                DateTime.UtcNow.AddHours(1)));

        var gate = new DigitalAssetComplianceGate(
            db,
            new[] { riskProvider },
            Options.Create(new DigitalAssetComplianceOptions
            {
                RequireAddressScreening = true,
                BlockingRiskScore = 0.80m,
                AddressRiskCacheMinutes = 60
            }));

        var businessProfileId = Guid.NewGuid();
        var businessCustomerId = Guid.NewGuid();
        var address = $"0x{Guid.NewGuid():N}";

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.EnsureWithdrawalAllowedAsync(
                businessProfileId,
                businessCustomerId,
                network.AssetCode,
                network.NetworkCode,
                25m,
                address));

        Assert.Contains("failed compliance screening", first.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();

        var assessment = await db.DigitalAssetAddressRiskAssessments.AsNoTracking()
            .SingleAsync(x =>
                x.BusinessProfileId == businessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                x.AssetNetworkId == network.NetworkId &&
                x.Address == address);

        Assert.True(assessment.IsBlocking);
        Assert.Equal(0.95m, assessment.RiskScore);

        var second = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.EnsureWithdrawalAllowedAsync(
                businessProfileId,
                businessCustomerId,
                network.AssetCode,
                network.NetworkCode,
                25m,
                address));

        Assert.Contains("failed compliance screening", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, riskProvider.ScreenCalls);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_configuration_health_and_balance_sync_persist_operational_state()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var network = await CreateNetworkAsync(db, "OPS");
        var providerCode = UniqueProviderCode("OPS");
        var walletId = $"wallet-{Guid.NewGuid():N}";

        var provider = new FakeCapabilityProvider(providerCode)
        {
            HealthResult = new DigitalAssetProviderHealthResult(true, "healthy"),
            BalanceSnapshots = new[]
            {
                new DigitalAssetProviderBalanceSnapshot(
                    walletId,
                    network.AssetCode.ToLowerInvariant(),
                    network.NetworkCode.ToLowerInvariant(),
                    125.50m,
                    true,
                    "balance-1")
            }
        };

        var registry = new DigitalAssetProviderRegistry(new[] { provider });
        var dataProtection = new EphemeralDataProtectionProvider();
        var service = new DigitalAssetProviderOperationsService(
            db,
            registry,
            dataProtection);

        var userId = Guid.NewGuid();
        var secret = "slice12a-webhook-secret-1234";

        var configuration = await service.UpsertConfigurationAsync(
            userId,
            new UpsertDigitalAssetProviderConfigurationRequestDto
            {
                ProviderCode = providerCode.ToLowerInvariant(),
                DisplayName = "Slice 12A Test Provider",
                Reason = "Provider configuration integration test.",
                BaseUrl = "https://provider.example.test",
                WebhookSecret = secret,
                IsActive = true,
                WebhooksEnabled = true,
                BalanceSyncEnabled = true
            });

        Assert.Equal(providerCode, configuration.ProviderCode);
        Assert.Equal("1234", configuration.WebhookSecretLastFour);

        db.ChangeTracker.Clear();

        var storedConfiguration = await db.DigitalAssetProviderConfigurations.AsNoTracking()
            .SingleAsync(x => x.Id == configuration.Id);

        Assert.NotNull(storedConfiguration.WebhookSecretProtected);
        Assert.NotEqual(secret, storedConfiguration.WebhookSecretProtected);

        var health = await service.CheckHealthAsync(providerCode);

        Assert.True(health.IsHealthy);
        Assert.Equal("healthy", health.Message);

        db.ChangeTracker.Clear();

        storedConfiguration = await db.DigitalAssetProviderConfigurations.AsNoTracking()
            .SingleAsync(x => x.Id == configuration.Id);

        Assert.True(storedConfiguration.LastHealthCheckSucceeded);
        Assert.NotNull(storedConfiguration.LastHealthCheckAt);
        Assert.Equal("healthy", storedConfiguration.LastHealthCheckMessage);

        var firstSync = await service.SyncBalancesAsync(providerCode);

        Assert.Equal(1, firstSync.BalanceCount);

        db.ChangeTracker.Clear();

        var wallet = await db.ProviderWalletBalances.AsNoTracking()
            .SingleAsync(x =>
                x.ProviderCode == providerCode &&
                x.ProviderWalletId == walletId);

        Assert.Equal(network.AssetCode, wallet.CurrencyCode);
        Assert.Equal(network.NetworkCode, wallet.NetworkCode);
        Assert.Equal(network.NetworkId, wallet.AssetNetworkId);
        Assert.Equal(125.50m, wallet.Balance);
        Assert.True(wallet.IsActive);
        Assert.True(wallet.LastSyncedAt > DateTime.UtcNow.AddMinutes(-2));

        provider.BalanceSnapshots = new[]
        {
            new DigitalAssetProviderBalanceSnapshot(
                walletId,
                network.AssetCode,
                network.NetworkCode,
                88.25m,
                false,
                "balance-2")
        };

        await service.SyncBalancesAsync(providerCode);

        db.ChangeTracker.Clear();

        var wallets = await db.ProviderWalletBalances.AsNoTracking()
            .Where(x =>
                x.ProviderCode == providerCode &&
                x.ProviderWalletId == walletId)
            .ToListAsync();

        var updated = Assert.Single(wallets);
        Assert.Equal(88.25m, updated.Balance);
        Assert.False(updated.IsActive);
        Assert.Equal(network.NetworkId, updated.AssetNetworkId);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_webhook_is_idempotent_and_rejects_event_id_replay_with_changed_payload()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var providerCode = UniqueProviderCode("WHK");
        var eventId = $"evt-{Guid.NewGuid():N}";

        var provider = new FakeCapabilityProvider(providerCode)
        {
            WebhookSignatureValid = true,
            WebhookEvent = new DigitalAssetProviderWebhookEvent(
                eventId,
                "digital_asset.deposit.updated",
                new DigitalAssetInboundNotification(
                    providerCode,
                    $"provider-tx-{Guid.NewGuid():N}",
                    Guid.NewGuid(),
                    "USDC",
                    $"0x{Guid.NewGuid():N}",
                    null,
                    $"0x{Guid.NewGuid():N}",
                    $"0x{Guid.NewGuid():N}",
                    10m,
                    3,
                    12345,
                    "provider-ref",
                    "{\"source\":\"webhook-test\"}",
                    DateTime.UtcNow),
                null)
        };

        var registry = new DigitalAssetProviderRegistry(new[] { provider });
        var dataProtection = new EphemeralDataProtectionProvider();
        var operations = new DigitalAssetProviderOperationsService(
            db,
            registry,
            dataProtection);

        await operations.UpsertConfigurationAsync(
            Guid.NewGuid(),
            new UpsertDigitalAssetProviderConfigurationRequestDto
            {
                ProviderCode = providerCode,
                DisplayName = "Webhook Test Provider",
                Reason = "Webhook provider integration test.",
                WebhookSecret = "webhook-secret-5678",
                IsActive = true,
                WebhooksEnabled = true,
                BalanceSyncEnabled = false
            });

        var settlement = new FakeSettlementService();
        var service = new DigitalAssetProviderWebhookService(
            db,
            registry,
            operations,
            settlement);

        var headers = new Dictionary<string, string>
        {
            ["X-Test-Signature"] = "valid"
        };

        const string payload = "{\"event\":\"deposit.updated\",\"version\":1}";

        await service.ProcessAsync(providerCode, payload, headers);

        db.ChangeTracker.Clear();

        var receipt = await db.DigitalAssetWebhookReceipts.AsNoTracking()
            .SingleAsync(x =>
                x.ProviderCode == providerCode &&
                x.ProviderEventId == eventId);

        Assert.Equal(DigitalAssetWebhookReceiptStatus.Processed, receipt.Status);
        Assert.NotNull(receipt.ProcessedAt);
        Assert.Equal(1, settlement.InboundCalls);
        Assert.Equal(0, settlement.OutboundCalls);

        await service.ProcessAsync(providerCode, payload, headers);

        Assert.Equal(1, settlement.InboundCalls);
        Assert.Equal(
            1,
            await db.DigitalAssetWebhookReceipts.CountAsync(x =>
                x.ProviderCode == providerCode &&
                x.ProviderEventId == eventId));

        var replay = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessAsync(
                providerCode,
                "{\"event\":\"deposit.updated\",\"version\":2}",
                headers));

        Assert.Contains("different payload", replay.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, settlement.InboundCalls);
    }

    [DatabaseIntegrationFact]
    public async Task Invalid_provider_webhook_signature_is_rejected_before_receipt_or_settlement()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var providerCode = UniqueProviderCode("SIG");
        var provider = new FakeCapabilityProvider(providerCode)
        {
            WebhookSignatureValid = false,
            WebhookEvent = new DigitalAssetProviderWebhookEvent(
                $"evt-{Guid.NewGuid():N}",
                "digital_asset.deposit.updated",
                new DigitalAssetInboundNotification(
                    providerCode,
                    $"provider-tx-{Guid.NewGuid():N}",
                    Guid.NewGuid(),
                    "USDC",
                    $"0x{Guid.NewGuid():N}",
                    null,
                    $"0x{Guid.NewGuid():N}",
                    $"0x{Guid.NewGuid():N}",
                    10m,
                    3,
                    12345,
                    "provider-ref",
                    null,
                    DateTime.UtcNow),
                null)
        };

        var registry = new DigitalAssetProviderRegistry(new[] { provider });
        var dataProtection = new EphemeralDataProtectionProvider();
        var operations = new DigitalAssetProviderOperationsService(
            db,
            registry,
            dataProtection);

        await operations.UpsertConfigurationAsync(
            Guid.NewGuid(),
            new UpsertDigitalAssetProviderConfigurationRequestDto
            {
                ProviderCode = providerCode,
                DisplayName = "Signature Test Provider",
                Reason = "Webhook signature integration test.",
                WebhookSecret = "webhook-secret-9999",
                IsActive = true,
                WebhooksEnabled = true,
                BalanceSyncEnabled = false
            });

        var settlement = new FakeSettlementService();
        var service = new DigitalAssetProviderWebhookService(
            db,
            registry,
            operations,
            settlement);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ProcessAsync(
                providerCode,
                "{\"event\":\"deposit.updated\"}",
                new Dictionary<string, string>()));

        Assert.Equal(0, settlement.InboundCalls);
        Assert.Equal(0, settlement.OutboundCalls);
        Assert.Equal(
            0,
            await db.DigitalAssetWebhookReceipts.CountAsync(x =>
                x.ProviderCode == providerCode));
    }

    private static async Task<NetworkScenario> CreateNetworkAsync(
        AppDbContext db,
        string prefix)
    {
        var assetCode = $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var networkCode = $"N{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var asset = new Asset
        {
            Code = assetCode,
            Name = $"Slice 12A Test Asset {assetCode}",
            Symbol = assetCode,
            Type = AssetType.Crypto,
            DecimalPlaces = 6,
            IsStablecoin = true,
            IsSupported = true,
            DepositEnabled = true,
            WithdrawalEnabled = true,
            TradingEnabled = true,
            InstantEnabled = true
        };

        var network = new AssetNetwork
        {
            Asset = asset,
            AssetCode = assetCode,
            NetworkCode = networkCode,
            Name = $"Slice 12A Test Network {networkCode}",
            NativeAssetCode = "TEST",
            RequiredConfirmations = 3,
            MinimumDeposit = 1m,
            MinimumWithdrawal = 1m,
            WithdrawalFee = 1m,
            DepositEnabled = true,
            WithdrawalEnabled = true,
            Status = AssetNetworkStatus.Active
        };

        db.Assets.Add(asset);
        db.AssetNetworks.Add(network);
        await db.SaveChangesAsync();

        var id = network.Id;
        db.ChangeTracker.Clear();

        return new NetworkScenario(
            assetCode,
            networkCode,
            id);
    }

    private static string UniqueProviderCode(string prefix) =>
        $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    private sealed record NetworkScenario(
        string AssetCode,
        string NetworkCode,
        Guid NetworkId);

    private sealed class FakeAddressRiskProvider :
        IDigitalAssetAddressRiskProvider
    {
        private readonly DigitalAssetAddressRiskResult _result;

        public FakeAddressRiskProvider(
            DigitalAssetAddressRiskResult result)
        {
            _result = result;
        }

        public string RiskProviderCode => "TEST-RISK";
        public int ScreenCalls { get; private set; }

        public Task<DigitalAssetAddressRiskResult> ScreenAsync(
            string assetCode,
            string networkCode,
            string address,
            DigitalAssetAddressScreeningDirection direction,
            CancellationToken ct = default)
        {
            ScreenCalls++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeCapabilityProvider :
        IDigitalAssetProvider,
        IDigitalAssetBalanceProvider,
        IDigitalAssetHealthProvider,
        IDigitalAssetWebhookProvider
    {
        public FakeCapabilityProvider(string providerCode)
        {
            ProviderCode = providerCode;
        }

        public string ProviderCode { get; }

        public IReadOnlyList<DigitalAssetProviderBalanceSnapshot> BalanceSnapshots { get; set; } =
            Array.Empty<DigitalAssetProviderBalanceSnapshot>();

        public DigitalAssetProviderHealthResult HealthResult { get; set; } =
            new(true, "healthy");

        public bool WebhookSignatureValid { get; set; } = true;

        public DigitalAssetProviderWebhookEvent WebhookEvent { get; set; } =
            new(
                $"evt-{Guid.NewGuid():N}",
                "digital_asset.deposit.updated",
                null,
                null);

        public bool Supports(string assetCode, string networkCode) => true;

        public Task<DigitalAssetDepositAddressResult> CreateDepositAddressAsync(
            DigitalAssetDepositAddressRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(new DigitalAssetDepositAddressResult(
                $"provider-address-{Guid.NewGuid():N}",
                $"0x{Guid.NewGuid():N}",
                null,
                $"provider-ref-{Guid.NewGuid():N}"));

        public Task<DigitalAssetWithdrawalSubmissionResult> SubmitWithdrawalAsync(
            DigitalAssetWithdrawalSubmissionRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(new DigitalAssetWithdrawalSubmissionResult(
                $"provider-wd-{Guid.NewGuid():N}",
                $"provider-ref-{Guid.NewGuid():N}",
                $"0x{Guid.NewGuid():N}",
                "SUBMITTED"));

        public Task<IReadOnlyList<DigitalAssetProviderBalanceSnapshot>> GetBalancesAsync(
            CancellationToken ct = default) =>
            Task.FromResult(BalanceSnapshots);

        public Task<DigitalAssetProviderHealthResult> CheckHealthAsync(
            CancellationToken ct = default) =>
            Task.FromResult(HealthResult);

        public bool VerifyWebhookSignature(
            string payload,
            IReadOnlyDictionary<string, string> headers,
            string webhookSecret) =>
            WebhookSignatureValid;

        public Task<DigitalAssetProviderWebhookEvent> ParseWebhookAsync(
            string payload,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken ct = default) =>
            Task.FromResult(WebhookEvent);
    }

    private sealed class FakeSettlementService :
        IDigitalAssetSettlementService
    {
        public int InboundCalls { get; private set; }
        public int OutboundCalls { get; private set; }

        public Task ProcessInboundAsync(
            DigitalAssetInboundNotification notification,
            CancellationToken ct = default)
        {
            InboundCalls++;
            return Task.CompletedTask;
        }

        public Task ProcessOutboundAsync(
            DigitalAssetOutboundNotification notification,
            CancellationToken ct = default)
        {
            OutboundCalls++;
            return Task.CompletedTask;
        }
    }
}
