using System.Reflection;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Compliance;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Compliance;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using KorridorX.Services.Wallets;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed partial class ConsumerWalletReceivingAccountWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Setup_requests_once_then_recovers_readiness_without_another_creation_or_financial_change()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Seed(db);
        var service = Service(db, s);
        Assert.Equal("NotStarted", (await service.GetAsync(s.UserId, s.Wallet.Id)).Status);
        Assert.Equal(0, s.Api.Creates);
        var first = await service.SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal("Pending", first.Status);
        Assert.Empty(first.Account!.Instructions.Fields);
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == s.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.ReceivingAccountEntityType));
        Assert.Equal(1, s.Api.Creates);
        Assert.Equal("Pending", (await service.GetAsync(s.UserId, s.Wallet.Id)).Status);
        Assert.Equal(0, s.Api.Lookups);
        s.Api.Ready = true;
        var second = await service.SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal("Ready", second.Status);
        Assert.True(second.Account!.Instructions.IsReady);
        Assert.Equal(first.Account.Id, second.Account.Id);
        Assert.Equal(1, s.Api.Creates);
        Assert.Equal(1, s.Api.Lookups);
        Assert.Single(await db.NotificationMessages.Where(x => x.UserId == s.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.ReceivingAccountEntityType).ToListAsync());
        Assert.Equal("Ready", (await service.SetupAsync(s.UserId, s.Wallet.Id)).Status);
        Assert.Equal(1, s.Api.Lookups);
        Assert.Single(await db.WalletDepositAccounts.Where(x => x.FinancialAccountId == s.Wallet.Id).ToListAsync());
        Assert.Empty(await db.Collections.Where(x => x.FinancialAccountId == s.Wallet.Id).ToListAsync());
        Assert.Empty(await db.LedgerPostings.Where(x => x.FinancialAccountId == s.Wallet.Id).ToListAsync());
        await db.Entry(s.Wallet).ReloadAsync();
        Assert.Equal(0m, s.Wallet.AvailableBalance);
        Assert.Equal(0m, s.Wallet.SettledBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Unverified_customer_gets_explicit_verification_status_without_provider_creation()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db, approved: false);
        var result = await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal("VerificationRequired", result.Status);
        Assert.False(result.CanRetry);
        Assert.Null(result.Account);
        Assert.Equal(0, s.Api.Creates);
        Assert.Empty(await db.WalletDepositAccounts.Where(x => x.FinancialAccountId == s.Wallet.Id).ToListAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Missing_active_deposit_channel_is_reported_without_activating_admin_configuration()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db, enabled: false);
        var result = await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal("Unavailable", result.Status);
        Assert.Equal(0, s.Api.Creates);
        Assert.Equal(PaymentChannelStatus.Draft, (await db.PaymentChannels.FindAsync(s.Channel.Id))!.Status);
    }

    [DatabaseIntegrationFact]
    public async Task Changed_provider_connection_never_reuses_or_recreates_the_old_receiving_account()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db); var service = Service(db, s);
        await service.SetupAsync(s.UserId, s.Wallet.Id);
        s.Resolver.ConnectionKey = new string('B', 64);
        var result = await service.SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal("ActionRequired", result.Status);
        Assert.Contains("previous provider connection", result.Message);
        Assert.Null(result.Account);
        Assert.Equal(1, s.Api.Creates);
        Assert.Equal(0, s.Api.Lookups);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_timeout_is_pending_and_retry_looks_up_instead_of_creating_twice()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db); var service = Service(db, s);
        s.Api.CreateError = new TaskCanceledException("simulated provider timeout");
        Assert.Equal("Pending", (await service.SetupAsync(s.UserId, s.Wallet.Id)).Status);
        s.Api.CreateError = null;
        Assert.Equal("Pending", (await service.SetupAsync(s.UserId, s.Wallet.Id)).Status);
        Assert.Equal(1, s.Api.Creates);
        Assert.Equal(1, s.Api.Lookups);
    }

    [DatabaseIntegrationFact]
    public async Task Ambiguous_routes_require_a_choice_and_default_route_selection_does_not_change_existing_accounts()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db);
        s.ProviderWallet.DefaultForCollection = false;
        var otherWallet = ProviderWallet(s.Resolver, "other-" + Guid.NewGuid().ToString("N"));
        otherWallet.DefaultForCollection = false;
        db.ProviderWalletConfigurations.Add(otherWallet);
        db.PaymentChannels.Add(Channel(otherWallet, s.Resolver.EnvironmentName));
        await db.SaveChangesAsync();
        var result = await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal("ActionRequired", result.Status);
        Assert.Contains("Several bank-deposit routes", result.Message);
        Assert.Equal(0, s.Api.Creates);
        s.ProviderWallet.DefaultForCollection = true; await db.SaveChangesAsync();
        result = await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(s.ProviderWallet.ProviderWalletId, s.Api.LastWalletId);
        s.ProviderWallet.DefaultForCollection = false; await db.SaveChangesAsync();
        otherWallet.DefaultForCollection = true; await db.SaveChangesAsync();
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal(1, s.Api.Creates);
        Assert.Equal(s.ProviderWallet.ProviderWalletId, s.Api.LastWalletId);
    }

    [DatabaseIntegrationFact]
    public async Task Another_user_cannot_start_setup_or_read_receiving_details()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db); var service = Service(db, s);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetupAsync(Guid.NewGuid(), s.Wallet.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(Guid.NewGuid(), s.Wallet.Id));
        Assert.Equal(0, s.Api.Creates);
    }

    private static ConsumerWalletReceivingAccountService Service(AppDbContext db, Scenario s)
    {
        var contexts = new WalletFundingContextService(db, null!, new ComplianceGateService(db));
        var channels = new PaymentChannelService(db, s.Resolver,
            Options.Create(new BlaaizOptions { IsEnabled = true, BaseUrl = "https://api-prod.blaaiz.com" }), new CollectionPaymentMethodPolicy());
        var accounts = new WalletDepositAccountService(db, channels, contexts, s.Resolver, s.Client, null!, null!, Notifications(db));
        return new(db, contexts, channels, accounts, s.Resolver, NullLogger<ConsumerWalletReceivingAccountService>.Instance);
    }

    private static NotificationQueueService Notifications(AppDbContext db) => new(db,
        Options.Create(new NotificationDeliveryOptions { Firebase = new FirebasePushDeliveryOptions { IsEnabled = true } }));

    private static WalletDepositAccountService Accounts(AppDbContext db, Scenario s, INotificationQueueService? notifications = null)
    {
        var contexts = new WalletFundingContextService(db, null!, new ComplianceGateService(db));
        var channels = new PaymentChannelService(db, s.Resolver,
            Options.Create(new BlaaizOptions { IsEnabled = true, BaseUrl = "https://api-prod.blaaiz.com" }), new CollectionPaymentMethodPolicy());
        return new(db, channels, contexts, s.Resolver, s.Client, null!, null!, notifications ?? Notifications(db));
    }

    private static async Task<Scenario> Seed(AppDbContext db, bool approved = true, bool enabled = true)
    {
        var unique = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "receiving-" + unique,
            Email = unique + "@example.test", CountryCode = "GB" };
        var profile = new CustomerProfile { User = user, FirstName = "Receiving", LastName = "Test", CountryCode = "GB",
            Email = user.Email, KycStatus = approved ? KycStatus.Approved : KycStatus.NotStarted };
        var wallet = new FinancialAccount { OwnerType = FinancialAccountOwnerType.User, OwnerId = user.Id,
            AccountCode = "KX-RECV-" + unique, AssetCode = "GBP", AccountType = FinancialAccountType.Customer, Status = FinancialAccountStatus.Active };
        var resolver = new Resolver(); var providerWallet = ProviderWallet(resolver, unique); resolver.WalletId = providerWallet.ProviderWalletId;
        var channel = Channel(providerWallet, resolver.EnvironmentName); channel.Status = enabled ? PaymentChannelStatus.Active : PaymentChannelStatus.Draft;
        db.CustomerProfiles.Add(profile); db.FinancialAccounts.Add(wallet); db.ProviderWalletConfigurations.Add(providerWallet); db.PaymentChannels.Add(channel);
        if (approved)
        {
            db.KycProfiles.Add(new KycProfile { CustomerProfile = profile, Status = KycStatus.Approved });
            db.ProviderCustomers.Add(new ProviderCustomer { CustomerProfile = profile, ProviderCustomerId = "customer-" + unique, ProviderStatus = "VERIFIED" });
        }
        await db.SaveChangesAsync();
        var client = DispatchProxy.Create<IBlaaizApiClient, AccountApi>(); var api = (AccountApi)(object)client;
        api.CustomerId = "customer-" + unique; api.AccountId = "receiving-" + unique;
        return new(user.Id, wallet, providerWallet, channel, resolver, client, api);
    }

    private static ProviderWalletConfiguration ProviderWallet(Resolver resolver, string unique) => new()
    {
        ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName, ProviderWalletId = "wallet-" + unique,
        AssetCode = "GBP", NetworkCode = "", DisplayName = "Test GBP", IsActive = true, CollectionEnabled = true,
        DefaultForCollection = true, VerifiedAt = DateTime.UtcNow, VerifiedConnectionKey = resolver.ConnectionKey
    };
    private static PaymentChannel Channel(ProviderWalletConfiguration wallet, string environment) => new()
    {
        Environment = environment, ProviderWalletConfigurationId = wallet.Id, ProviderCode = "BLAAIZ", AssetCode = "GBP", CountryCode = "GB",
        Direction = PaymentChannelDirection.Deposit, CapabilityCode = BlaaizChannelCapabilities.VirtualAccount,
        DisplayName = "Test GBP receiving", IndividualEnabled = true, ProviderCapabilityConfirmed = true,
        HasBeenActivated = true, Status = PaymentChannelStatus.Active
    };
    private sealed record Scenario(Guid UserId, FinancialAccount Wallet, ProviderWalletConfiguration ProviderWallet,
        PaymentChannel Channel, Resolver Resolver, IBlaaizApiClient Client, AccountApi Api);
    private sealed class Resolver : IProviderWalletResolver
    {
        public string EnvironmentName { get; } = "Receiving-" + Guid.NewGuid().ToString("N");
        public string ConnectionKey { get; set; } = new('A', 64);
        public string WalletId { get; set; } = "";
        public Task<string> SelectAsync(string operationType, Guid operationId, string providerCode, string assetCode,
            string? networkCode, string purpose, bool previouslySubmitted, CancellationToken ct = default) => Task.FromResult(WalletId);
    }
    public class AccountApi : DispatchProxy
    {
        public string CustomerId { get; set; } = "";
        public string AccountId { get; set; } = "";
        public string? LastWalletId { get; private set; }
        public bool Ready { get; set; }
        public Exception? CreateError { get; set; }
        public int Creates { get; private set; }
        public int Lookups { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IBlaaizApiClient.CreateVirtualBankAccountAsync))
            {
                Creates++; LastWalletId = ((BlaaizVirtualBankAccountRequest)args![0]!).WalletId;
                if (CreateError is not null) return Task.FromException<BlaaizApiResult<BlaaizVirtualBankAccountEnvelope>>(CreateError);
            }
            else if (targetMethod?.Name == nameof(IBlaaizApiClient.GetVirtualBankAccountsAsync)) { Lookups++; LastWalletId = (string)args![0]!; }
            else throw new InvalidOperationException("Unexpected provider operation; this test permits dedicated account setup only.");
            var raw = JsonSerializer.Serialize(new { data = new { id = AccountId, customer_id = CustomerId, wallet_id = LastWalletId,
                currency = "GBP", status = Ready ? "SUCCESSFUL" : "PENDING", account_number = Ready ? "12345678" : null,
                account_name = Ready ? "Receiving Test" : null, bank_name = Ready ? "Test Bank" : null, sort_code = Ready ? "123456" : null } });
            return Task.FromResult(new BlaaizApiResult<BlaaizVirtualBankAccountEnvelope>(new(), raw, Guid.NewGuid()));
        }
    }
}
