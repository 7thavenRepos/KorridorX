using System.Reflection;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using KorridorX.Services.Wallets;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

// Real PostgreSQL transactions; the provider boundary is a strict local fake.
// These tests never contact Blaaiz or credit a staging/production wallet.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class WalletBankDepositWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    public class ProviderProxy : DispatchProxy
    {
        public RemittanceTransactionStatusResult Result { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IRemittanceProvider.GetTransactionAsync)
                ? Task.FromResult(Result) : throw new InvalidOperationException("Unexpected provider operation in bank-deposit test.");
    }
    private sealed class Resolver : IProviderWalletResolver
    {
        public string EnvironmentName { get; } = "BankTest-" + Guid.NewGuid().ToString("N");
        public string ConnectionKey => new('B', 64);
        public Task<string> SelectAsync(string a, Guid b, string c, string d, string? e, string f, bool g, CancellationToken ct = default) =>
            throw new InvalidOperationException("No provider-wallet selection during reconciliation tests.");
    }
    private sealed record Fixture(WalletDepositAccountService Service, WalletDepositAccount Bank, FinancialAccount Wallet,
        ProviderProxy Provider, EmbeddedInboundCollectionRequest Request, Guid UserId);
    private static async Task<Fixture> Setup(AppDbContext db, bool failAfterNotification = false)
    {
        var resolver = new Resolver(); var suffix = Guid.NewGuid().ToString("N"); var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = userId, UserName = "bank-" + suffix, NormalizedUserName = "BANK-" + suffix,
            Email = suffix + "@example.test", NormalizedEmail = suffix + "@EXAMPLE.TEST", SecurityStamp = Guid.NewGuid().ToString() });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = userId, CountryCode = "NG", FirstName = "Test", LastName = "Customer", KycStatus = KycStatus.Approved });
        var wallet = new FinancialAccount { OwnerType = FinancialAccountOwnerType.User, OwnerId = userId,
            AssetCode = "NGN", AccountCode = "bank-" + suffix, AccountType = FinancialAccountType.Customer, Status = FinancialAccountStatus.Active };
        var providerWallet = new ProviderWalletConfiguration { ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName,
            AssetCode = "NGN", ProviderWalletId = "wallet-" + suffix, DisplayName = "Test wallet", NetworkCode = "" };
        var channel = new PaymentChannel { Environment = resolver.EnvironmentName, AssetCode = "NGN", CountryCode = "*",
            Direction = PaymentChannelDirection.Deposit, CapabilityCode = "virtual-account", DisplayName = "NGN bank test",
            ProviderWalletConfigurationId = providerWallet.Id, IndividualEnabled = true, HasBeenActivated = true, Status = PaymentChannelStatus.Paused };
        var bank = new WalletDepositAccount { FinancialAccountId = wallet.Id, PaymentChannelId = channel.Id,
            ProviderWalletConfigurationId = providerWallet.Id, ProviderWalletId = providerWallet.ProviderWalletId,
            ProviderCustomerId = "customer-" + suffix, ProviderAccountId = "account-" + suffix,
            Environment = resolver.EnvironmentName, ConnectionKey = resolver.ConnectionKey, AssetCode = "NGN", Status = "Active", AccountNumber = "0123456789" };
        db.FinancialAccounts.Add(wallet); db.ProviderWalletConfigurations.Add(providerWallet); db.PaymentChannels.Add(channel); db.WalletDepositAccounts.Add(bank);
        await db.SaveChangesAsync();
        var provider = DispatchProxy.Create<IRemittanceProvider, ProviderProxy>(); var proxy = (ProviderProxy)(object)provider;
        var request = new EmbeddedInboundCollectionRequest("deposit-" + suffix, null, "SUCCESSFUL", "NGN", 100m,
            null, null, null, bank.ProviderAccountId, bank.ProviderCustomerId, null, bank.AccountNumber, "{}", DateTime.UtcNow);
        proxy.Result = new(request.ProviderTransactionId, null, "collection", "SUCCESSFUL", "NGN", 100m, null, null, "{}", Guid.NewGuid());
        var channels = new PaymentChannelService(db, resolver, Options.Create(new BlaaizOptions()), new CollectionPaymentMethodPolicy());
        INotificationQueueService notifications = new NotificationQueueService(db, Options.Create(new NotificationDeliveryOptions()));
        if (failAfterNotification)
        {
            var failing = DispatchProxy.Create<INotificationQueueService, ConsumerWalletReceivingAccountWorkflowTests.QueueThenFail>();
            ((ConsumerWalletReceivingAccountWorkflowTests.QueueThenFail)(object)failing).Inner = notifications;
            notifications = failing;
        }
        return new(new WalletDepositAccountService(db, channels, null!, resolver, null!, provider, new ConsumerWalletFundingCreditService(db, notifications), notifications), bank, wallet, proxy, request, userId);
    }
    [DatabaseIntegrationFact] public async Task Verified_deposit_posts_once_even_when_channel_was_paused_after_account_creation()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var f = await Setup(db);
        Assert.True(await f.Service.TryProcessDepositAsync(f.Request, default)); Assert.True(await f.Service.TryProcessDepositAsync(f.Request, default));
        await db.Entry(f.Wallet).ReloadAsync(); Assert.Equal(100m, f.Wallet.AvailableBalance); Assert.Equal(100m, f.Wallet.SettledBalance);
        var collection = Assert.Single(await db.Collections.Where(x => x.FinancialAccountId == f.Wallet.Id).ToListAsync());
        Assert.Equal(f.Bank.Id, collection.RelatedEntityId); Assert.Equal(f.Bank.PaymentChannelId, collection.PaymentChannelId);
        Assert.Single(await db.LedgerTransactions.Where(x => x.RelatedEntityId == collection.Id).ToListAsync());
        Assert.Single(await db.ProviderTransactions.Where(x => x.ProviderTransactionId == f.Request.ProviderTransactionId).ToListAsync());
        var notification = Assert.Single(await db.NotificationMessages.Where(x => x.UserId == f.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType).ToListAsync());
        Assert.Equal(NotificationChannels.Email, notification.Channel); Assert.Contains("NGN 100.00", notification.Body);
        Assert.Equal(collection.Id.ToString(), notification.RelatedEntityId);
    }
    [DatabaseIntegrationFact] public async Task Wrong_verified_amount_never_credits_wallet()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var f = await Setup(db);
        f.Provider.Result = f.Provider.Result with { Amount = 101m };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.TryProcessDepositAsync(f.Request, default));
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == f.Wallet.Id)).AvailableBalance);
        Assert.Empty(await db.Collections.Where(x => x.FinancialAccountId == f.Wallet.Id).ToListAsync());
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == f.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
    }

    [DatabaseIntegrationFact] public async Task Rolled_back_bank_credit_does_not_leak_a_funding_email_during_outer_webhook_save()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var f = await Setup(db, failAfterNotification: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.TryProcessDepositAsync(f.Request, default));
        await db.SaveChangesAsync(); // The webhook handler saves only its failed receipt.
        var wallet = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == f.Wallet.Id);
        Assert.Equal(0m, wallet.AvailableBalance); Assert.Equal(0m, wallet.SettledBalance);
        Assert.Empty(await db.Collections.Where(x => x.FinancialAccountId == f.Wallet.Id).ToListAsync());
        Assert.Empty(await db.LedgerPostings.Where(x => x.FinancialAccountId == f.Wallet.Id).ToListAsync());
        Assert.False(await db.ProviderTransactions.AnyAsync(x => x.ProviderTransactionId == f.Request.ProviderTransactionId));
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == f.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
        Assert.DoesNotContain(db.NotificationMessages.Local, x => x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType);
    }
    [DatabaseIntegrationFact] public async Task Unverified_owner_leaves_no_tracked_money_for_outer_webhook_save()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var f = await Setup(db);
        var profile = await db.CustomerProfiles.SingleAsync(x => x.UserId == f.UserId); profile.KycStatus = KycStatus.NotStarted; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.TryProcessDepositAsync(f.Request, default));
        await db.SaveChangesAsync(); // Simulate the outer handler saving its failed receipt.
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == f.Wallet.Id)).AvailableBalance);
        Assert.Empty(await db.Collections.Where(x => x.FinancialAccountId == f.Wallet.Id).ToListAsync());
    }
    [DatabaseIntegrationFact] public async Task Account_ID_cannot_be_replaced_by_customer_email_or_a_pooled_wallet()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var f = await Setup(db);
        Assert.False(await f.Service.TryProcessDepositAsync(f.Request with { ProviderAccountId = null }, default));
        Assert.False(await f.Service.TryProcessDepositAsync(f.Request with { ProviderAccountId = "other-account" }, default));
        Assert.Equal(0m, f.Wallet.AvailableBalance);
    }
    [DatabaseIntegrationFact] public async Task Mismatched_customer_cannot_replay_an_already_credited_receipt()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var f = await Setup(db);
        Assert.True(await f.Service.TryProcessDepositAsync(f.Request, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.TryProcessDepositAsync(f.Request with { ProviderCustomerId = "other-customer" }, default));
        Assert.Equal(100m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == f.Wallet.Id)).AvailableBalance);
    }
}
