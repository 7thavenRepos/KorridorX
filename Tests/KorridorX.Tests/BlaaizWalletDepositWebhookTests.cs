using System.Reflection;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Models.Notifications;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Webhooks;
using KorridorX.Providers.Remittance;
using KorridorX.Services.DigitalAssets;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using KorridorX.Services.Wallets;
using KorridorX.Services.Webhooks;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

public sealed class BlaaizWalletDepositIdentityTests
{
    private static string? Read(string method, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return (string?)typeof(BlaaizWebhookService).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [root, root]);
    }

    [Theory]
    [InlineData("{\"virtual_bank_account\":{\"id\":\"account-test\",\"account_number\":\"0123456789\"}}")]
    [InlineData("{\"data\":{\"virtual_bank_account\":{\"id\":\"account-test\",\"account_number\":\"0123456789\"}}}")]
    [InlineData("{\"data\":{\"transaction\":{\"virtual_bank_account\":{\"id\":\"account-test\",\"account_number\":\"0123456789\"}}}}")]
    [InlineData("{\"virtual_bank_account_id\":\"account-test\",\"account_number\":\"0123456789\"}")]
    public void Explicit_receiving_identity_supports_flat_and_nested_payloads(string json)
    {
        Assert.Equal("account-test", Read("ReadVirtualBankAccountId", json));
        Assert.Equal("0123456789", Read("ReadVirtualBankAccountNumber", json));
    }

    [Theory]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account_id\":\"one\",\"virtual_bank_account\":{\"id\":\"two\"}}")]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account_id\":\"one\",\"bank_account_id\":\"two\"}")]
    [InlineData("ReadVirtualBankAccountNumber", "{\"account_number\":\"0123456789\",\"virtual_bank_account\":{\"account_number\":\"9876543210\"}}")]
    [InlineData("ReadVirtualBankAccountId", "{\"data\":{\"virtual_bank_account\":{\"id\":\"one\"}},\"virtual_bank_account\":{\"id\":\"two\"}}")]
    public void Contradictory_receiving_identity_is_rejected(string method, string json)
    {
        var error = Assert.Throws<TargetInvocationException>(() => Read(method, json));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("conflicting", error.InnerException!.Message);
    }

    [Theory]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account\":{\"id\":12345}}")]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account\":{\"id\":true}}")]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account\":{\"id\":null}}")]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account\":{\"id\":\"\"}}")]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account\":{\"id\":\"  \"}}")]
    [InlineData("ReadVirtualBankAccountId", "{\"virtual_bank_account_id\":\"legacy-account\",\"virtual_bank_account\":{\"id\":{}}}")]
    [InlineData("ReadVirtualBankAccountNumber", "{\"virtual_bank_account\":{\"account_number\":1234567890}}")]
    [InlineData("ReadVirtualBankAccountNumber", "{\"virtual_bank_account\":{\"account_number\":false}}")]
    [InlineData("ReadVirtualBankAccountNumber", "{\"virtual_bank_account\":{\"account_number\":\"\"}}")]
    [InlineData("ReadVirtualBankAccountNumber", "{\"virtual_bank_account\":{\"account_number\":\"  \"}}")]
    [InlineData("ReadVirtualBankAccountNumber", "{\"account_number\":\"0123456789\",\"virtual_bank_account\":{\"account_number\":[]}}")]
    public void Malformed_explicit_nested_identity_is_rejected_without_legacy_scalar_coercion(string method, string json)
    {
        var error = Assert.Throws<TargetInvocationException>(() => Read(method, json));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("nonempty string", error.InnerException!.Message);
    }

    [Fact]
    public void Optional_null_local_account_number_does_not_discard_explicit_receiving_account_id()
    {
        const string json = "{\"virtual_bank_account\":{\"id\":\"iban-only-account\",\"account_number\":null}}";
        Assert.Equal("iban-only-account", Read("ReadVirtualBankAccountId", json));
        Assert.Null(Read("ReadVirtualBankAccountNumber", json));
    }

    [Fact]
    public void Source_bank_and_transaction_identity_never_substitute_for_receiving_account()
    {
        const string json = """
            {"transaction_id":"transaction-test","source_information":{"id":"sender-bank", "account_number":"9876543210",
              "virtual_bank_account":{"id":"sender-account","account_number":"9876543210"}}}
            """;
        Assert.Null(Read("ReadVirtualBankAccountId", json));
        Assert.Null(Read("ReadVirtualBankAccountNumber", json));
        using var document = JsonDocument.Parse("{\"virtual_bank_account\":{\"id\":\"account-test\"}}");
        var root = document.RootElement;
        var transaction = typeof(BlaaizWebhookService).GetMethod("ReadTransactionId", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [root, root, new[] { "transaction_id", "provider_transaction_id", "collection_id" }]);
        Assert.Null(transaction);
    }

    [Fact]
    public void Matching_flat_and_nested_evidence_is_accepted_without_using_merchant_reference()
    {
        const string json = """
            {"merchant_reference":null,"virtual_bank_account_id":"account-test","account_number":"0123456789",
             "virtual_bank_account":{"id":"account-test","account_number":"0123456789","account_reference":"bank-reference"}}
            """;
        Assert.Equal("account-test", Read("ReadVirtualBankAccountId", json));
        Assert.Equal("0123456789", Read("ReadVirtualBankAccountNumber", json));
    }

    [Fact]
    public void Legacy_numeric_flat_aliases_keep_their_existing_string_representation()
    {
        const string json = "{\"virtual_bank_account_id\":12345,\"account_number\":1234567890}";
        Assert.Equal("12345", Read("ReadVirtualBankAccountId", json));
        Assert.Equal("1234567890", Read("ReadVirtualBankAccountNumber", json));
    }
}

// Signed synthetic provider callbacks and a disposable PostgreSQL database only.
// Provider verification, SMTP and Firebase never make external requests here.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BlaaizWalletDepositWebhookTests(ReleaseCandidateDatabaseFixture fixture)
{
    private const string Secret = "test-only-nested-bank-deposit-signing-secret";

    [DatabaseIntegrationFact]
    public async Task Flat_collection_with_nested_virtual_bank_account_credits_and_notifies_once()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var payload = Payload(scenario);

        var first = await Signed(scenario.Service, payload);
        var duplicate = await Signed(scenario.Service, payload);
        var redelivery = await Signed(scenario.Service, Payload(scenario, eventId: Guid.NewGuid().ToString()));

        Assert.Equal("collection.completed", first.EventType);
        Assert.Equal("Processed", first.ProcessingStatus);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal("Processed", redelivery.ProcessingStatus);
        Assert.Equal(0, scenario.Embedded.Calls);
        Assert.Equal(2, scenario.Provider.VerificationCalls);
        Assert.Equal(1000m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == scenario.Wallet.Id)).AvailableBalance);
        var collection = Assert.Single(await db.Collections.AsNoTracking().Where(x => x.FinancialAccountId == scenario.Wallet.Id).ToListAsync());
        Assert.Equal(scenario.Bank.Id, collection.RelatedEntityId);
        Assert.Equal(scenario.TransactionId, collection.ProviderCollectionId);
        Assert.Single(await db.LedgerTransactions.Where(x => x.RelatedEntityId == collection.Id).ToListAsync());
        Assert.Single(await db.ProviderTransactions.Where(x => x.ProviderTransactionId == scenario.TransactionId).ToListAsync());
        var notices = await db.NotificationMessages.Where(x => x.UserId == scenario.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType).ToListAsync();
        Assert.Equal(2, notices.Count);
        var email = Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Email));
        Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Push));
        Assert.Contains("NGN 1,000.00", email.Body);
        Assert.All(notices, x => Assert.Equal(collection.Id.ToString(), x.RelatedEntityId));
        var feed = await scenario.Notifications.GetMyNotificationsAsync(scenario.UserId, 1, 20);
        Assert.Single(feed.Items.Where(x => x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
    }

    [DatabaseIntegrationFact]
    public async Task Unknown_nested_account_stays_audited_and_cannot_use_the_matching_customer_or_source_bank()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var unknown = Guid.NewGuid().ToString();
        var payload = Payload(scenario, accountId: unknown);
        var result = await Signed(scenario.Service, payload);
        var repeated = await Signed(scenario.Service, payload);
        Assert.Equal("Ignored", result.ProcessingStatus);
        Assert.Equal("Ignored", repeated.ProcessingStatus);
        Assert.Equal(unknown, scenario.Embedded.LastAccountId);
        Assert.Equal(0, scenario.Provider.VerificationCalls);
        Assert.Equal(payload, (await db.WebhookEvents.AsNoTracking().SingleAsync(x => x.Id == result.WebhookEventId)).RawPayloadJson);
        await AssertNoCreditOrFundingNotice(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Fresh_signed_redelivery_of_retained_ignored_bank_event_recovers_once_with_audited_attempts()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var payload = Payload(scenario);
        var previous = await RetainIgnored(db, scenario, payload);

        var recovered = await Signed(scenario.Service, payload);
        var duplicate = await Signed(scenario.Service, payload);

        Assert.Equal(previous.Id, recovered.WebhookEventId);
        Assert.Equal("Processed", recovered.ProcessingStatus);
        Assert.False(recovered.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(1, scenario.Provider.VerificationCalls);
        var receipt = await db.WebhookEvents.AsNoTracking().Include(x => x.Attempts).SingleAsync(x => x.Id == previous.Id);
        Assert.Equal(payload, receipt.RawPayloadJson);
        Assert.Equal(2, receipt.Attempts.Count);
        Assert.Contains(receipt.Attempts, x => x.Status == WebhookProcessingStatus.Ignored);
        Assert.Contains(receipt.Attempts, x => x.Status == WebhookProcessingStatus.Processed);
        Assert.Equal(1000m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == scenario.Wallet.Id)).AvailableBalance);
        var notices = await db.NotificationMessages.Where(x => x.UserId == scenario.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType).ToListAsync();
        Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Email));
        Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Push));
    }

    [DatabaseIntegrationFact]
    public async Task Ignored_recovery_requires_same_payload_and_a_fresh_valid_signature()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var payload = Payload(scenario);
        var previous = await RetainIgnored(db, scenario, payload);
        var stale = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Service.ProcessCollectionWebhookAsync(
            payload, BlaaizWebhookSignature.Create(stale, payload, Secret), stale));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Signed(scenario.Service,
            Payload(scenario, accountId: Guid.NewGuid().ToString())));
        Assert.Equal(0, scenario.Provider.VerificationCalls);
        var receipt = await db.WebhookEvents.AsNoTracking().Include(x => x.Attempts).SingleAsync(x => x.Id == previous.Id);
        Assert.Equal(WebhookProcessingStatus.Ignored, receipt.ProcessingStatus);
        Assert.Single(receipt.Attempts);
        await AssertNoCreditOrFundingNotice(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Ignored_non_bank_collection_is_not_retried_by_the_bank_recovery_rule()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var payload = JsonSerializer.Serialize(new { event_id = scenario.EventId, type = "collection", transaction_status = "SUCCESSFUL",
            transaction_id = scenario.TransactionId, transaction_amount = 1000, transaction_currency = "NGN", customer_id = scenario.Bank.ProviderCustomerId });
        var previous = await RetainIgnored(db, scenario, payload);
        var result = await Signed(scenario.Service, payload);
        Assert.True(result.IsDuplicate);
        Assert.Equal("Ignored", result.ProcessingStatus);
        Assert.Equal(0, scenario.Provider.VerificationCalls);
        Assert.Single(await db.Set<WebhookProcessingAttempt>().Where(x => x.WebhookEventId == previous.Id).ToListAsync());
        await AssertNoCreditOrFundingNotice(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Invalid_signature_cannot_persist_or_route_nested_bank_deposit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scenario.Service.ProcessCollectionWebhookAsync(
            Payload(scenario), "invalid-signature", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
        Assert.False(await db.WebhookEvents.AnyAsync(x => x.ProviderEventId == scenario.EventId));
        Assert.Equal(0, scenario.Provider.VerificationCalls);
        await AssertNoCreditOrFundingNotice(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_verification_mismatch_retains_failed_event_without_credit_or_notice()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        scenario.Provider.Result = scenario.Provider.Result with { Amount = 1001m };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Signed(scenario.Service, Payload(scenario)));
        Assert.Equal(WebhookProcessingStatus.Failed, (await db.WebhookEvents.AsNoTracking()
            .SingleAsync(x => x.ProviderEventId == scenario.EventId)).ProcessingStatus);
        Assert.Equal(1, scenario.Provider.VerificationCalls);
        await AssertNoCreditOrFundingNotice(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Conflicting_flat_and_nested_accounts_fail_before_provider_or_credit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var payload = Payload(scenario, flatAccountId: Guid.NewGuid().ToString());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Signed(scenario.Service, payload));
        Assert.Contains("conflicting", error.Message);
        Assert.Equal(WebhookProcessingStatus.Failed, (await db.WebhookEvents.AsNoTracking()
            .SingleAsync(x => x.ProviderEventId == scenario.EventId)).ProcessingStatus);
        Assert.Equal(0, scenario.Provider.VerificationCalls);
        await AssertNoCreditOrFundingNotice(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Prior_connection_account_is_not_reassigned_by_nested_identity()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        scenario.Bank.ConnectionKey = new string('D', 64);
        await db.SaveChangesAsync();
        var result = await Signed(scenario.Service, Payload(scenario));
        Assert.Equal("Ignored", result.ProcessingStatus);
        Assert.Equal(0, scenario.Provider.VerificationCalls);
        await AssertNoCreditOrFundingNotice(db, scenario);
    }

    private static async Task AssertNoCreditOrFundingNotice(AppDbContext db, Scenario scenario)
    {
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == scenario.Wallet.Id)).AvailableBalance);
        Assert.False(await db.Collections.AnyAsync(x => x.FinancialAccountId == scenario.Wallet.Id));
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == scenario.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
    }

    private static async Task<WebhookEvent> RetainIgnored(AppDbContext db, Scenario scenario, string payload)
    {
        var recordedAt = DateTime.UtcNow.AddHours(-1);
        var receipt = new WebhookEvent { ProviderCode = ProviderCode.Blaaiz, ProviderEventId = scenario.EventId,
            EventType = "collection.completed", RawPayloadJson = payload, ProcessingStatus = WebhookProcessingStatus.Ignored,
            ReceivedAt = recordedAt, ProcessedAt = recordedAt };
        receipt.Attempts.Add(new WebhookProcessingAttempt { WebhookEventId = receipt.Id,
            Status = WebhookProcessingStatus.Ignored, StartedAt = recordedAt, FinishedAt = recordedAt });
        db.WebhookEvents.Add(receipt);
        await db.SaveChangesAsync();
        return receipt;
    }

    private static async Task<Scenario> Setup(AppDbContext db)
    {
        var resolver = new Resolver();
        var suffix = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = userId, UserName = "nested-" + suffix,
            NormalizedUserName = "NESTED-" + suffix, Email = suffix + "@example.test", NormalizedEmail = suffix + "@EXAMPLE.TEST",
            EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString() });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = userId, CountryCode = "NG", FirstName = "Synthetic", LastName = "Customer", KycStatus = KycStatus.Approved });
        db.MobilePushDevices.Add(new MobilePushDevice { UserId = userId, Platform = "android", PushToken = "fake-" + suffix,
            PushTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix))),
            DeviceFingerprint = suffix, DeviceName = "Local test device" });
        var wallet = new FinancialAccount { OwnerType = FinancialAccountOwnerType.User, OwnerId = userId,
            AssetCode = "NGN", AccountCode = "nested-" + suffix, AccountType = FinancialAccountType.Customer, Status = FinancialAccountStatus.Active };
        var providerWallet = new ProviderWalletConfiguration { ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName,
            AssetCode = "NGN", ProviderWalletId = Guid.NewGuid().ToString(), DisplayName = "Synthetic NGN wallet", NetworkCode = "" };
        var channel = new PaymentChannel { Environment = resolver.EnvironmentName, AssetCode = "NGN", CountryCode = "*",
            Direction = PaymentChannelDirection.Deposit, CapabilityCode = "virtual-account", DisplayName = "Synthetic bank deposits",
            ProviderWalletConfigurationId = providerWallet.Id, IndividualEnabled = true, HasBeenActivated = true, Status = PaymentChannelStatus.Active };
        var bank = new WalletDepositAccount { FinancialAccountId = wallet.Id, PaymentChannelId = channel.Id,
            ProviderWalletConfigurationId = providerWallet.Id, ProviderWalletId = providerWallet.ProviderWalletId,
            ProviderCustomerId = Guid.NewGuid().ToString(), ProviderAccountId = Guid.NewGuid().ToString(),
            Environment = resolver.EnvironmentName, ConnectionKey = resolver.ConnectionKey, AssetCode = "NGN", Status = "Active",
            AccountNumber = "0123456789", AccountName = "Synthetic Customer", BankName = "Synthetic Bank", CreatedByUserId = userId };
        db.FinancialAccounts.Add(wallet); db.ProviderWalletConfigurations.Add(providerWallet); db.PaymentChannels.Add(channel); db.WalletDepositAccounts.Add(bank);
        await db.SaveChangesAsync();
        var transactionId = Guid.NewGuid().ToString();
        var provider = DispatchProxy.Create<IRemittanceProvider, ProviderProxy>();
        var proxy = (ProviderProxy)(object)provider;
        proxy.Result = new(transactionId, "synthetic-bank-reference", "collection", "SUCCESSFUL", "NGN", 1000m, null, null,
            JsonSerializer.Serialize(new { data = new { id = transactionId, customer_id = bank.ProviderCustomerId,
                wallet_id = bank.ProviderWalletId, virtual_bank_account_id = bank.ProviderAccountId } }), Guid.NewGuid());
        var notifications = new NotificationQueueService(db, Options.Create(new NotificationDeliveryOptions
            { Firebase = new FirebasePushDeliveryOptions { IsEnabled = true } }));
        var channels = new PaymentChannelService(db, resolver, Options.Create(new BlaaizOptions()), new CollectionPaymentMethodPolicy());
        var accounts = new WalletDepositAccountService(db, channels, null!, resolver, null!, provider,
            new ConsumerWalletFundingCreditService(db, notifications), notifications);
        var embedded = new UnmatchedEmbeddedDeposits();
        var interac = new InteracDepositService(db, resolver, null!, new ProviderCollectionCreditGuard(db, resolver, null!), null!);
        var service = new BlaaizWebhookService(db, Options.Create(new BlaaizOptions { IsEnabled = true, WebhookSigningSecret = Secret }),
            null!, null!, provider, null!, embedded, null!, new NoDigitalDeposits(), null!, accounts, interac, null!);
        return new(service, proxy, embedded, notifications, bank, wallet, userId, transactionId, Guid.NewGuid().ToString());
    }

    private static string Payload(Scenario scenario, string? accountId = null, string? eventId = null, string? flatAccountId = null) =>
        JsonSerializer.Serialize(new
        {
            message = "Transaction Completed", transaction_id = scenario.TransactionId,
            transaction_reference = "synthetic-bank-reference", merchant_reference = (string?)null,
            transaction_status = "SUCCESSFUL", transaction_fee = 0, transaction_amount_without_fee = 1000,
            transaction_amount = 1000, transaction_currency = "NGN", customer_id = scenario.Bank.ProviderCustomerId,
            virtual_bank_account_id = flatAccountId,
            virtual_bank_account = new { id = accountId ?? scenario.Bank.ProviderAccountId, account_number = scenario.Bank.AccountNumber,
                account_name = "Synthetic Customer", bank_name = "Synthetic Bank" },
            source_information = new { id = "synthetic-sender-bank", account_number = "9876543210", account_name = "Synthetic Sender" },
            event_id = eventId ?? scenario.EventId, type = "collection"
        });

    private static Task<BlaaizWebhookResult> Signed(BlaaizWebhookService service, string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        return service.ProcessCollectionWebhookAsync(payload, BlaaizWebhookSignature.Create(timestamp, payload, Secret), timestamp);
    }

    private sealed record Scenario(BlaaizWebhookService Service, ProviderProxy Provider, UnmatchedEmbeddedDeposits Embedded,
        NotificationQueueService Notifications, WalletDepositAccount Bank, FinancialAccount Wallet, Guid UserId,
        string TransactionId, string EventId);

    public class ProviderProxy : DispatchProxy
    {
        public RemittanceTransactionStatusResult Result { get; set; } = null!;
        public int VerificationCalls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRemittanceProvider.GetTransactionAsync))
                throw new InvalidOperationException("Unexpected provider operation in synthetic webhook test.");
            Assert.Equal(Result.ProviderTransactionId, args![0]);
            VerificationCalls++;
            return Task.FromResult(Result);
        }
    }

    private sealed class Resolver : IProviderWalletResolver
    {
        public string EnvironmentName { get; } = "NestedBankTest-" + Guid.NewGuid().ToString("N");
        public string ConnectionKey => new('C', 64);
        public Task<string> SelectAsync(string a, Guid b, string c, string d, string? e, string f, bool g, CancellationToken ct = default) =>
            throw new InvalidOperationException("No provider account creation is permitted in deposit webhook tests.");
    }

    private sealed class UnmatchedEmbeddedDeposits : IEmbeddedInboundCollectionService
    {
        public int Calls { get; private set; }
        public string? LastAccountId { get; private set; }
        public Task<bool> TryProcessBlaaizDepositAsync(EmbeddedInboundCollectionRequest request, CancellationToken ct = default)
        {
            Calls++; LastAccountId = request.ProviderAccountId;
            return Task.FromResult(false);
        }
    }

    private sealed class NoDigitalDeposits : IDigitalAssetDepositIntentSettlementService
    {
        public Task<bool> TryProcessProviderCollectionAsync(string providerCode, string providerCollectionId, string? providerReference,
            string providerStatus, string? assetCode, decimal? amount, string rawPayload, DateTime occurredAt, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
