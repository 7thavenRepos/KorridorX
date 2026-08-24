using System.Net;
using System.Reflection;
using System.Text;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.DigitalAssets;
using KorridorX.Models.DigitalAssets;
using KorridorX.Models.Payments;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Models.Providers;
using KorridorX.Providers.DigitalAssets;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.DigitalAssets;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BlaaizDigitalAssetProviderTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public BlaaizDigitalAssetProviderTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }


    [Fact]
    public async Task Crypto_payout_uses_dedicated_blaaiz_crypto_endpoint_and_idempotency_key()
    {
        var payoutId = Guid.NewGuid();
        var address = $"0x{Guid.NewGuid():N}";
        var idempotencyKey = $"DAX-WD-{Guid.NewGuid():N}";

        var handler = new RecordingHttpMessageHandler(
            $$"""
            {
              "message": "Payout initiated",
              "data": {
                "id": "crypto-tx-001",
                "reference": "crypto-ref-001",
                "status": "PENDING",
                "wallet_id": "wallet-usdc",
                "customer_id": "customer-001",
                "currency": "USDC",
                "destination_currency": "USDC",
                "amount": "20",
                "fee": "0.1",
                "net_amount": "19.9",
                "destination_amount": "19.9",
                "address": "{{address}}",
                "network": "MATIC_MAINNET"
              }
            }
            """);

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test")
        };

        var token = new FakeTokenService();
        var audit = new RecordingAuditService();
        var client = new BlaaizApiClient(http, token, audit);

        var result = await client.InitiateCryptoPayoutAsync(
            new BlaaizCryptoPayoutRequest
            {
                CustomerId = "customer-001",
                WalletId = "wallet-usdc",
                Amount = "20",
                Address = address,
                Network = "MATIC_MAINNET",
                Token = "USDC"
            },
            idempotencyKey,
            payoutId);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/external/crypto/payouts", handler.RequestUri?.AbsolutePath);
        Assert.Equal("Bearer test-access-token", handler.Authorization);
        Assert.Equal(idempotencyKey, handler.IdempotencyKey);
        Assert.Contains("\"customer_id\":\"customer-001\"", handler.Body);
        Assert.Contains("\"wallet_id\":\"wallet-usdc\"", handler.Body);
        Assert.Contains("\"network\":\"MATIC_MAINNET\"", handler.Body);
        Assert.Contains("\"token\":\"USDC\"", handler.Body);

        Assert.Equal(payoutId, audit.RelatedPayoutId);
        Assert.Equal("/api/external/crypto/payouts", audit.Endpoint);
        Assert.Equal("POST", audit.HttpMethod);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Equal(0, audit.FailCalls);

        Assert.Equal("crypto-tx-001", result.Data.Data.Id);
        Assert.Equal("crypto-ref-001", result.Data.Data.Reference);
        Assert.Equal(audit.LogId, result.RequestLogId);
    }


    [DatabaseIntegrationFact]
    public async Task Blaaiz_crypto_balances_are_translated_from_dedicated_crypto_wallet_endpoint()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (api, proxy) = CreateFakeApi();

        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-usdc", "usdc", "125.500001", true),
            ("wallet-usdt", "USDT", "75.25", false));

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(new BlaaizOptions()));

        var balances = await provider.GetBalancesAsync();

        Assert.Equal(2, balances.Count);

        var usdc = Assert.Single(
            balances,
            x => x.ProviderWalletId == "wallet-usdc");

        Assert.Equal("USDC", usdc.AssetCode);
        Assert.Null(usdc.NetworkCode);
        Assert.Equal(125.500001m, usdc.Balance);
        Assert.True(usdc.IsActive);

        var health = await provider.CheckHealthAsync();

        Assert.True(health.IsHealthy);
        Assert.Contains("Crypto API reachable", health.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, proxy.CryptoWalletCalls);
    }


    [DatabaseIntegrationFact]
    public async Task Blaaiz_persistent_deposit_address_provisioning_redirects_to_amount_specific_intents()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (api, _) = CreateFakeApi();

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(new BlaaizOptions()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.CreateDepositAddressAsync(
                new DigitalAssetDepositAddressRequest(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "USDC",
                    "POLYGON")));

        Assert.Contains("amount-specific", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deposit-intents", ex.Message, StringComparison.OrdinalIgnoreCase);
    }


    [DatabaseIntegrationFact]
    public async Task Blaaiz_withdrawal_uses_verified_customer_mapped_network_configured_crypto_wallet_and_idempotency_key()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var setup = await CreateScenarioAsync(
            db,
            "blaaiz-success",
            100m,
            "POLYGON",
            "VERIFIED",
            "USDC");

        var (api, proxy) = CreateFakeApi();

        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-primary", setup.AssetCode, "1000", true),
            ("wallet-selected", setup.AssetCode, "500", true));

        proxy.CryptoPayoutResponse = new BlaaizCryptoPayoutResponse
        {
            Message = "Payout initiated",
            Data = new BlaaizCryptoPayoutData
            {
                Id = $"crypto-tx-{Guid.NewGuid():N}",
                Reference = $"crypto-ref-{Guid.NewGuid():N}",
                Status = "PENDING",
                WalletId = "wallet-selected",
                CustomerId = setup.ProviderCustomerId,
                Currency = setup.AssetCode,
                DestinationCurrency = setup.AssetCode,
                Amount = "20",
                Network = "MATIC_MAINNET"
            }
        };

        var options = new BlaaizOptions();
        options.CryptoWalletIds[setup.AssetCode] = "wallet-selected";
        options.CryptoNetworkMappings[setup.NetworkCode] = "MATIC_MAINNET";

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(options));

        var service = CreateDigitalAssetService(
            scope,
            db,
            setup.ProfileId,
            provider);

        var destinationAddress = $"0x{Guid.NewGuid():N}";

        var destination = await service.CreateWithdrawalDestinationAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalDestinationRequestDto(
                setup.NetworkId,
                destinationAddress,
                null,
                "Blaaiz Polygon target"));

        var externalReference = $"EXT-{Guid.NewGuid():N}";

        var created = await service.CreateWithdrawalAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalRequestDto(
                setup.AccountId,
                destination.Id,
                20m,
                provider.ProviderCode,
                externalReference));

        Assert.Equal(DigitalAssetWithdrawalStatus.Submitted, created.Status);
        Assert.Equal(1, proxy.PayoutCalls);

        Assert.NotNull(proxy.LastCryptoPayoutRequest);
        Assert.Equal(setup.ProviderCustomerId, proxy.LastCryptoPayoutRequest!.CustomerId);
        Assert.Equal("wallet-selected", proxy.LastCryptoPayoutRequest.WalletId);
        Assert.Equal("20", proxy.LastCryptoPayoutRequest.Amount);
        Assert.Equal(destinationAddress, proxy.LastCryptoPayoutRequest.Address);
        Assert.Equal("MATIC_MAINNET", proxy.LastCryptoPayoutRequest.Network);
        Assert.Equal(setup.AssetCode, proxy.LastCryptoPayoutRequest.Token);
        Assert.Equal(externalReference, proxy.LastIdempotencyKey);
        Assert.Equal(created.PayoutId, proxy.LastPayoutId);

        db.ChangeTracker.Clear();

        var networkTx = await db.DigitalAssetNetworkTransactions.AsNoTracking()
            .SingleAsync(x => x.PayoutId == created.PayoutId);

        Assert.Equal(provider.ProviderCode, networkTx.ProviderCode);
        Assert.Equal(proxy.CryptoPayoutResponse.Data.Id, networkTx.ProviderTransactionId);
        Assert.Equal(proxy.CryptoPayoutResponse.Data.Reference, networkTx.ProviderReference);
        Assert.Equal(DigitalAssetTransactionStatus.Confirming, networkTx.Status);
    }


    [DatabaseIntegrationFact]
    public async Task Blaaiz_withdrawal_rejects_unverified_customer_before_crypto_payout_submission()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var setup = await CreateScenarioAsync(
            db,
            "blaaiz-unverified",
            100m,
            "POLYGON",
            "PENDING",
            "USDC");

        var (api, proxy) = CreateFakeApi();

        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-only", setup.AssetCode, "1000", true));

        var options = new BlaaizOptions();
        options.CryptoNetworkMappings[setup.NetworkCode] = "MATIC_MAINNET";

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(options));

        var service = CreateDigitalAssetService(
            scope,
            db,
            setup.ProfileId,
            provider);

        var destination = await service.CreateWithdrawalDestinationAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalDestinationRequestDto(
                setup.NetworkId,
                $"0x{Guid.NewGuid():N}",
                null,
                "Unverified customer target"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWithdrawalAsync(
                setup.CustomerId,
                new CreateDigitalAssetWithdrawalRequestDto(
                    setup.AccountId,
                    destination.Id,
                    20m,
                    provider.ProviderCode,
                    $"EXT-{Guid.NewGuid():N}")));

        Assert.Contains("VERIFIED", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, proxy.PayoutCalls);
        Assert.Equal(0, proxy.CryptoWalletCalls);

        db.ChangeTracker.Clear();

        var withdrawal = await db.DigitalAssetWithdrawals.AsNoTracking()
            .SingleAsync(x =>
                x.BusinessCustomerId == setup.CustomerId &&
                x.ProviderCode == provider.ProviderCode);

        Assert.Equal(DigitalAssetWithdrawalStatus.Failed, withdrawal.Status);

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(100m, account.AvailableBalance);
        Assert.Equal(0m, account.HeldBalance);
    }


    [DatabaseIntegrationFact]
    public async Task Multiple_active_blaaiz_crypto_wallets_require_explicit_wallet_configuration()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var setup = await CreateScenarioAsync(
            db,
            "blaaiz-wallet-choice",
            100m,
            "POLYGON",
            "VERIFIED",
            "USDC");

        var (api, proxy) = CreateFakeApi();

        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-a", setup.AssetCode, "1000", true),
            ("wallet-b", setup.AssetCode, "500", true));

        var options = new BlaaizOptions();
        options.CryptoNetworkMappings[setup.NetworkCode] = "MATIC_MAINNET";

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(options));

        var service = CreateDigitalAssetService(
            scope,
            db,
            setup.ProfileId,
            provider);

        var destination = await service.CreateWithdrawalDestinationAsync(
            setup.CustomerId,
            new CreateDigitalAssetWithdrawalDestinationRequestDto(
                setup.NetworkId,
                $"0x{Guid.NewGuid():N}",
                null,
                "Ambiguous wallet target"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateWithdrawalAsync(
                setup.CustomerId,
                new CreateDigitalAssetWithdrawalRequestDto(
                    setup.AccountId,
                    destination.Id,
                    20m,
                    provider.ProviderCode,
                    $"EXT-{Guid.NewGuid():N}")));

        Assert.Contains("Multiple active", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CryptoWalletIds", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, proxy.PayoutCalls);

        db.ChangeTracker.Clear();

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(100m, account.AvailableBalance);
        Assert.Equal(0m, account.HeldBalance);
    }


    [Fact]
    public async Task Crypto_collection_client_posts_customer_bound_payload_to_collection_crypto_endpoint()
    {
        var depositIntentId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        var handler = new RecordingHttpMessageHandler(
            $$"""
            {
              "message": "Crypto collection initiated",
              "transaction": {
                "transaction_id": "collection-tx-001",
                "reference": "collection-ref-001",
                "token": "USDC",
                "token_amount": 25,
                "network": "MATIC_MAINNET",
                "address": "0xcollectionaddress",
                "status": "PENDING",
                "expires_at": "{{expiresAt:O}}"
              }
            }
            """);

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test")
        };

        var token = new FakeTokenService();
        var audit = new RecordingAuditService();
        var client = new BlaaizApiClient(http, token, audit);

        var result = await client.InitiateCryptoCollectionAsync(
            new BlaaizCryptoCollectionRequest
            {
                Amount = 25m,
                WalletId = "wallet-usdc",
                Network = "MATIC_MAINNET",
                Token = "USDC",
                CustomerId = "customer-001"
            },
            depositIntentId);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/external/collection/crypto", handler.RequestUri?.AbsolutePath);
        Assert.Contains("\"amount\":25", handler.Body);
        Assert.Contains("\"wallet_id\":\"wallet-usdc\"", handler.Body);
        Assert.Contains("\"network\":\"MATIC_MAINNET\"", handler.Body);
        Assert.Contains("\"token\":\"USDC\"", handler.Body);
        Assert.Contains("\"customer_id\":\"customer-001\"", handler.Body);

        Assert.Equal("/api/external/collection/crypto", audit.Endpoint);
        Assert.Equal("POST", audit.HttpMethod);
        Assert.Equal(1, audit.CompleteCalls);
        Assert.Equal("collection-tx-001", result.Data.Transaction.TransactionId);
        Assert.Equal(25m, result.Data.Transaction.TokenAmount);
    }

    [DatabaseIntegrationFact]
    public async Task Blaaiz_deposit_intent_binds_verified_customer_amount_wallet_network_and_provider_transaction()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var setup = await CreateScenarioAsync(
            db,
            "blaaiz-deposit-intent",
            100m,
            "POLYGON",
            "VERIFIED",
            "USDC");

        var (api, proxy) = CreateFakeApi();
        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-usdc", setup.AssetCode, "500", true));

        var providerCollectionId = $"collection-{Guid.NewGuid():N}";
        var providerReference = $"collection-ref-{Guid.NewGuid():N}";
        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        proxy.CryptoCollectionResponse = CollectionResponse(
            providerCollectionId,
            providerReference,
            setup.AssetCode,
            25m,
            "MATIC_MAINNET",
            "0xcollection001",
            "PENDING",
            expiresAt);

        var options = new BlaaizOptions();
        options.CryptoNetworkMappings[setup.NetworkCode] = "MATIC_MAINNET";

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(options));

        var service = CreateDigitalAssetService(
            scope,
            db,
            setup.ProfileId,
            provider);

        var created = await service.CreateDepositIntentAsync(
            setup.CustomerId,
            new CreateDigitalAssetDepositIntentRequestDto(
                setup.AccountId,
                setup.NetworkId,
                25m,
                provider.ProviderCode));

        Assert.Equal(CollectionStatus.Initiated, created.Status);
        Assert.Equal(25m, created.Amount);
        Assert.Equal("0xcollection001", created.Address);
        Assert.Equal(providerCollectionId, created.ProviderCollectionId);
        Assert.Equal(providerReference, created.ProviderReference);
        Assert.Equal(1, proxy.CollectionCalls);

        Assert.NotNull(proxy.LastCryptoCollectionRequest);
        Assert.Equal(25m, proxy.LastCryptoCollectionRequest!.Amount);
        Assert.Equal("wallet-usdc", proxy.LastCryptoCollectionRequest.WalletId);
        Assert.Equal("MATIC_MAINNET", proxy.LastCryptoCollectionRequest.Network);
        Assert.Equal(setup.AssetCode, proxy.LastCryptoCollectionRequest.Token);
        Assert.Equal(setup.ProviderCustomerId, proxy.LastCryptoCollectionRequest.CustomerId);
        Assert.Equal(created.Id, proxy.LastDepositIntentId);

        db.ChangeTracker.Clear();

        var persisted = await db.DigitalAssetDepositIntents.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(setup.CustomerId, persisted.BusinessCustomerId);
        Assert.Equal(setup.AccountId, persisted.FinancialAccountId);
        Assert.Equal(providerCollectionId, persisted.ProviderCollectionId);
        Assert.Equal(CollectionStatus.Initiated, persisted.Status);

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(100m, account.AvailableBalance);
        Assert.Equal(100m, account.SettledBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Successful_blaaiz_deposit_intent_webhook_credits_exact_account_once_and_replay_is_idempotent()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var setup = await CreateScenarioAsync(
            db,
            "blaaiz-deposit-credit",
            100m,
            "POLYGON",
            "VERIFIED",
            "USDC");

        var (api, proxy) = CreateFakeApi();
        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-usdc", setup.AssetCode, "500", true));
        var providerCollectionId = $"collection-credit-{Guid.NewGuid():N}";
        proxy.CryptoCollectionResponse = CollectionResponse(
            providerCollectionId,
            $"collection-ref-{Guid.NewGuid():N}",
            setup.AssetCode,
            25m,
            "MATIC_MAINNET",
            "0xcollectioncredit",
            "PENDING",
            DateTime.UtcNow.AddMinutes(30));

        var options = new BlaaizOptions();
        options.CryptoNetworkMappings[setup.NetworkCode] = "MATIC_MAINNET";

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(options));

        var service = CreateDigitalAssetService(
            scope,
            db,
            setup.ProfileId,
            provider);

        var created = await service.CreateDepositIntentAsync(
            setup.CustomerId,
            new CreateDigitalAssetDepositIntentRequestDto(
                setup.AccountId,
                setup.NetworkId,
                25m,
                provider.ProviderCode));

        var occurredAt = DateTime.UtcNow;

        var handled = await service.TryProcessProviderCollectionAsync(
            provider.ProviderCode,
            created.ProviderCollectionId!,
            "collection-ref-completed",
            "COMPLETED",
            setup.AssetCode,
            25m,
            "{}",
            occurredAt);

        Assert.True(handled);

        db.ChangeTracker.Clear();

        var accountAfterFirst = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(125m, accountAfterFirst.AvailableBalance);
        Assert.Equal(125m, accountAfterFirst.SettledBalance);

        var intentAfterFirst = await db.DigitalAssetDepositIntents.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(CollectionStatus.Successful, intentAfterFirst.Status);
        Assert.NotNull(intentAfterFirst.CollectionId);
        Assert.NotNull(intentAfterFirst.CompletedAt);

        var collectionCountAfterFirst = await db.Collections.AsNoTracking()
            .CountAsync(x =>
                x.RelatedEntityType == nameof(DigitalAssetDepositIntent) &&
                x.RelatedEntityId == created.Id);

        var ledgerCountAfterFirst = await db.LedgerTransactions.AsNoTracking()
            .CountAsync(x =>
                x.IdempotencyScope == $"DigitalAssetCollection:{provider.ProviderCode}" &&
                x.IdempotencyKey == created.ProviderCollectionId);

        Assert.Equal(1, collectionCountAfterFirst);
        Assert.Equal(1, ledgerCountAfterFirst);

        var replayHandled = await service.TryProcessProviderCollectionAsync(
            provider.ProviderCode,
            created.ProviderCollectionId!,
            "collection-ref-completed",
            "COMPLETED",
            setup.AssetCode,
            25m,
            "{}",
            occurredAt.AddSeconds(10));

        Assert.True(replayHandled);

        db.ChangeTracker.Clear();

        var accountAfterReplay = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(125m, accountAfterReplay.AvailableBalance);
        Assert.Equal(125m, accountAfterReplay.SettledBalance);

        Assert.Equal(
            1,
            await db.Collections.AsNoTracking()
                .CountAsync(x =>
                    x.RelatedEntityType == nameof(DigitalAssetDepositIntent) &&
                    x.RelatedEntityId == created.Id));

        Assert.Equal(
            1,
            await db.LedgerTransactions.AsNoTracking()
                .CountAsync(x =>
                    x.IdempotencyScope == $"DigitalAssetCollection:{provider.ProviderCode}" &&
                    x.IdempotencyKey == created.ProviderCollectionId));
    }

    [DatabaseIntegrationFact]
    public async Task Blaaiz_deposit_webhook_amount_mismatch_is_rejected_without_credit()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var setup = await CreateScenarioAsync(
            db,
            "blaaiz-deposit-mismatch",
            100m,
            "POLYGON",
            "VERIFIED",
            "USDC");

        var (api, proxy) = CreateFakeApi();
        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-usdc", setup.AssetCode, "500", true));
        var providerCollectionId = $"collection-mismatch-{Guid.NewGuid():N}";
        proxy.CryptoCollectionResponse = CollectionResponse(
            providerCollectionId,
            $"collection-ref-{Guid.NewGuid():N}",
            setup.AssetCode,
            25m,
            "MATIC_MAINNET",
            "0xcollectionmismatch",
            "PENDING",
            DateTime.UtcNow.AddMinutes(30));

        var options = new BlaaizOptions();
        options.CryptoNetworkMappings[setup.NetworkCode] = "MATIC_MAINNET";

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(options));

        var service = CreateDigitalAssetService(
            scope,
            db,
            setup.ProfileId,
            provider);

        var created = await service.CreateDepositIntentAsync(
            setup.CustomerId,
            new CreateDigitalAssetDepositIntentRequestDto(
                setup.AccountId,
                setup.NetworkId,
                25m,
                provider.ProviderCode));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TryProcessProviderCollectionAsync(
                provider.ProviderCode,
                created.ProviderCollectionId!,
                "collection-ref-mismatch",
                "COMPLETED",
                setup.AssetCode,
                24.99m,
                "{}",
                DateTime.UtcNow));

        Assert.Contains("amount", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(100m, account.AvailableBalance);
        Assert.Equal(100m, account.SettledBalance);

        Assert.Equal(
            0,
            await db.Collections.AsNoTracking()
                .CountAsync(x =>
                    x.RelatedEntityType == nameof(DigitalAssetDepositIntent) &&
                    x.RelatedEntityId == created.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Failed_blaaiz_deposit_intent_marks_failed_without_credit()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var setup = await CreateScenarioAsync(
            db,
            "blaaiz-deposit-failed",
            100m,
            "POLYGON",
            "VERIFIED",
            "USDC");

        var (api, proxy) = CreateFakeApi();
        proxy.CryptoWallets = CryptoWalletResponse(
            ("wallet-usdc", setup.AssetCode, "500", true));
        var providerCollectionId = $"collection-failed-{Guid.NewGuid():N}";
        proxy.CryptoCollectionResponse = CollectionResponse(
            providerCollectionId,
            $"collection-ref-{Guid.NewGuid():N}",
            setup.AssetCode,
            25m,
            "MATIC_MAINNET",
            "0xcollectionfailed",
            "PENDING",
            DateTime.UtcNow.AddMinutes(30));

        var options = new BlaaizOptions();
        options.CryptoNetworkMappings[setup.NetworkCode] = "MATIC_MAINNET";

        var provider = new BlaaizDigitalAssetProvider(
            db,
            api,
            Options.Create(options));

        var service = CreateDigitalAssetService(
            scope,
            db,
            setup.ProfileId,
            provider);

        var created = await service.CreateDepositIntentAsync(
            setup.CustomerId,
            new CreateDigitalAssetDepositIntentRequestDto(
                setup.AccountId,
                setup.NetworkId,
                25m,
                provider.ProviderCode));

        Assert.True(await service.TryProcessProviderCollectionAsync(
            provider.ProviderCode,
            created.ProviderCollectionId!,
            "collection-ref-failed",
            "FAILED",
            setup.AssetCode,
            25m,
            "{}",
            DateTime.UtcNow));

        db.ChangeTracker.Clear();

        var intent = await db.DigitalAssetDepositIntents.AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        Assert.Equal(CollectionStatus.Failed, intent.Status);
        Assert.NotNull(intent.FailedAt);
        Assert.Contains("FAILED", intent.FailureReason ?? "", StringComparison.OrdinalIgnoreCase);

        var account = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == setup.AccountId);

        Assert.Equal(100m, account.AvailableBalance);
        Assert.Equal(100m, account.SettledBalance);
    }

    private DigitalAssetService CreateDigitalAssetService(
        AsyncServiceScope scope,
        AppDbContext db,
        Guid businessProfileId,
        IDigitalAssetProvider provider)
    {
        return new DigitalAssetService(
            db,
            new FixedEmbeddedContextAccessor(
                new EmbeddedFinancePrincipal(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    businessProfileId,
                    EmbeddedFinanceScope.DigitalAssetsRead |
                    EmbeddedFinanceScope.DigitalAssetsWrite,
                    "blaaiz-digital-asset-test-key")),
            new DigitalAssetProviderRegistry(new[] { provider }),
            scope.ServiceProvider.GetRequiredService<IFinancialReservationService>(),
            new DigitalAssetComplianceGate(),
            scope.ServiceProvider.GetRequiredService<IEmbeddedWebhookPublisher>());
    }

    private async Task<Scenario> CreateScenarioAsync(
        AppDbContext db,
        string label,
        decimal balance,
        string networkCode,
        string providerStatus,
        string assetCode)
    {
        using var client = _fixture.CreateClient();
        var unique = Guid.NewGuid().ToString("N");

        var registration = await client.RegisterAndConfirmAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Blaaiz",
                "DigitalAssetOwner",
                $"{label}-{unique}@example.test",
                "ReleaseCandidate!123",
                "+12145550241",
                "CA",
                UserType.Business));

        db.ChangeTracker.Clear();

        var profile = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = $"Blaaiz Digital Asset Test {unique}",
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
            DisplayName = $"Blaaiz Digital Asset Customer {unique}",
            Email = $"customer-{unique}@example.test",
            CountryCode = "CA",
            Status = BusinessCustomerStatus.Active
        };

        var asset = await db.Assets
            .FirstOrDefaultAsync(x => x.Code == assetCode);

        var addAsset = asset is null;

        AssetType? originalAssetType = null;
        bool? originalIsSupported = null;
        bool? originalDepositEnabled = null;
        bool? originalWithdrawalEnabled = null;

        if (asset is null)
        {
            asset = new Asset
            {
                Code = assetCode,
                Name = $"Blaaiz Test Stablecoin {assetCode}",
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
        }
        else
        {
            originalAssetType = asset.Type;
            originalIsSupported = asset.IsSupported;
            originalDepositEnabled = asset.DepositEnabled;
            originalWithdrawalEnabled = asset.WithdrawalEnabled;

            asset.Type = AssetType.Crypto;
            asset.IsSupported = true;
            asset.DepositEnabled = true;
            asset.WithdrawalEnabled = true;
        }

        var scenarioNetworkCode =
            $"{networkCode}-{unique[..8]}".ToUpperInvariant();

        var network = new AssetNetwork
        {
            Asset = asset,
            AssetCode = assetCode,
            NetworkCode = scenarioNetworkCode,
            Name = $"Blaaiz {scenarioNetworkCode} Test Network",
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

        var providerCustomerId = $"blaaiz-customer-{unique}";

        var providerCustomer = new ProviderCustomer
        {
            BusinessProfile = profile,
            BusinessProfileId = profile.Id,
            BusinessCustomer = customer,
            BusinessCustomerId = customer.Id,
            ProviderCode = KorridorX.Models.Enums.ProviderCode.Blaaiz,
            ProviderCustomerId = providerCustomerId,
            ProviderStatus = providerStatus,
            LastSyncedAt = DateTime.UtcNow
        };

        var countryAsset = await db.CountryAssets
            .FirstOrDefaultAsync(x =>
                x.CountryCode == customer.CountryCode &&
                x.AssetCode == assetCode);

        var countryAssetWasCreated = countryAsset is null;

        bool? originalCountryCanDeposit = null;
        bool? originalCountryCanWithdraw = null;
        bool? originalCountryCanTrade = null;
        bool? originalCountryCanUseInstant = null;

        if (countryAsset is null)
        {
            countryAsset = new CountryAsset
            {
                CountryCode = customer.CountryCode,
                Asset = asset,
                AssetCode = assetCode,
                CanDeposit = true,
                CanWithdraw = true,
                CanTrade = true,
                CanUseInstant = true
            };

            db.CountryAssets.Add(countryAsset);
        }
        else
        {
            originalCountryCanDeposit = countryAsset.CanDeposit;
            originalCountryCanWithdraw = countryAsset.CanWithdraw;
            originalCountryCanTrade = countryAsset.CanTrade;
            originalCountryCanUseInstant = countryAsset.CanUseInstant;

            countryAsset.CanDeposit = true;
            countryAsset.CanWithdraw = true;
            countryAsset.CanTrade = true;
            countryAsset.CanUseInstant = true;
        }

        db.BusinessProfiles.Add(profile);
        db.BusinessCustomers.Add(customer);

        if (addAsset)
            db.Assets.Add(asset);

        db.AssetNetworks.Add(network);
        db.FinancialAccounts.Add(account);
        db.ProviderCustomers.Add(providerCustomer);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new Scenario(
            db,
            profile.Id,
            customer.Id,
            account.Id,
            assetCode,
            network.Id,
            scenarioNetworkCode,
            providerCustomerId,
            addAsset,
            originalAssetType,
            originalIsSupported,
            originalDepositEnabled,
            originalWithdrawalEnabled,
            countryAssetWasCreated,
            originalCountryCanDeposit,
            originalCountryCanWithdraw,
            originalCountryCanTrade,
            originalCountryCanUseInstant);
    }

    private static BlaaizCryptoWalletListResponse CryptoWalletResponse(
        params (string Id, string AssetCode, string Balance, bool IsActive)[] wallets)
    {
        return new BlaaizCryptoWalletListResponse
        {
            Message = "ok",
            Data = wallets.Select((x, index) =>
                new BlaaizCryptoWalletData
                {
                    Id = x.Id,
                    Asset = new BlaaizCryptoWalletAssetData
                    {
                        Id = index + 1,
                        Symbol = x.AssetCode,
                        Name = x.AssetCode,
                        Decimals = 6
                    },
                    Balance = x.Balance,
                    IsActive = x.IsActive
                }).ToList()
        };
    }

    private static BlaaizCryptoCollectionResponse CollectionResponse(
        string transactionId,
        string reference,
        string token,
        decimal amount,
        string network,
        string address,
        string status,
        DateTime? expiresAt)
    {
        return new BlaaizCryptoCollectionResponse
        {
            Message = "ok",
            Transaction = new BlaaizCryptoCollectionTransaction
            {
                TransactionId = transactionId,
                Reference = reference,
                Token = token,
                TokenAmount = amount,
                Network = network,
                Address = address,
                Status = status,
                ExpiresAt = expiresAt
            }
        };
    }

    private static (IBlaaizApiClient Api, FakeBlaaizApiProxy Proxy) CreateFakeApi()
    {
        var api = DispatchProxy.Create<IBlaaizApiClient, FakeBlaaizApiProxy>();
        return (api, (FakeBlaaizApiProxy)(object)api);
    }

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }

    private sealed class Scenario : IAsyncDisposable
    {
        private readonly AppDbContext _db;
        private readonly bool _assetWasCreated;
        private readonly AssetType? _originalAssetType;
        private readonly bool? _originalIsSupported;
        private readonly bool? _originalDepositEnabled;
        private readonly bool? _originalWithdrawalEnabled;
        private readonly bool _countryAssetWasCreated;
        private readonly bool? _originalCountryCanDeposit;
        private readonly bool? _originalCountryCanWithdraw;
        private readonly bool? _originalCountryCanTrade;
        private readonly bool? _originalCountryCanUseInstant;
        private bool _disposed;

        public Scenario(
            AppDbContext db,
            Guid profileId,
            Guid customerId,
            Guid accountId,
            string assetCode,
            Guid networkId,
            string networkCode,
            string providerCustomerId,
            bool assetWasCreated,
            AssetType? originalAssetType,
            bool? originalIsSupported,
            bool? originalDepositEnabled,
            bool? originalWithdrawalEnabled,
            bool countryAssetWasCreated,
            bool? originalCountryCanDeposit,
            bool? originalCountryCanWithdraw,
            bool? originalCountryCanTrade,
            bool? originalCountryCanUseInstant)
        {
            _db = db;
            ProfileId = profileId;
            CustomerId = customerId;
            AccountId = accountId;
            AssetCode = assetCode;
            NetworkId = networkId;
            NetworkCode = networkCode;
            ProviderCustomerId = providerCustomerId;
            _assetWasCreated = assetWasCreated;
            _originalAssetType = originalAssetType;
            _originalIsSupported = originalIsSupported;
            _originalDepositEnabled = originalDepositEnabled;
            _originalWithdrawalEnabled = originalWithdrawalEnabled;
            _countryAssetWasCreated = countryAssetWasCreated;
            _originalCountryCanDeposit = originalCountryCanDeposit;
            _originalCountryCanWithdraw = originalCountryCanWithdraw;
            _originalCountryCanTrade = originalCountryCanTrade;
            _originalCountryCanUseInstant = originalCountryCanUseInstant;
        }

        public Guid ProfileId { get; }
        public Guid CustomerId { get; }
        public Guid AccountId { get; }
        public string AssetCode { get; }
        public Guid NetworkId { get; }
        public string NetworkCode { get; }
        public string ProviderCustomerId { get; }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            _db.ChangeTracker.Clear();

            var countryAsset = await _db.CountryAssets
                .FirstOrDefaultAsync(x =>
                    x.CountryCode == "CA" &&
                    x.AssetCode == AssetCode);

            if (countryAsset is not null)
            {
                if (_countryAssetWasCreated)
                {
                    _db.CountryAssets.Remove(countryAsset);
                }
                else
                {
                    if (_originalCountryCanDeposit.HasValue)
                        countryAsset.CanDeposit = _originalCountryCanDeposit.Value;

                    if (_originalCountryCanWithdraw.HasValue)
                        countryAsset.CanWithdraw = _originalCountryCanWithdraw.Value;

                    if (_originalCountryCanTrade.HasValue)
                        countryAsset.CanTrade = _originalCountryCanTrade.Value;

                    if (_originalCountryCanUseInstant.HasValue)
                        countryAsset.CanUseInstant = _originalCountryCanUseInstant.Value;
                }
            }

            if (!_assetWasCreated)
            {
                var asset = await _db.Assets
                    .FirstOrDefaultAsync(x => x.Code == AssetCode);

                if (asset is not null)
                {
                    if (_originalAssetType.HasValue)
                        asset.Type = _originalAssetType.Value;

                    if (_originalIsSupported.HasValue)
                        asset.IsSupported = _originalIsSupported.Value;

                    if (_originalDepositEnabled.HasValue)
                        asset.DepositEnabled = _originalDepositEnabled.Value;

                    if (_originalWithdrawalEnabled.HasValue)
                        asset.WithdrawalEnabled = _originalWithdrawalEnabled.Value;
                }
            }

            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }
    }

    private sealed class FixedEmbeddedContextAccessor :
        IEmbeddedFinanceContextAccessor
    {
        private readonly EmbeddedFinancePrincipal _principal;

        public FixedEmbeddedContextAccessor(
            EmbeddedFinancePrincipal principal)
        {
            _principal = principal;
        }

        public EmbeddedFinancePrincipal GetRequiredPrincipal() =>
            _principal;
    }

    public class FakeBlaaizApiProxy : DispatchProxy
    {
        public BlaaizCryptoWalletListResponse CryptoWallets { get; set; } =
            new();

        public BlaaizCryptoPayoutResponse CryptoPayoutResponse { get; set; } =
            new()
            {
                Message = "ok",
                Data = new BlaaizCryptoPayoutData
                {
                    Id = "crypto-tx-default",
                    Reference = "crypto-ref-default",
                    Status = "PENDING"
                }
            };

        public BlaaizCryptoCollectionResponse CryptoCollectionResponse { get; set; } =
            new()
            {
                Message = "ok",
                Transaction = new BlaaizCryptoCollectionTransaction
                {
                    TransactionId = "collection-tx-default",
                    Reference = "collection-ref-default",
                    Token = "USDC",
                    TokenAmount = 1m,
                    Network = "MATIC_MAINNET",
                    Address = "0xcollectiondefault",
                    Status = "PENDING"
                }
            };

        public int CryptoWalletCalls { get; private set; }
        public int PayoutCalls { get; private set; }
        public int CollectionCalls { get; private set; }

        public BlaaizCryptoPayoutRequest? LastCryptoPayoutRequest { get; private set; }
        public string? LastIdempotencyKey { get; private set; }
        public Guid? LastPayoutId { get; private set; }

        public BlaaizCryptoCollectionRequest? LastCryptoCollectionRequest { get; private set; }
        public Guid? LastDepositIntentId { get; private set; }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException(
                    "Blaaiz API proxy method is unavailable.");

            switch (targetMethod.Name)
            {
                case nameof(IBlaaizApiClient.ListCryptoWalletsAsync):
                    CryptoWalletCalls++;
                    return Task.FromResult(
                        new BlaaizApiResult<BlaaizCryptoWalletListResponse>(
                            CryptoWallets,
                            "{}",
                            Guid.NewGuid()));

                case nameof(IBlaaizApiClient.InitiateCryptoPayoutAsync):
                    PayoutCalls++;
                    LastCryptoPayoutRequest =
                        (BlaaizCryptoPayoutRequest)args![0]!;
                    LastIdempotencyKey = (string)args[1]!;
                    LastPayoutId = (Guid)args[2]!;

                    return Task.FromResult(
                        new BlaaizApiResult<BlaaizCryptoPayoutResponse>(
                            CryptoPayoutResponse,
                            "{}",
                            Guid.NewGuid()));

                case nameof(IBlaaizApiClient.InitiateCryptoCollectionAsync):
                    CollectionCalls++;
                    LastCryptoCollectionRequest =
                        (BlaaizCryptoCollectionRequest)args![0]!;
                    LastDepositIntentId = (Guid)args[1]!;

                    return Task.FromResult(
                        new BlaaizApiResult<BlaaizCryptoCollectionResponse>(
                            CryptoCollectionResponse,
                            "{}",
                            Guid.NewGuid()));

                default:
                    throw new NotSupportedException(
                        $"Unexpected Blaaiz API test call: {targetMethod.Name}.");
            }
        }
    }

    private sealed class FakeTokenService : IBlaaizTokenService
    {
        public Task<string> GetAccessTokenAsync(
            CancellationToken ct = default) =>
            Task.FromResult("test-access-token");
    }

    private sealed class RecordingAuditService :
        IProviderRequestAuditService
    {
        public Guid LogId { get; } = Guid.NewGuid();
        public string? Endpoint { get; private set; }
        public string? HttpMethod { get; private set; }
        public Guid? RelatedPayoutId { get; private set; }
        public int CompleteCalls { get; private set; }
        public int FailCalls { get; private set; }

        public Task<Guid> StartAsync(
            KorridorX.Models.Enums.ProviderCode providerCode,
            string endpoint,
            string httpMethod,
            string? requestHeadersJson,
            string? requestBodyJson,
            Guid? relatedTransferId = null,
            Guid? relatedCollectionId = null,
            Guid? relatedPayoutId = null,
            CancellationToken ct = default)
        {
            Endpoint = endpoint;
            HttpMethod = httpMethod;
            RelatedPayoutId = relatedPayoutId;
            return Task.FromResult(LogId);
        }

        public Task CompleteAsync(
            Guid requestLogId,
            int responseStatusCode,
            string? responseBodyJson,
            long durationMs,
            CancellationToken ct = default)
        {
            CompleteCalls++;
            return Task.CompletedTask;
        }

        public Task FailAsync(
            Guid requestLogId,
            int? responseStatusCode,
            string? responseBodyJson,
            string errorMessage,
            long durationMs,
            CancellationToken ct = default)
        {
            FailCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHttpMessageHandler :
        HttpMessageHandler
    {
        private readonly string _responseJson;

        public RecordingHttpMessageHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Authorization { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            IdempotencyKey = request.Headers
                .TryGetValues("Idempotency-Key", out var values)
                ? values.SingleOrDefault()
                : null;

            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseJson,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
