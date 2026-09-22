using System.Reflection;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Audit;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Webhooks;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BlaaizVirtualAccountWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Pending_provision_is_single_flight_and_keeps_account_pending()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db);
        var provider = new StubProvisioner(s);
        var service = new CollectionAccountProvisioningService(db, new Context(s.Account.BusinessProfileId), [provider]);
        var first = await service.ProvisionAsync(s.Account.BusinessCustomerId, s.Account.Id, new("blaaiz"));
        var second = await service.ProvisionAsync(s.Account.BusinessCustomerId, s.Account.Id, new("Blaaiz"));
        Assert.Equal(1, provider.Calls);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(ProviderAccountMappingStatus.Pending, second.Status);
        Assert.Equal(CollectionAccountStatus.Pending, (await db.CollectionAccounts.FindAsync(s.Account.Id))!.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Ready_callback_before_POST_returns_cannot_be_overwritten_by_pending_response()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db);
        var provider = new StubProvisioner(s) { BeforeReturn = () => new BlaaizVirtualAccountWebhookHandler(db).ProcessAsync("virtual_account.ready", Payload(s)) };
        var service = new CollectionAccountProvisioningService(db, new Context(s.Account.BusinessProfileId), [provider]);
        var result = await service.ProvisionAsync(s.Account.BusinessCustomerId, s.Account.Id, new("Blaaiz"));
        Assert.Equal(ProviderAccountMappingStatus.Active, result.Status);
        Assert.Equal("12345678", result.AccountNumber);
        Assert.NotNull(result.BankDetails?.Iban);
        Assert.NotNull((await service.GetMappingsAsync(s.Account.BusinessCustomerId, s.Account.Id)).Single().BankDetails?.Iban);
        var admin = scope.ServiceProvider.GetRequiredService<IEmbeddedFinanceAdminQueryService>();
        Assert.NotNull((await admin.GetProviderMappingsAsync(s.Account.Id)).Single().BankDetails?.Iban);
        Assert.Equal(CollectionAccountStatus.Active, (await db.CollectionAccounts.FindAsync(s.Account.Id))!.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Signed_ready_duplicate_and_late_failure_emit_one_activation_without_money()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db, mapping: true);
        var service = Webhooks(db);
        var payload = Payload(s);
        var first = await Signed(service, payload);
        var replay = await Signed(service, payload);
        await Signed(service, Payload(s, "virtual_account.failed", "FAILED"));
        Assert.Equal("Processed", first.ProcessingStatus);
        Assert.True(replay.IsDuplicate);
        db.ChangeTracker.Clear();
        Assert.Equal(ProviderAccountMappingStatus.Active, (await db.ProviderAccountMappings.SingleAsync(x => x.CollectionAccountId == s.Account.Id)).Status);
        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x => x.BusinessProfileId == s.Account.BusinessProfileId && x.EventType == "account.status.changed"));
        Assert.Equal(0, await db.Collections.CountAsync(x => x.FinancialAccountId == s.Financial.Id));
        Assert.Equal(0m, (await db.FinancialAccounts.FindAsync(s.Financial.Id))!.AvailableBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Invalid_signature_and_wrong_wallet_cannot_activate_account()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db, mapping: true);
        var service = Webhooks(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ProcessCollectionWebhookAsync(Payload(s), "bad", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Signed(service, Payload(s).Replace(s.Wallet, "wrong-wallet")));
        db.ChangeTracker.Clear();
        Assert.Equal(CollectionAccountStatus.Pending, (await db.CollectionAccounts.FindAsync(s.Account.Id))!.Status);
        Assert.Equal(0, await db.BusinessWebhookEvents.CountAsync(x => x.BusinessProfileId == s.Account.BusinessProfileId));
    }

    [DatabaseIntegrationFact]
    public async Task Rejection_keeps_collection_account_pending_and_admin_retry_can_remain_pending()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db, mapping: true);
        await Signed(Webhooks(db), Payload(s, "virtual_account.rejected", "REJECTED"));
        db.ChangeTracker.Clear();
        var mapping = await db.ProviderAccountMappings.SingleAsync(x => x.CollectionAccountId == s.Account.Id);
        Assert.Equal(ProviderAccountMappingStatus.Failed, mapping.Status);
        var service = new EmbeddedFinanceAdminCommandService(db, scope.ServiceProvider.GetRequiredService<IAuditService>(),
            scope.ServiceProvider.GetRequiredService<IEmbeddedWebhookPublisher>(), [new StubProvisioner(s)]);
        var result = await service.RetryProviderMappingAsync(s.OwnerId, s.Account.Id, mapping.Id, "Retry after provider review.");
        Assert.Equal(ProviderAccountMappingStatus.Pending, result.Mapping.Status);
        Assert.Equal(CollectionAccountStatus.Pending, result.CollectionAccountStatus);
    }

    [DatabaseIntegrationFact]
    public async Task Failed_virtual_account_delivery_can_be_retried_after_correlation_is_repaired()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db, mapping: true);
        var customer = await db.ProviderCustomers.SingleAsync(x => x.ProviderCustomerId == s.Customer);
        customer.IsDeleted = true;
        await db.SaveChangesAsync();
        var service = Webhooks(db);
        var payload = Payload(s);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Signed(service, payload));
        customer.IsDeleted = false;
        await db.SaveChangesAsync();
        var result = await Signed(service, payload);
        Assert.Equal("Processed", result.ProcessingStatus);
        db.ChangeTracker.Clear();
        var receipt = await db.WebhookEvents.Include(x => x.Attempts).SingleAsync(x => x.Id == result.WebhookEventId);
        Assert.Equal(2, receipt.Attempts.Count);
        Assert.Equal(CollectionAccountStatus.Active, (await db.CollectionAccounts.FindAsync(s.Account.Id))!.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Verified_customer_is_required_before_EUR_provider_dispatch()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db);
        var customer = await db.ProviderCustomers.SingleAsync(x => x.ProviderCustomerId == s.Customer);
        customer.ProviderStatus = "PENDING";
        await db.SaveChangesAsync();
        var provisioner = new BlaaizCollectionAccountProvisioner(db, null!, null!);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync(new(
            s.Account.BusinessProfileId, s.Account.BusinessCustomerId, s.Account.Id, s.Account.ExternalReference,
            "Customer", null, null, "CA", "EUR")));
        Assert.Contains("VERIFIED", error.Message);
    }

    [DatabaseIntegrationFact]
    public async Task Earlier_EUR_request_does_not_block_new_GBP_wallet_selection()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db, "GBP");
        var otherWallet = "eur-" + Guid.NewGuid().ToString("N");
        db.ProviderWalletConfigurations.Add(new ProviderWalletConfiguration
        {
            ProviderWalletId = otherWallet, AssetCode = "EUR", Environment = "Testing", DisplayName = "EUR wallet"
        });
        db.ProviderRequestLogs.Add(new ProviderRequestLog
        {
            ProviderCode = ProviderCode.Blaaiz, Endpoint = "/api/external/virtual-bank-account", HttpMethod = "POST",
            RequestBodyJson = JsonSerializer.Serialize(new { businessCustomerId = s.Account.BusinessCustomerId, wallet_id = otherWallet })
        });
        await db.SaveChangesAsync();
        var client = DispatchProxy.Create<IBlaaizApiClient, AccountApi>();
        var api = (AccountApi)(object)client;
        api.AccountId = s.ProviderAccount;
        var wallet = new RecordedWallet(s.Wallet);
        var result = await new BlaaizCollectionAccountProvisioner(db, client, wallet).ProvisionAsync(new(
            s.Account.BusinessProfileId, s.Account.BusinessCustomerId, s.Account.Id, s.Account.ExternalReference,
            "Customer", null, null, "CA", "GBP"));
        Assert.False(wallet.PreviouslySubmitted);
        Assert.Equal(s.Wallet, api.Request!.WalletId);
        Assert.Equal(s.Customer, api.Request.CustomerId);
        Assert.Equal(ProviderAccountMappingStatus.Pending, result.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Another_business_cannot_provision_the_account()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db);
        var provider = new StubProvisioner(s);
        var service = new CollectionAccountProvisioningService(db, new Context(Guid.NewGuid()), [provider]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(s.Account.BusinessCustomerId, s.Account.Id, new("Blaaiz")));
        Assert.Equal(0, provider.Calls);
    }

    [DatabaseIntegrationFact]
    public async Task Ready_EUR_and_GBP_deposits_credit_once_and_unknown_account_cannot_fall_back()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var currency in new[] { "EUR", "GBP" })
        {
            var s = await Setup(db, currency, mapping: true);
            await Signed(Webhooks(db), Payload(s));
            db.ChangeTracker.Clear();
            var inbound = scope.ServiceProvider.GetRequiredService<IEmbeddedInboundCollectionService>();
            var request = Deposit(s);
            Assert.False(await inbound.TryProcessBlaaizDepositAsync(request with { ProviderAccountId = "unknown-account" }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => inbound.TryProcessBlaaizDepositAsync(request with { ProviderCustomerId = "other-customer" }));
            Assert.True(await inbound.TryProcessBlaaizDepositAsync(request));
            db.ChangeTracker.Clear();
            Assert.True(await inbound.TryProcessBlaaizDepositAsync(request));
            db.ChangeTracker.Clear();
            Assert.Equal(25m, (await db.FinancialAccounts.FindAsync(s.Financial.Id))!.AvailableBalance);
            Assert.Equal(1, await db.LedgerTransactions.CountAsync(x => x.IdempotencyKey == request.ProviderTransactionId));
        }
    }

    private static EmbeddedInboundCollectionRequest Deposit(Scenario s) => new(
        "deposit-" + s.ProviderAccount, null, "COMPLETED", s.Account.AssetCode, 25m, 25m, 0m, s.Account.AssetCode,
        s.ProviderAccount, s.Customer, null, "12345678", "{}", DateTime.UtcNow);
    private const string Secret = "test-only-virtual-account-webhook-secret";
    private static BlaaizWebhookService Webhooks(AppDbContext db) => new(db,
        Options.Create(new BlaaizOptions { IsEnabled = true, WebhookSigningSecret = Secret }), null!, null!, null!, null!, null!, null!, null!);
    private static Task<BlaaizWebhookResult> Signed(BlaaizWebhookService service, string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        return service.ProcessCollectionWebhookAsync(payload, BlaaizWebhookSignature.Create(timestamp, payload, Secret), timestamp);
    }
    private static string Payload(Scenario s, string eventType = "virtual_account.ready", string status = "SUCCESSFUL") =>
        BlaaizVirtualAccountTests.Payload(s.Account.AssetCode, eventType, status, s.ProviderAccount, s.Customer, s.Wallet);

    private static async Task<Scenario> Setup(AppDbContext db, string currency = "EUR", bool mapping = false)
    {
        var unique = Guid.NewGuid().ToString("N");
        var owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = "owner-" + unique, CountryCode = "CA" };
        var business = new BusinessProfile { OwnerUser = owner, BusinessName = "Test business", CountryCode = "CA" };
        var customer = new BusinessCustomer { BusinessProfile = business, ExternalReference = unique, DisplayName = "Test customer", CountryCode = "CA", Status = BusinessCustomerStatus.Active };
        var financial = new FinancialAccount { OwnerType = FinancialAccountOwnerType.BusinessCustomer, OwnerId = customer.Id, AccountCode = unique, AssetCode = currency, AccountType = FinancialAccountType.Customer, Status = FinancialAccountStatus.Active };
        var account = new CollectionAccount { BusinessProfile = business, BusinessCustomer = customer, FinancialAccount = financial, ExternalReference = unique, AssetCode = currency, Status = CollectionAccountStatus.Pending };
        var wallet = new ProviderWalletConfiguration { ProviderWalletId = "wallet-" + unique, AssetCode = currency, Environment = "Testing", DisplayName = "Test wallet" };
        var s = new Scenario(account, financial, owner.Id, "customer-" + unique, wallet.ProviderWalletId, "va-" + unique);
        db.Users.Add(owner);
        db.CollectionAccounts.Add(account);
        db.ProviderCustomers.Add(new ProviderCustomer { BusinessCustomer = customer, ProviderCustomerId = s.Customer, ProviderStatus = "VERIFIED" });
        db.ProviderWalletSelections.Add(new ProviderWalletSelection { OperationType = "CollectionAccount", OperationId = account.Id, WalletConfiguration = wallet, ProviderWalletId = wallet.ProviderWalletId, Purpose = "COLLECTION" });
        if (mapping) db.ProviderAccountMappings.Add(new ProviderAccountMapping { CollectionAccount = account, ProviderCode = "Blaaiz", ProviderCustomerId = s.Customer, ProviderAccountId = s.ProviderAccount });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return s;
    }
    private sealed record Scenario(CollectionAccount Account, FinancialAccount Financial, Guid OwnerId, string Customer, string Wallet, string ProviderAccount);
    public class AccountApi : DispatchProxy
    {
        public string AccountId { get; set; } = "";
        public BlaaizVirtualBankAccountRequest? Request { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IBlaaizApiClient.CreateVirtualBankAccountAsync)) throw new InvalidOperationException("Unexpected provider call.");
            Request = (BlaaizVirtualBankAccountRequest)args![0]!;
            var raw = JsonSerializer.Serialize(new { data = new { id = AccountId, status = "PENDING" } });
            return Task.FromResult(new BlaaizApiResult<BlaaizVirtualBankAccountEnvelope>(new(), raw, Guid.NewGuid()));
        }
    }
    private sealed class RecordedWallet(string wallet) : IProviderWalletResolver
    {
        public bool PreviouslySubmitted { get; private set; }
        public string EnvironmentName => "Testing";
        public string ConnectionKey => "test";
        public Task<string> SelectAsync(string operationType, Guid operationId, string providerCode, string assetCode, string? networkCode, string purpose, bool previouslySubmitted, CancellationToken ct = default)
        {
            PreviouslySubmitted = previouslySubmitted;
            return Task.FromResult(wallet);
        }
    }
    private sealed class Context(Guid businessId) : IEmbeddedFinanceContextAccessor
    {
        public EmbeddedFinancePrincipal GetRequiredPrincipal() => new(Guid.NewGuid(), Guid.NewGuid(), businessId, EmbeddedFinanceScope.All, "test");
    }
    private sealed class StubProvisioner(Scenario s) : ICollectionAccountProvisioner
    {
        public int Calls { get; private set; }
        public Func<Task<bool>>? BeforeReturn { get; init; }
        public string ProviderCode => "Blaaiz";
        public bool Supports(string countryCode, string assetCode) => assetCode is "EUR" or "GBP";
        public async Task<CollectionAccountProvisioningResult> ProvisionAsync(CollectionAccountProvisioningRequest request, CancellationToken ct = default)
        {
            Calls++;
            if (BeforeReturn is not null) await BeforeReturn();
            return new(s.Customer, s.ProviderAccount, null, null, null, null, null, ProviderAccountMappingStatus.Pending);
        }
    }
}
