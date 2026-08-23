using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Services.DigitalAssets;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.FinancialCore;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class DigitalAssetWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public DigitalAssetWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Deposit_address_creation_is_tenant_scoped_and_idempotent()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantOne = await CreateScenarioAsync(db, "address-one", 0m);
        var tenantTwo = await CreateScenarioAsync(db, "address-two", 0m);

        var provider = new FakeDigitalAssetProvider(
            "TESTCHAIN",
            $"0x{Guid.NewGuid():N}");

        var service = CreateService(
            scope,
            db,
            tenantOne.ProfileId,
            EmbeddedFinanceScope.DigitalAssetsRead | EmbeddedFinanceScope.DigitalAssetsWrite,
            provider);

        var created = await service.CreateDepositAddressAsync(
            tenantOne.CustomerId,
            new CreateDigitalAssetDepositAddressRequestDto(
                tenantOne.AccountId,
                tenantOne.NetworkId,
                provider.ProviderCode));

        var duplicate = await service.CreateDepositAddressAsync(
            tenantOne.CustomerId,
            new CreateDigitalAssetDepositAddressRequestDto(
                tenantOne.AccountId,
                tenantOne.NetworkId,
                provider.ProviderCode));

        Assert.Equal(created.Id, duplicate.Id);
        Assert.Equal(1, provider.DepositAddressCreateCalls);

        var stored = await db.DigitalAssetDepositAddresses.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(tenantOne.ProfileId, stored.BusinessProfileId);
        Assert.Equal(tenantOne.CustomerId, stored.BusinessCustomerId);
        Assert.Equal(tenantOne.AccountId, stored.FinancialAccountId);

        var tenantEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetDepositAddressesAsync(tenantTwo.CustomerId));

        Assert.Contains("business customer", tenantEx.Message, StringComparison.OrdinalIgnoreCase);

        var readOnly = CreateService(
            scope,
            db,
            tenantOne.ProfileId,
            EmbeddedFinanceScope.DigitalAssetsRead,
            provider);

        var writeEx = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            readOnly.CreateDepositAddressAsync(
                tenantOne.CustomerId,
                new CreateDigitalAssetDepositAddressRequestDto(
                    tenantOne.AccountId,
                    tenantOne.NetworkId,
                    provider.ProviderCode)));

        Assert.Contains("DigitalAssetsWrite", writeEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task Inbound_deposit_credits_only_after_confirmations_and_replay_is_idempotent()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateScenarioAsync(db, "deposit", 0m);
        var provider = new FakeDigitalAssetProvider(
            "TESTCHAIN",
            $"0x{Guid.NewGuid():N}");

        var service = CreateService(
            scope,
            db,
            setup.ProfileId,
            EmbeddedFinanceScope.DigitalAssetsRead | EmbeddedFinanceScope.DigitalAssetsWrite,
            provider);

        var address = await service.CreateDepositAddressAsync(
            setup.CustomerId,
            new CreateDigitalAssetDepositAddressRequestDto(
                setup.AccountId,
                setup.NetworkId,
                provider.ProviderCode));

        var providerTxId = $"dep-{Guid.NewGuid():N}";
        var txHash = $"0x{Guid.NewGuid():N}";
        const decimal amount = 25m;

        await service.ProcessInboundAsync(
            Inbound(provider.ProviderCode, providerTxId, setup, address.Address, amount, 1, txHash));

        db.ChangeTracker.Clear();

        var beforeConfirmation = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(0m, beforeConfirmation.AvailableBalance);
        Assert.Equal(0m, beforeConfirmation.SettledBalance);
        Assert.Equal(0, await db.Collections.CountAsync(x => x.ProviderCollectionId == providerTxId));

        var confirming = await db.DigitalAssetNetworkTransactions.AsNoTracking()
            .SingleAsync(x =>
                x.ProviderCode == provider.ProviderCode &&
                x.ProviderTransactionId == providerTxId);

        Assert.Equal(DigitalAssetTransactionStatus.Confirming, confirming.Status);
        Assert.Equal(1, confirming.Confirmations);

        await service.ProcessInboundAsync(
            Inbound(provider.ProviderCode, providerTxId, setup, address.Address, amount, 3, txHash));

        db.ChangeTracker.Clear();

        var afterConfirmation = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(amount, afterConfirmation.AvailableBalance);
        Assert.Equal(amount, afterConfirmation.SettledBalance);

        var confirmed = await db.DigitalAssetNetworkTransactions.AsNoTracking()
            .SingleAsync(x =>
                x.ProviderCode == provider.ProviderCode &&
                x.ProviderTransactionId == providerTxId);

        Assert.Equal(DigitalAssetTransactionStatus.Confirmed, confirmed.Status);
        Assert.Equal(3, confirmed.Confirmations);
        Assert.NotNull(confirmed.CollectionId);
        Assert.NotNull(confirmed.LedgerTransactionId);

        Assert.Equal(1, await db.Collections.CountAsync(x => x.ProviderCollectionId == providerTxId));
        Assert.Equal(
            1,
            await db.LedgerTransactions.CountAsync(x =>
                x.IdempotencyScope == $"DigitalAssetDeposit:{provider.ProviderCode}" &&
                x.IdempotencyKey == providerTxId));

        await service.ProcessInboundAsync(
            Inbound(provider.ProviderCode, providerTxId, setup, address.Address, amount, 5, txHash));

        db.ChangeTracker.Clear();

        var afterReplay = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(amount, afterReplay.AvailableBalance);
        Assert.Equal(amount, afterReplay.SettledBalance);
        Assert.Equal(1, await db.Collections.CountAsync(x => x.ProviderCollectionId == providerTxId));
        Assert.Equal(
            1,
            await db.LedgerTransactions.CountAsync(x =>
                x.IdempotencyScope == $"DigitalAssetDeposit:{provider.ProviderCode}" &&
                x.IdempotencyKey == providerTxId));
    }

    [DatabaseIntegrationFact]
    public async Task Successful_withdrawal_reserves_amount_plus_fee_and_captures_after_confirmation()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateScenarioAsync(db, "withdraw-success", 100m);
        var providerTxId = $"wd-{Guid.NewGuid():N}";
        var txHash = $"0x{Guid.NewGuid():N}";

        var provider = new FakeDigitalAssetProvider(
            "TESTCHAIN",
            $"0x{Guid.NewGuid():N}",
            providerTxId,
            txHash);

        var service = CreateService(
            scope,
            db,
            setup.ProfileId,
            EmbeddedFinanceScope.DigitalAssetsRead | EmbeddedFinanceScope.DigitalAssetsWrite,
            provider);

        var destination = await service.CreateWithdrawalDestinationAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalDestinationRequestDto(
                setup.NetworkId,
                $"0x{Guid.NewGuid():N}",
                null,
                "Primary treasury wallet"));

        var created = await service.CreateWithdrawalAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalRequestDto(
                setup.AccountId,
                destination.Id,
                20m,
                provider.ProviderCode,
                $"EXT-{Guid.NewGuid():N}"));

        Assert.Equal(DigitalAssetWithdrawalStatus.Submitted, created.Status);
        Assert.Equal(20m, created.Amount);
        Assert.Equal(1m, created.NetworkFee);
        Assert.Equal(21m, created.TotalDebitAmount);
        Assert.Equal(1, provider.WithdrawalSubmitCalls);

        db.ChangeTracker.Clear();

        var reservedAccount = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(79m, reservedAccount.AvailableBalance);
        Assert.Equal(100m, reservedAccount.SettledBalance);
        Assert.Equal(21m, reservedAccount.HeldBalance);

        var withdrawal = await db.DigitalAssetWithdrawals.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        var reservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == withdrawal.ReservationId);

        Assert.Equal(FinancialReservationType.Withdrawal, reservation.Type);
        Assert.Equal(FinancialReservationStatus.Active, reservation.Status);
        Assert.Equal(21m, reservation.Amount);

        await service.ProcessOutboundAsync(
            new DigitalAssetOutboundNotification(
                provider.ProviderCode,
                providerTxId,
                "CONFIRMED",
                3,
                txHash,
                12345,
                1m,
                "provider-completed",
                "{\"status\":\"confirmed\"}",
                DateTime.UtcNow));

        db.ChangeTracker.Clear();

        var completedAccount = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(79m, completedAccount.AvailableBalance);
        Assert.Equal(79m, completedAccount.SettledBalance);
        Assert.Equal(0m, completedAccount.HeldBalance);

        var completedReservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == withdrawal.ReservationId);

        Assert.Equal(FinancialReservationStatus.Captured, completedReservation.Status);
        Assert.Equal(21m, completedReservation.CapturedAmount);
        Assert.Equal(0m, completedReservation.ReleasedAmount);

        var completedWithdrawal = await db.DigitalAssetWithdrawals.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(DigitalAssetWithdrawalStatus.Completed, completedWithdrawal.Status);
        Assert.NotNull(completedWithdrawal.CompletedAt);

        var payout = await db.Payouts.AsNoTracking()
            .SingleAsync(x => x.Id == created.PayoutId);

        Assert.Equal(PayoutStatus.Successful, payout.Status);

        var networkTx = await db.DigitalAssetNetworkTransactions.AsNoTracking()
            .SingleAsync(x =>
                x.ProviderCode == provider.ProviderCode &&
                x.ProviderTransactionId == providerTxId);

        Assert.Equal(DigitalAssetTransactionStatus.Confirmed, networkTx.Status);
        Assert.Equal(3, networkTx.Confirmations);
        Assert.Equal(txHash, networkTx.TransactionHash);
    }

    [DatabaseIntegrationFact]
    public async Task Failed_outbound_withdrawal_releases_reservation_and_restores_available_balance()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var setup = await CreateScenarioAsync(db, "withdraw-fail", 100m);
        var providerTxId = $"wd-{Guid.NewGuid():N}";

        var provider = new FakeDigitalAssetProvider(
            "TESTCHAIN",
            $"0x{Guid.NewGuid():N}",
            providerTxId,
            null);

        var service = CreateService(
            scope,
            db,
            setup.ProfileId,
            EmbeddedFinanceScope.DigitalAssetsRead | EmbeddedFinanceScope.DigitalAssetsWrite,
            provider);

        var destination = await service.CreateWithdrawalDestinationAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalDestinationRequestDto(
                setup.NetworkId,
                $"0x{Guid.NewGuid():N}",
                null,
                "Failure target"));

        var created = await service.CreateWithdrawalAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalRequestDto(
                setup.AccountId,
                destination.Id,
                20m,
                provider.ProviderCode,
                null));

        db.ChangeTracker.Clear();

        var beforeFailure = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(79m, beforeFailure.AvailableBalance);
        Assert.Equal(21m, beforeFailure.HeldBalance);

        await service.ProcessOutboundAsync(
            new DigitalAssetOutboundNotification(
                provider.ProviderCode,
                providerTxId,
                "FAILED",
                0,
                null,
                null,
                null,
                "provider-failed",
                "{\"status\":\"failed\"}",
                DateTime.UtcNow));

        db.ChangeTracker.Clear();

        var restored = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(100m, restored.AvailableBalance);
        Assert.Equal(100m, restored.SettledBalance);
        Assert.Equal(0m, restored.HeldBalance);

        var withdrawal = await db.DigitalAssetWithdrawals.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(DigitalAssetWithdrawalStatus.Failed, withdrawal.Status);
        Assert.NotNull(withdrawal.FailedAt);

        var reservation = await db.FinancialReservations.AsNoTracking()
            .SingleAsync(x => x.Id == withdrawal.ReservationId);

        Assert.Equal(FinancialReservationStatus.Released, reservation.Status);
        Assert.Equal(0m, reservation.CapturedAmount);
        Assert.Equal(21m, reservation.ReleasedAmount);

        var payout = await db.Payouts.AsNoTracking()
            .SingleAsync(x => x.Id == created.PayoutId);

        Assert.Equal(PayoutStatus.Failed, payout.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Reconciliation_surfaces_failed_network_transactions_but_not_clean_confirmed_deposits()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reconciliation = scope.ServiceProvider.GetRequiredService<IDigitalAssetReconciliationService>();

        var setup = await CreateScenarioAsync(db, "reconciliation", 0m);
        var provider = new FakeDigitalAssetProvider(
            "TESTCHAIN",
            $"0x{Guid.NewGuid():N}");

        var service = CreateService(
            scope,
            db,
            setup.ProfileId,
            EmbeddedFinanceScope.DigitalAssetsRead | EmbeddedFinanceScope.DigitalAssetsWrite,
            provider);

        var address = await service.CreateDepositAddressAsync(
            setup.CustomerId,
            new CreateDigitalAssetDepositAddressRequestDto(
                setup.AccountId,
                setup.NetworkId,
                provider.ProviderCode));

        var cleanProviderTx = $"clean-{Guid.NewGuid():N}";

        await service.ProcessInboundAsync(
            Inbound(
                provider.ProviderCode,
                cleanProviderTx,
                setup,
                address.Address,
                10m,
                3,
                $"0x{Guid.NewGuid():N}"));

        db.DigitalAssetNetworkTransactions.Add(new KorridorX.Models.DigitalAssets.DigitalAssetNetworkTransaction
        {
            ProviderCode = provider.ProviderCode,
            ProviderTransactionId = $"failed-{Guid.NewGuid():N}",
            AssetNetworkId = setup.NetworkId,
            AssetCode = setup.AssetCode,
            Direction = DigitalAssetTransactionDirection.Inbound,
            Status = DigitalAssetTransactionStatus.Failed,
            Amount = 5m,
            Confirmations = 0,
            RequiredConfirmations = 3,
            ObservedAt = DateTime.UtcNow,
            FailedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var exceptions = await reconciliation.GetExceptionsAsync();

        Assert.Contains(exceptions, x =>
            x.ProviderCode == provider.ProviderCode &&
            x.ProviderTransactionId.StartsWith("failed-"));

        Assert.DoesNotContain(exceptions, x =>
            x.ProviderCode == provider.ProviderCode &&
            x.ProviderTransactionId == cleanProviderTx);
    }

    private DigitalAssetService CreateService(
        AsyncServiceScope scope,
        AppDbContext db,
        Guid businessProfileId,
        EmbeddedFinanceScope scopes,
        IDigitalAssetProvider provider)
    {
        return new DigitalAssetService(
            db,
            new FixedEmbeddedContextAccessor(new EmbeddedFinancePrincipal(
                Guid.NewGuid(),
                Guid.NewGuid(),
                businessProfileId,
                scopes,
                "digital-asset-test-key")),
            new DigitalAssetProviderRegistry(new[] { provider }),
            scope.ServiceProvider.GetRequiredService<IFinancialReservationService>(),
            new DigitalAssetComplianceGate(),
            scope.ServiceProvider.GetRequiredService<IEmbeddedWebhookPublisher>());
    }

    private async Task<Scenario> CreateScenarioAsync(
        AppDbContext db,
        string label,
        decimal balance)
    {
        using var client = _fixture.CreateClient();
        var unique = Guid.NewGuid().ToString("N");

        var registration = await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Digital",
                "AssetOwner",
                $"{label}-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550241",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var profile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"Digital Asset Test {label} {unique}",
            CountryCode = "CA",
            ContactEmail = $"{label}-{unique}@example.test",
            ContactPhone = "+12145550241",
            KybStatus = KybStatus.Approved,
            KybApprovedAt = DateTime.UtcNow
        };

        var customer = new BusinessCustomer
        {
            BusinessProfile = profile,
            ExternalReference = $"CUS-{unique}",
            DisplayName = $"Digital Asset Customer {unique}",
            Email = $"customer-{unique}@example.test",
            CountryCode = "CA",
            Status = BusinessCustomerStatus.Active
        };

        var assetCode = $"DX{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var asset = new Asset
        {
            Code = assetCode,
            Name = $"Test Stablecoin {assetCode}",
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
            NetworkCode = $"NET{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            Name = "Test Settlement Network",
            NativeAssetCode = "TEST",
            RequiredConfirmations = 3,
            MinimumDeposit = 1m,
            MinimumWithdrawal = 1m,
            WithdrawalFee = 1m,
            DepositEnabled = true,
            WithdrawalEnabled = true,
            Status = AssetNetworkStatus.Active
        };

        var account = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.BusinessCustomer,
            OwnerId = customer.Id,
            AccountCode = TestReference($"ACC-{assetCode}"),
            AssetCode = assetCode,
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = balance,
            AvailableBalance = balance,
            HeldBalance = 0m
        };

        db.BusinessProfiles.Add(profile);
        db.BusinessCustomers.Add(customer);
        db.Assets.Add(asset);
        db.AssetNetworks.Add(network);
        db.FinancialAccounts.Add(account);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Scenario(
            profile.Id,
            customer.Id,
            account.Id,
            assetCode,
            network.Id,
            network.NetworkCode);
    }

    private static DigitalAssetInboundNotification Inbound(
        string providerCode,
        string providerTransactionId,
        Scenario setup,
        string address,
        decimal amount,
        int confirmations,
        string txHash) =>
        new(
            providerCode,
            providerTransactionId,
            setup.NetworkId,
            setup.AssetCode,
            address,
            null,
            $"0x{Guid.NewGuid():N}",
            txHash,
            amount,
            confirmations,
            confirmations >= 3 ? 1000 : null,
            providerTransactionId,
            "{\"source\":\"integration-test\"}",
            DateTime.UtcNow);

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }

    private sealed record Scenario(
        Guid ProfileId,
        Guid CustomerId,
        Guid AccountId,
        string AssetCode,
        Guid NetworkId,
        string NetworkCode);

    private sealed class FixedEmbeddedContextAccessor : IEmbeddedFinanceContextAccessor
    {
        private readonly EmbeddedFinancePrincipal _principal;

        public FixedEmbeddedContextAccessor(EmbeddedFinancePrincipal principal)
        {
            _principal = principal;
        }

        public EmbeddedFinancePrincipal GetRequiredPrincipal() => _principal;
    }

    private sealed class FakeDigitalAssetProvider : IDigitalAssetProvider
    {
        private readonly string _depositAddress;
        private readonly string? _withdrawalProviderTransactionId;
        private readonly string? _withdrawalTransactionHash;

        public FakeDigitalAssetProvider(
            string providerCode,
            string depositAddress,
            string? withdrawalProviderTransactionId = null,
            string? withdrawalTransactionHash = null)
        {
            ProviderCode = providerCode;
            _depositAddress = depositAddress;
            _withdrawalProviderTransactionId = withdrawalProviderTransactionId;
            _withdrawalTransactionHash = withdrawalTransactionHash;
        }

        public string ProviderCode { get; }
        public int DepositAddressCreateCalls { get; private set; }
        public int WithdrawalSubmitCalls { get; private set; }

        public bool Supports(string assetCode, string networkCode) =>
            !string.IsNullOrWhiteSpace(assetCode) &&
            !string.IsNullOrWhiteSpace(networkCode);

        public Task<DigitalAssetDepositAddressResult> CreateDepositAddressAsync(
            DigitalAssetDepositAddressRequest request,
            CancellationToken ct = default)
        {
            DepositAddressCreateCalls++;

            return Task.FromResult(new DigitalAssetDepositAddressResult(
                $"provider-address-{Guid.NewGuid():N}",
                _depositAddress,
                null,
                $"provider-ref-{Guid.NewGuid():N}"));
        }

        public Task<DigitalAssetWithdrawalSubmissionResult> SubmitWithdrawalAsync(
            DigitalAssetWithdrawalSubmissionRequest request,
            CancellationToken ct = default)
        {
            WithdrawalSubmitCalls++;

            return Task.FromResult(new DigitalAssetWithdrawalSubmissionResult(
                _withdrawalProviderTransactionId ?? $"provider-wd-{Guid.NewGuid():N}",
                $"provider-ref-{Guid.NewGuid():N}",
                _withdrawalTransactionHash,
                "SUBMITTED"));
        }
    }
}
