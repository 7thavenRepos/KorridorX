using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Wallets;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Notifications;
using KorridorX.Services.Audit;
using KorridorX.Services.Auth;
using KorridorX.Services.Notifications;
using KorridorX.Services.Security;
using KorridorX.Services.Wallets;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class WalletActivityNotificationWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    private static async Task<ApplicationUser> CreateUser(AppDbContext db, UserManager<ApplicationUser> users, string country)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { UserName = "notice-" + suffix + "@example.test", Email = "notice-" + suffix + "@example.test",
            FirstName = "Notice", LastName = "Test", EmailConfirmed = true, UserType = UserType.Consumer, Status = UserStatus.Active };
        var result = await users.CreateAsync(user, "ReleaseCandidate!123");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(x => x.Description)));
        db.CustomerProfiles.Add(new CustomerProfile { UserId = user.Id, CountryCode = country, FirstName = "Notice", LastName = "Test" });
        db.MobilePushDevices.Add(new MobilePushDevice { UserId = user.Id, Platform = "android", PushToken = "fake-" + suffix,
            PushTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix))),
            DeviceFingerprint = suffix, DeviceName = "Local test device" });
        await db.SaveChangesAsync();
        return user;
    }

    private static NotificationQueueService Queue(AppDbContext db) => new(db, Options.Create(new NotificationDeliveryOptions
        { Firebase = new FirebasePushDeliveryOptions { IsEnabled = true } }));

    [DatabaseIntegrationFact]
    public async Task Creating_a_wallet_queues_one_email_and_push_while_reopening_it_is_silent()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var route = await db.CountryAssets.AsNoTracking().Where(x => x.Country.IsSupported && x.Asset.IsSupported &&
            x.Asset.IndividualEnabled && x.Asset.Type == AssetType.Fiat && x.CanDeposit)
            .Select(x => new { x.CountryCode, x.AssetCode }).FirstAsync();
        var user = await CreateUser(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), route.CountryCode);
        var wallets = new ConsumerWalletService(db, Queue(db));
        var first = await wallets.CreateWalletAsync(user.Id, new CreateConsumerWalletRequestDto(route.AssetCode));
        var again = await wallets.CreateWalletAsync(user.Id, new CreateConsumerWalletRequestDto(route.AssetCode));
        Assert.Equal(first.Id, again.Id);
        var messages = await db.NotificationMessages.Where(x => x.UserId == user.Id && x.RelatedEntityType == "FinancialAccount").ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Single(messages.Where(x => x.Channel == NotificationChannels.Email));
        Assert.Single(messages.Where(x => x.Channel == NotificationChannels.Push));
        Assert.All(messages, x => { Assert.Equal(first.Id.ToString(), x.RelatedEntityId); Assert.Equal(NotificationStatuses.Pending, x.Status); });
    }

    [DatabaseIntegrationFact]
    public async Task Successful_pin_setup_and_change_each_queue_email_and_safe_push_but_failed_password_does_not()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await CreateUser(db, users, "CA");
        var pins = new TransactionPinService(db, users, services.GetRequiredService<IAuditService>(),
            services.GetRequiredService<IOptions<SecurityOptions>>(), services.GetRequiredService<IOptions<ConsumerExternalAuthOptions>>(),
            services.GetRequiredService<ExternalAuthChallengeService>(), services.GetRequiredService<IExternalProviderTokenVerifier>(), Queue(db));
        await pins.SetAsync(user.Id, "ReleaseCandidate!123", "2468");
        await pins.SetAsync(user.Id, "ReleaseCandidate!123", "8642");
        await Assert.ThrowsAsync<InvalidOperationException>(() => pins.SetAsync(user.Id, "IncorrectPassword!", "1357"));
        var messages = await db.NotificationMessages.Where(x => x.UserId == user.Id &&
            x.RelatedEntityType == NotificationSecurityPolicy.AccountSecurityEntityType).ToListAsync();
        Assert.Equal(4, messages.Count);
        var emails = messages.Where(x => x.Channel == NotificationChannels.Email).ToList();
        Assert.Equal(2, emails.Count);
        Assert.Single(emails.Where(x => x.Subject.EndsWith("was set")));
        Assert.Single(emails.Where(x => x.Subject.EndsWith("was changed")));
        Assert.All(messages, x => { Assert.DoesNotContain("2468", x.Body); Assert.DoesNotContain("8642", x.Body); Assert.DoesNotContain("1357", x.Body); });
        Assert.All(messages.Where(x => x.Channel == NotificationChannels.Push), x =>
            Assert.Equal("Open KorridorX to review this account security update.", x.Body));
        Assert.All(emails, x => Assert.Equal(NotificationSecurityPolicy.RedactedBody, NotificationSecurityPolicy.BodyForApi(x)));
    }
}
