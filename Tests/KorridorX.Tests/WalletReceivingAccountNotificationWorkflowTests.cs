using System.Reflection;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Notifications;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

public sealed partial class ConsumerWalletReceivingAccountWorkflowTests
{
    [DatabaseIntegrationFact]
    public async Task Immediately_ready_account_queues_full_email_and_safe_push_and_is_visible_only_to_its_owner()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Seed(db); s.Api.Ready = true;
        await AddPushDevice(db, s.UserId);
        Assert.Equal("Ready", (await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id)).Status);
        var notices = await ReadyNotices(db, s.UserId);
        var email = Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Email));
        var push = Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Push));
        Assert.Contains("12345678", email.Body);
        Assert.Contains("Receiving Test", email.Body);
        Assert.Contains("Test Bank", email.Body);
        Assert.Contains("123456", email.Body);
        Assert.Equal("Open KorridorX to review this account security update.", push.Body);
        Assert.DoesNotContain("12345678", push.Body);
        Assert.Equal(email.Body, NotificationSecurityPolicy.BodyForApi(email));
        var history = await Notifications(db).GetMyNotificationsAsync(s.UserId, 1, 20);
        Assert.Contains(history.Items, x => x.Id == email.Id && x.Body.Contains("12345678"));
        Assert.Empty((await Notifications(db).GetMyNotificationsAsync(Guid.NewGuid(), 1, 20)).Items);
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Equal(2, (await ReadyNotices(db, s.UserId)).Count);
        Assert.Equal(1, s.Api.Creates);
        Assert.Equal(0, s.Api.Lookups);
        Assert.Empty(await db.Collections.Where(x => x.FinancialAccountId == s.Wallet.Id).ToListAsync());
        Assert.Empty(await db.LedgerPostings.Where(x => x.FinancialAccountId == s.Wallet.Id).ToListAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Ready_webhook_replay_and_later_pending_event_queue_one_notice_and_keep_confirmed_details()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db);
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        var accounts = Accounts(db, s);
        Assert.True(await accounts.TryProcessAccountEventAsync("virtual_account.ready", AccountEvent(s, "SUCCESSFUL"), default));
        Assert.True(await accounts.TryProcessAccountEventAsync("virtual_account.ready", AccountEvent(s, "SUCCESSFUL"), default));
        Assert.True(await accounts.TryProcessAccountEventAsync("virtual_account.created", AccountEvent(s, "PENDING"), default));
        Assert.Single(await ReadyNotices(db, s.UserId));
        var bank = await db.WalletDepositAccounts.SingleAsync(x => x.FinancialAccountId == s.Wallet.Id);
        Assert.Equal("Active", bank.Status); Assert.Equal("12345678", bank.AccountNumber);
        Assert.Equal(0m, s.Wallet.AvailableBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Existing_ready_account_get_is_silent_and_explicit_setup_catches_up_one_notice_without_provider_call()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db);
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        var bank = await db.WalletDepositAccounts.SingleAsync(x => x.FinancialAccountId == s.Wallet.Id);
        WalletDepositAccountService.ApplyState(bank, BlaaizVirtualAccountState.ParseWebhook(AccountEvent(s, "SUCCESSFUL"), "virtual_account.ready"));
        await db.SaveChangesAsync();
        Assert.Equal("Ready", (await Service(db, s).GetAsync(s.UserId, s.Wallet.Id)).Status);
        Assert.Empty(await ReadyNotices(db, s.UserId));
        Assert.Equal("Ready", (await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id)).Status);
        Assert.Equal("Ready", (await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id)).Status);
        Assert.Single(await ReadyNotices(db, s.UserId));
        Assert.Equal(1, s.Api.Creates); Assert.Equal(0, s.Api.Lookups);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_provider_lookup_and_ready_webhook_commit_one_notice()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db);
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id); s.Api.Ready = true;
        await using var lookupScope = fixture.Factory.Services.CreateAsyncScope();
        await using var webhookScope = fixture.Factory.Services.CreateAsyncScope();
        var lookupDb = lookupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var webhookDb = webhookScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Task.WhenAll(
            Accounts(lookupDb, s).GetAsync(s.UserId, s.Wallet.Id, s.Channel.Id, false, true, default),
            Accounts(webhookDb, s).TryProcessAccountEventAsync("virtual_account.ready", AccountEvent(s, "SUCCESSFUL"), default));
        Assert.Single(await ReadyNotices(db, s.UserId));
        Assert.Equal("Active", (await db.WalletDepositAccounts.AsNoTracking().SingleAsync(x => x.FinancialAccountId == s.Wallet.Id)).Status);
    }

    [DatabaseIntegrationFact]
    public async Task Queue_failure_rolls_back_readiness_and_new_notices_before_outer_webhook_save()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db);
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        var failing = DispatchProxy.Create<INotificationQueueService, QueueThenFail>();
        ((QueueThenFail)(object)failing).Inner = Notifications(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Accounts(db, s, failing)
            .TryProcessAccountEventAsync("virtual_account.ready", AccountEvent(s, "SUCCESSFUL"), default));
        await db.SaveChangesAsync(); // Outer handler persists its failed receipt.
        Assert.Empty(await ReadyNotices(db, s.UserId));
        Assert.DoesNotContain(db.NotificationMessages.Local, x => x.RelatedEntityType == WalletActivityNotificationFactory.ReceivingAccountEntityType);
        Assert.Equal("Pending", (await db.WalletDepositAccounts.AsNoTracking().SingleAsync(x => x.FinancialAccountId == s.Wallet.Id)).Status);
        Assert.True(await Accounts(db, s).TryProcessAccountEventAsync("virtual_account.ready", AccountEvent(s, "SUCCESSFUL"), default));
        Assert.Single(await ReadyNotices(db, s.UserId));
    }

    [DatabaseIntegrationFact]
    public async Task Another_provider_connection_or_mismatched_account_identity_never_sends_a_notice()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db);
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        var accounts = Accounts(db, s);
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.TryProcessAccountEventAsync(
            "virtual_account.ready", AccountEvent(s, "SUCCESSFUL", "wrong-account"), default));
        s.Resolver.ConnectionKey = new string('C', 64);
        Assert.False(await accounts.TryProcessAccountEventAsync("virtual_account.ready", AccountEvent(s, "SUCCESSFUL"), default));
        Assert.Empty(await ReadyNotices(db, s.UserId));
        Assert.Equal("Pending", (await db.WalletDepositAccounts.AsNoTracking().SingleAsync(x => x.FinancialAccountId == s.Wallet.Id)).Status);
    }

    [DatabaseIntegrationFact]
    public async Task Soft_deleted_notice_still_prevents_duplicate_ready_email()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db); s.Api.Ready = true;
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        var notice = Assert.Single(await ReadyNotices(db, s.UserId));
        notice.IsDeleted = true; await db.SaveChangesAsync();
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        Assert.Single(await ReadyNotices(db, s.UserId));
    }

    [DatabaseIntegrationFact]
    public async Task Business_ready_details_reach_owner_and_wallet_authorized_members_only()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Seed(db);
        await Service(db, s).SetupAsync(s.UserId, s.Wallet.Id);
        var business = new BusinessProfile { OwnerUserId = s.UserId, BusinessName = "Receiving Company", CountryCode = "GB" };
        db.BusinessProfiles.Add(business);
        s.Wallet.OwnerType = FinancialAccountOwnerType.Business; s.Wallet.OwnerId = business.Id;
        var authorized = Member(db, business.Id, BusinessPermission.ViewWallets, true);
        var defaultFinance = Member(db, business.Id, BusinessPermission.None, true, BusinessUserRole.Finance);
        var excluded = Member(db, business.Id, BusinessPermission.ViewReports, true);
        var inactive = Member(db, business.Id, BusinessPermission.ViewWallets, false);
        db.BusinessUsers.Add(new BusinessUser { BusinessProfileId = business.Id, UserId = s.UserId,
            Role = BusinessUserRole.Owner, Permissions = BusinessPermission.All });
        await db.SaveChangesAsync();
        Assert.True(await Accounts(db, s).TryProcessAccountEventAsync("virtual_account.ready", AccountEvent(s, "SUCCESSFUL"), default));
        Assert.Single(await ReadyNotices(db, s.UserId));
        Assert.Single(await ReadyNotices(db, authorized));
        Assert.Single(await ReadyNotices(db, defaultFinance));
        Assert.Empty(await ReadyNotices(db, excluded));
        Assert.Empty(await ReadyNotices(db, inactive));
    }

    private static Guid Member(AppDbContext db, Guid business, BusinessPermission permissions, bool active,
        BusinessUserRole role = BusinessUserRole.Member)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "member-" + suffix, Email = suffix + "@example.test" };
        db.Users.Add(user);
        db.BusinessUsers.Add(new BusinessUser { BusinessProfileId = business, UserId = user.Id,
            Role = role, Permissions = permissions, IsActive = active });
        return user.Id;
    }

    private static async Task AddPushDevice(AppDbContext db, Guid user)
    {
        var suffix = Guid.NewGuid().ToString("N");
        db.MobilePushDevices.Add(new MobilePushDevice { UserId = user, Platform = "android", PushToken = "fake-" + suffix,
            PushTokenHash = suffix.PadRight(64, '0'), DeviceFingerprint = suffix, DeviceName = "Local fake device" });
        await db.SaveChangesAsync();
    }

    private static Task<List<NotificationMessage>> ReadyNotices(AppDbContext db, Guid user) =>
        db.NotificationMessages.Where(x => x.UserId == user &&
            x.RelatedEntityType == WalletActivityNotificationFactory.ReceivingAccountEntityType).ToListAsync();

    private static string AccountEvent(Scenario s, string status, string? accountId = null) => JsonSerializer.Serialize(new
    {
        data = new { id = accountId ?? s.Api.AccountId, customer_id = s.Api.CustomerId, wallet_id = s.ProviderWallet.ProviderWalletId,
            currency = "GBP", status, account_number = status == "SUCCESSFUL" ? "12345678" : null,
            account_name = status == "SUCCESSFUL" ? "Receiving Test" : null, bank_name = status == "SUCCESSFUL" ? "Test Bank" : null,
            sort_code = status == "SUCCESSFUL" ? "123456" : null }
    });

    public class QueueThenFail : DispatchProxy
    {
        public INotificationQueueService Inner { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(INotificationQueueService.QueueUserAsync)
                ? QueueAndFail(args!) : throw new InvalidOperationException("Unexpected notification operation in failure test.");
        private async Task QueueAndFail(object?[] args)
        {
            await Inner.QueueUserAsync((Guid)args[0]!, (string)args[1]!, (string)args[2]!,
                (string?)args[3], (Guid?)args[4], (CancellationToken)args[5]!);
            throw new InvalidOperationException("Simulated queue failure after creating notification intent.");
        }
    }
}
