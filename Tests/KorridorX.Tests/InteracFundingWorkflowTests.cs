using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Models.Notifications;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.BusinessContext;
using KorridorX.Services.BusinessTransfers;
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

// These gates require the existing explicitly disposable RC PostgreSQL fixture.
// Every provider operation is a strict local fake; no live provider call is allowed.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class InteracFundingWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    public sealed class Resolver(string environment, string key) : IProviderWalletResolver
    {
        public string EnvironmentName => environment;
        public string ConnectionKey => key;
        public Task<string> SelectAsync(string operationType, Guid operationId, string providerCode, string assetCode,
            string? networkCode, string purpose, bool previouslySubmitted, CancellationToken ct = default) =>
            throw new InvalidOperationException("Tests must never submit or select a live provider operation.");
    }
    private sealed class SelectedBusiness(Guid id) : IBusinessContextAccessor
    { public Guid? GetSelectedBusinessProfileId() => id; }
    public class ProviderFake : DispatchProxy
    {
        public BlaaizTransactionData Transaction { get; set; } = null!;
        public BlaaizWalletData Wallet { get; set; } = null!;
        public BlaaizInteracAutoDepositEmailData Registration { get; set; } = null!;
        public bool FailRead { get; set; }
        public int Calls { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Calls++;
            if (FailRead) throw new HttpRequestException("Local fake read failed; no network request occurred.");
            return method?.Name switch
            {
                nameof(IBlaaizApiClient.GetTransactionAsync) => Task.FromResult(new BlaaizApiResult<BlaaizTransactionEnvelope>(
                    new() { Data = Transaction }, JsonSerializer.Serialize(new BlaaizTransactionEnvelope { Data = Transaction }), Guid.NewGuid())),
                nameof(IBlaaizApiClient.GetWalletAsync) => Task.FromResult(new BlaaizApiResult<BlaaizWalletData>(Wallet, JsonSerializer.Serialize(Wallet), Guid.NewGuid())),
                nameof(IBlaaizApiClient.GetInteracAutoDepositEmailAsync) => Task.FromResult(new BlaaizApiResult<BlaaizInteracAutoDepositEmailEnvelope>(
                    new() { Data = Registration }, JsonSerializer.Serialize(new BlaaizInteracAutoDepositEmailEnvelope { Data = Registration }), Guid.NewGuid())),
                _ => throw new InvalidOperationException("Unexpected provider operation: " + method?.Name)
            };
        }
    }
    private sealed record State(Guid UserId, Guid BusinessId, Guid WalletId, Guid BusinessWalletId, string RegistrationEmail,
        Resolver Resolver, InteracReceivingRoute Route, BlaaizTransactionData Transaction, Guid? SenderId);
    private sealed record Harness(InteracFundingService Funding, InteracDepositService Deposits, InteracAdminService Admin,
        CollectionStatusService Statuses, ProviderCollectionCreditGuard Guard, ProviderFake Provider);

    private static async Task<State> Setup(AppDbContext db, bool sender = true)
    {
        var suffix = Guid.NewGuid().ToString("N"); var userId = Guid.NewGuid(); var registrationEmail = "user-" + suffix + "@example.test";
        var resolver = new Resolver("InteracTest-" + suffix, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix))));
        db.Users.Add(new ApplicationUser { Id = userId, UserName = registrationEmail, NormalizedUserName = registrationEmail.ToUpperInvariant(),
            Email = registrationEmail, NormalizedEmail = registrationEmail.ToUpperInvariant(), EmailConfirmed = true, SecurityStamp = Guid.NewGuid().ToString(), UserType = UserType.Consumer });
        db.MobilePushDevices.Add(new MobilePushDevice { UserId = userId, Platform = "android", PushToken = "local-fake-" + suffix,
            PushTokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(suffix))),
            DeviceFingerprint = suffix, DeviceName = "Local test device" });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = userId, CountryCode = "CA", FirstName = "Interac", LastName = "Test", KycStatus = KycStatus.Approved });
        var business = new BusinessProfile { OwnerUserId = userId, BusinessName = "Interac business " + suffix, CountryCode = "CA", KybStatus = KybStatus.Approved };
        db.BusinessProfiles.Add(business);
        var personal = new FinancialAccount { OwnerType = FinancialAccountOwnerType.User, OwnerId = userId, AssetCode = "CAD",
            AccountCode = "int-personal-" + suffix, AccountType = FinancialAccountType.Customer, Status = FinancialAccountStatus.Active };
        var businessWallet = new FinancialAccount { OwnerType = FinancialAccountOwnerType.Business, OwnerId = business.Id, AssetCode = "CAD",
            AccountCode = "int-business-" + suffix, AccountType = FinancialAccountType.Customer, Status = FinancialAccountStatus.Active };
        db.FinancialAccounts.AddRange(personal, businessWallet);
        var configured = new ProviderWalletConfiguration { ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName, AssetCode = "CAD",
            NetworkCode = "", ProviderWalletId = "cad-" + suffix, DisplayName = "Local fake CAD wallet", IsActive = true,
            CollectionEnabled = true, VerifiedAt = DateTime.UtcNow.AddDays(-1), VerifiedConnectionKey = resolver.ConnectionKey };
        db.ProviderWalletConfigurations.Add(configured);
        var route = new InteracReceivingRoute { Environment = resolver.EnvironmentName, ConnectionKey = resolver.ConnectionKey,
            ProviderWalletConfigurationId = configured.Id, ProviderWalletId = configured.ProviderWalletId, ProviderBusinessId = "business-" + suffix,
            ProviderRegistrationId = Guid.NewGuid().ToString(), ReceivingEmail = "receive-" + suffix + "@example.test",
            NormalizedReceivingEmail = "receive-" + suffix + "@example.test", DisplayName = "Local fake Interac",
            Status = "Active", ProviderStatus = "ACTIVE", VerifiedAt = DateTime.UtcNow.AddDays(-1), IndividualEnabled = true, BusinessEnabled = true };
        db.InteracReceivingRoutes.Add(route);
        var sendingEmail = "sender-" + suffix + "@example.test"; Guid? senderId = null;
        if (sender)
        {
            var address = new InteracSendingEmail { RouteId = route.Id, FinancialAccountId = personal.Id, Email = sendingEmail,
                NormalizedEmail = sendingEmail, Status = "Verified", VerifiedAt = DateTime.UtcNow.AddMinutes(-10) };
            db.InteracSendingEmails.Add(address); senderId = address.Id;
            db.InteracSenderOwnerships.Add(new InteracSenderOwnership { RouteId = route.Id, FinancialAccountId = personal.Id,
                NormalizedEmail = sendingEmail, EstablishedAt = DateTime.UtcNow.AddMinutes(-10) });
        }
        await db.SaveChangesAsync();
        return new(userId, business.Id, personal.Id, businessWallet.Id, registrationEmail, resolver, route,
            new BlaaizTransactionData { Id = "deposit-" + suffix, Reference = "reference-" + suffix, Type = "collection", Status = "SUCCESSFUL",
                Currency = "CAD", Amount = 100m, AmountWithoutFee = 97.5m, Fee = 2.5m, BusinessId = route.ProviderBusinessId,
                BusinessWalletId = route.ProviderWalletId, PayeeCollectionEmail = route.ReceivingEmail,
                SourceInformation = new() { CollectionEmail = sendingEmail }, Date = DateTime.UtcNow.AddSeconds(-1) }, senderId);
    }

    private static Harness Build(AppDbContext db, State state)
    {
        var api = DispatchProxy.Create<IBlaaizApiClient, ProviderFake>(); var fake = (ProviderFake)(object)api;
        fake.Transaction = state.Transaction;
        fake.Wallet = new BlaaizWalletData { Id = state.Route.ProviderWalletId, Currency = "CAD", BusinessId = state.Route.ProviderBusinessId, IsActive = true };
        fake.Registration = new BlaaizInteracAutoDepositEmailData { Id = state.Route.ProviderRegistrationId, Email = state.Route.ReceivingEmail, Status = "ACTIVE" };
        var options = Options.Create(new BlaaizOptions { IsEnabled = true });
        var contexts = new WalletFundingContextService(db, new BusinessAccessService(db, new SelectedBusiness(state.BusinessId)), null!);
        var funding = new InteracFundingService(db, contexts, state.Resolver, new NotificationQueueService(db, Options.Create(new NotificationDeliveryOptions())), options);
        var guard = new ProviderCollectionCreditGuard(db, state.Resolver, api);
        var queue = new NotificationQueueService(db, Options.Create(new NotificationDeliveryOptions
            { Firebase = new FirebasePushDeliveryOptions { IsEnabled = true } }));
        var statuses = new CollectionStatusService(db, null!, null!, new ConsumerWalletFundingCreditService(db, queue), guard);
        return new(funding, new InteracDepositService(db, state.Resolver, api, guard, statuses), new InteracAdminService(db, state.Resolver, api, options), statuses, guard, fake);
    }

    private static string Payload(State s) => JsonSerializer.Serialize(new
    {
        event_id = Guid.NewGuid().ToString(), transaction_id = s.Transaction.Id, transaction_reference = s.Transaction.Reference,
        transaction_currency = "CAD", transaction_status = "SUCCESSFUL", transaction_amount = s.Transaction.Amount,
        transaction_amount_without_fee = s.Transaction.AmountWithoutFee, transaction_fee = s.Transaction.Fee,
        payee_collection_email = s.Transaction.PayeeCollectionEmail,
        source_information = new { collection_email = s.Transaction.SourceInformation?.CollectionEmail }
    });
    private static async Task<string> LatestCode(AppDbContext db, Guid senderId)
    {
        var body = await db.NotificationMessages.AsNoTracking().Where(x => x.RelatedEntityType == NotificationSecurityPolicy.AccountSecurityEntityType &&
            x.RelatedEntityId == senderId.ToString()).OrderByDescending(x => x.CreatedAt).Select(x => x.Body).FirstAsync();
        return Regex.Match(body, @"\b\d{6}\b").Value;
    }

    private static async Task AssertFundingNotices(AppDbContext db, Guid userId, Guid collectionId, string creditedAmount)
    {
        var messages = await db.NotificationMessages.AsNoTracking().Where(x => x.UserId == userId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType).ToListAsync();
        Assert.Equal(2, messages.Count);
        var email = Assert.Single(messages.Where(x => x.Channel == NotificationChannels.Email));
        var push = Assert.Single(messages.Where(x => x.Channel == NotificationChannels.Push));
        Assert.All(messages, x => { Assert.Equal(NotificationStatuses.Pending, x.Status); Assert.Equal(collectionId.ToString(), x.RelatedEntityId); });
        Assert.Contains("CAD " + creditedAmount, email.Body);
        Assert.Contains("CAD " + creditedAmount, push.Body);
        Assert.DoesNotContain("<p>", push.Body);
    }

    [DatabaseIntegrationFact]
    public async Task Verified_unsolicited_deposit_credits_net_once_without_transfer_and_replay_preserves_original_claim()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db); var h = Build(db, s);
        Assert.True(await h.Deposits.TryProcessAsync(Payload(s), default)); Assert.True(await h.Deposits.TryProcessAsync(Payload(s), default));
        var wallet = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId);
        Assert.Equal(97.5m, wallet.AvailableBalance); Assert.Equal(97.5m, wallet.SettledBalance);
        var collection = Assert.Single(await db.Collections.AsNoTracking().Where(x => x.FinancialAccountId == s.WalletId).ToListAsync());
        Assert.Null(collection.TransferId); Assert.Equal(nameof(InteracDeposit), collection.RelatedEntityType); Assert.Equal(97.5m, collection.Amount);
        Assert.Single(await db.ProviderCollectionCreditClaims.Where(x => x.ProviderTransactionId == s.Transaction.Id).ToListAsync());
        Assert.Single(await db.LedgerTransactions.Where(x => x.IdempotencyKey == s.Transaction.Id).ToListAsync());
        var provider = await db.ProviderTransactions.AsNoTracking().SingleAsync(x => x.ProviderTransactionId == s.Transaction.Id);
        Assert.Equal(100m, provider.Amount); Assert.Equal(97.5m, provider.AmountWithoutFee); Assert.Equal(2.5m, provider.ProviderFeeAmount);
        await AssertFundingNotices(db, s.UserId, collection.Id, "97.50");
    }

    [DatabaseIntegrationFact]
    public async Task Existing_Money_Request_and_unsolicited_handler_share_the_same_credit_claim()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db); var h = Build(db, s);
        var collection = new Collection { FinancialAccountId = s.WalletId, Purpose = PaymentOperationPurpose.AccountFunding, CurrencyCode = "CAD", Amount = 100m,
            PaymentMethod = PaymentMethod.Interac, ProviderCode = "BLAAIZ", ProviderCollectionId = s.Transaction.Id, ProviderReference = s.Transaction.Reference,
            Reference = "COL-TEST-" + Guid.NewGuid().ToString("N"), Status = CollectionStatus.Initiated };
        db.Collections.Add(collection); await db.SaveChangesAsync();
        await h.Statuses.ApplyTransitionAsync(collection, CollectionStatus.Successful, new("Reconciliation", ProviderCollectionId: s.Transaction.Id));
        Assert.True(await h.Deposits.TryProcessAsync(Payload(s), default));
        Assert.Equal(100m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
        Assert.Single(await db.LedgerTransactions.Where(x => x.IdempotencyKey == s.Transaction.Id).ToListAsync());
        Assert.Equal(collection.Id, (await db.InteracDeposits.SingleAsync(x => x.ProviderTransactionId == s.Transaction.Id)).CollectionId);
        await AssertFundingNotices(db, s.UserId, collection.Id, "100.00");
    }

    [DatabaseIntegrationFact]
    public async Task Unsolicited_first_then_second_collection_cannot_credit_same_provider_transaction()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db); var h = Build(db, s);
        await h.Deposits.TryProcessAsync(Payload(s), default);
        var duplicate = new Collection { FinancialAccountId = s.BusinessWalletId, Purpose = PaymentOperationPurpose.AccountFunding, CurrencyCode = "CAD", Amount = 100m,
            PaymentMethod = PaymentMethod.Interac, ProviderCode = "BLAAIZ", ProviderCollectionId = s.Transaction.Id,
            Reference = "COL-DUP-" + Guid.NewGuid().ToString("N"), Status = CollectionStatus.Initiated };
        db.Collections.Add(duplicate); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Statuses.ApplyTransitionAsync(duplicate, CollectionStatus.Successful, new("Reconciliation", ProviderCollectionId: s.Transaction.Id)));
        await db.SaveChangesAsync(); // Simulate the outer webhook saving its failed attempt.
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.BusinessWalletId)).AvailableBalance);
        Assert.Single(await db.LedgerTransactions.Where(x => x.IdempotencyKey == s.Transaction.Id).ToListAsync());
        Assert.Equal(2, await db.NotificationMessages.CountAsync(x => x.UserId == s.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
    }

    [DatabaseIntegrationFact]
    public async Task Unmatched_deposit_remains_in_audited_review_after_a_later_email_claim()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db, false); var h = Build(db, s);
        await h.Deposits.TryProcessAsync(Payload(s), default);
        var receipt = await db.InteracDeposits.SingleAsync(x => x.ProviderTransactionId == s.Transaction.Id); Assert.Equal("NeedsReview", receipt.Status);
        db.InteracSendingEmails.Add(new InteracSendingEmail { RouteId = s.Route.Id, FinancialAccountId = s.WalletId, Email = s.Transaction.SourceInformation!.CollectionEmail!,
            NormalizedEmail = s.Transaction.SourceInformation.CollectionEmail!, Status = "Verified", VerifiedAt = DateTime.UtcNow });
        db.InteracSenderOwnerships.Add(new InteracSenderOwnership { RouteId = s.Route.Id, FinancialAccountId = s.WalletId, NormalizedEmail = s.Transaction.SourceInformation.CollectionEmail! });
        await db.SaveChangesAsync(); await h.Deposits.TryProcessAsync(Payload(s), default);
        Assert.Equal("NeedsReview", receipt.Status); Assert.Null(receipt.CreditedAt);
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
        Assert.Contains(await db.AuditLogs.Where(x => x.EntityId == receipt.Id.ToString()).ToListAsync(), x => x.Action == "DEPOSIT_HELD_FOR_REVIEW");
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == s.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
    }

    [DatabaseIntegrationFact]
    public async Task Wrong_provider_wallet_and_preownership_deposit_never_credit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db); var h = Build(db, s);
        s.Transaction.BusinessWalletId = "different-wallet"; await h.Deposits.TryProcessAsync(Payload(s), default);
        Assert.Equal("NeedsReview", (await db.InteracDeposits.SingleAsync(x => x.ProviderTransactionId == s.Transaction.Id)).Status);
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == s.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
        s.Transaction.Id += "-old"; s.Transaction.BusinessWalletId = s.Route.ProviderWalletId; s.Transaction.Date = DateTime.UtcNow.AddHours(-1);
        await h.Deposits.TryProcessAsync(Payload(s), default);
        Assert.Contains("predates", (await db.InteracDeposits.SingleAsync(x => x.ProviderTransactionId == s.Transaction.Id)).ReviewReason);
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Verified_registration_email_is_default_and_removed_default_is_not_readded_on_GET()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db, false); var h = Build(db, s);
        var before = await h.Funding.GetAsync(s.UserId, s.WalletId, false, default);
        var sender = Assert.Single(before.SendingEmails); Assert.True(sender.IsDefault); Assert.Equal("Verified", sender.Status); Assert.True(before.Available);
        var after = await h.Funding.RemoveAsync(s.UserId, s.WalletId, false, sender.Id, default);
        Assert.Empty(after.SendingEmails); Assert.False(after.Available); Assert.Equal("Revoked", after.DefaultEmailStatus);
        var again = await h.Funding.GetAsync(s.UserId, s.WalletId, false, default); Assert.Empty(again.SendingEmails);
        Assert.Single(await db.InteracSenderOwnerships.Where(x => x.RouteId == s.Route.Id).ToListAsync());
        Assert.Equal(0, h.Provider.Calls); // Opening instructions performs no provider or monetary operation.
    }

    [DatabaseIntegrationFact]
    public async Task Business_cannot_claim_a_personal_verified_registration_email_even_before_first_funding_GET()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db, false); var h = Build(db, s);
        var added = await h.Funding.AddAsync(s.UserId, s.BusinessWalletId, true, s.RegistrationEmail, default);
        var sender = Assert.Single(added.SendingEmails); var code = await LatestCode(db, sender.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Funding.VerifyAsync(s.UserId, s.BusinessWalletId, true, sender.Id, code, default));
        Assert.Empty(await db.InteracSenderOwnerships.Where(x => x.RouteId == s.Route.Id && x.FinancialAccountId == s.BusinessWalletId).ToListAsync());
        var personal = await h.Funding.GetAsync(s.UserId, s.WalletId, false, default); Assert.Equal("Verified", personal.DefaultEmailStatus);
    }

    [DatabaseIntegrationFact]
    public async Task Verification_is_single_use_expiring_attempt_limited_and_resend_delayed()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db, false); var h = Build(db, s);
        var email = "verify-" + Guid.NewGuid().ToString("N") + "@example.test";
        var added = await h.Funding.AddAsync(s.UserId, s.BusinessWalletId, true, email, default); var sender = Assert.Single(added.SendingEmails);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Funding.ResendAsync(s.UserId, s.BusinessWalletId, true, sender.Id, default));
        var correct = await LatestCode(db, sender.Id); var wrong = correct == "000000" ? "999999" : "000000";
        for (var i = 0; i < 5; i++) await Assert.ThrowsAsync<InvalidOperationException>(() => h.Funding.VerifyAsync(s.UserId, s.BusinessWalletId, true, sender.Id, wrong, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Funding.VerifyAsync(s.UserId, s.BusinessWalletId, true, sender.Id, correct, default));
        var stored = await db.InteracSendingEmails.SingleAsync(x => x.Id == sender.Id); Assert.Equal(5, stored.FailedAttempts); Assert.Null(stored.CodeHash);
        stored.CanResendAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync(); await h.Funding.ResendAsync(s.UserId, s.BusinessWalletId, true, sender.Id, default);
        var replacement = await LatestCode(db, sender.Id); await h.Funding.VerifyAsync(s.UserId, s.BusinessWalletId, true, sender.Id, replacement, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Funding.VerifyAsync(s.UserId, s.BusinessWalletId, true, sender.Id, replacement, default));
        Assert.Single(await db.InteracSenderOwnerships.Where(x => x.RouteId == s.Route.Id && x.NormalizedEmail == email).ToListAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Business_cap_counts_pending_addresses_and_cross_wallet_access_is_rejected()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db, false); var h = Build(db, s);
        for (var i = 0; i < 5; i++) await h.Funding.AddAsync(s.UserId, s.BusinessWalletId, true, $"sender{i}-{Guid.NewGuid():N}@example.test", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Funding.AddAsync(s.UserId, s.BusinessWalletId, true, "sixth@example.test", default));
        Assert.Equal(5, await db.InteracSendingEmails.CountAsync(x => x.FinancialAccountId == s.BusinessWalletId && x.Status == "Pending"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Funding.GetAsync(s.UserId, s.BusinessWalletId, false, default));
    }

    [DatabaseIntegrationFact]
    public async Task Revalidation_failure_disables_stale_active_route_and_review_notes_never_change_money()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db, false); var h = Build(db, s);
        h.Provider.FailRead = true;
        var route = await h.Admin.VerifyAsync(s.UserId, s.Route.Id, new(s.Route.Revision, "Reverify local fake"), default);
        Assert.Equal("VerificationFailed", route.Status); Assert.Null(route.VerifiedAt);
        h.Provider.FailRead = false; s.Transaction.Id += "-unmatched"; await h.Deposits.TryProcessAsync(Payload(s), default);
        var receipt = await db.InteracDeposits.SingleAsync(x => x.ProviderTransactionId == s.Transaction.Id);
        var detail = await h.Admin.AddNoteAsync(s.UserId, receipt.Id, "Verified support evidence; retain for controlled resolution.", default);
        Assert.Single(detail.Notes); Assert.Null(detail.CreditedAt);
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_duplicate_callbacks_post_one_credit_in_separate_database_contexts()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db);
        async Task Process()
        {
            await using var separate = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options);
            await Build(separate, s).Deposits.TryProcessAsync(Payload(s), default);
        }
        await Task.WhenAll(Process(), Process());
        Assert.Equal(97.5m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
        Assert.Single(await db.LedgerTransactions.Where(x => x.IdempotencyKey == s.Transaction.Id).ToListAsync());
        var collection = await db.Collections.SingleAsync(x => x.ProviderCollectionId == s.Transaction.Id);
        await AssertFundingNotices(db, s.UserId, collection.Id, "97.50");
    }

    [DatabaseIntegrationFact]
    public async Task Rollback_after_SaveChanges_restores_unchanged_financial_identity_map_before_outer_save()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db);
        var outboxId = Guid.NewGuid();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var wallet = await db.FinancialAccounts.SingleAsync(x => x.Id == s.WalletId); wallet.AvailableBalance = 123m; wallet.SettledBalance = 123m;
            var created = new Collection { FinancialAccountId = wallet.Id, Purpose = PaymentOperationPurpose.AccountFunding,
                Reference = "ROLLBACK-" + Guid.NewGuid().ToString("N"), CurrencyCode = "CAD", Amount = 123m, PaymentMethod = PaymentMethod.Interac };
            var outbox = new BusinessWebhookEvent { Id = outboxId, BusinessProfileId = s.BusinessId,
                EventId = "evt_" + Guid.NewGuid().ToString("N"), EventType = "collection.completed", PayloadJson = "{}" };
            var notification = new NotificationMessage { UserId = s.UserId, Channel = NotificationChannels.Email,
                Recipient = s.RegistrationEmail, Subject = "Wallet funded", Body = "Rolled back credit", RelatedEntityType = WalletActivityNotificationFactory.FundingEntityType,
                RelatedEntityId = created.Id.ToString() };
            db.NotificationMessages.Add(notification);
            db.BusinessWebhookEvents.Add(outbox);
            db.Collections.Add(created); await db.SaveChangesAsync(); Assert.Equal(EntityState.Unchanged, db.Entry(created).State);
            await transaction.RollbackAsync(); await ProviderCollectionCreditGuard.RestoreMoneyTrackingAsync(db);
            Assert.Equal(EntityState.Detached, db.Entry(created).State);
            Assert.Equal(EntityState.Detached, db.Entry(outbox).State);
            Assert.Equal(EntityState.Detached, db.Entry(notification).State);
        }
        await db.SaveChangesAsync();
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
        Assert.Empty(await db.Collections.Where(x => x.FinancialAccountId == s.WalletId).ToListAsync());
        Assert.False(await db.BusinessWebhookEvents.AnyAsync(x => x.Id == outboxId));
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == s.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
    }

    [DatabaseIntegrationFact]
    public async Task Rolled_back_credit_does_not_leave_unsaved_success_notifications_for_outer_save()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var s = await Setup(db);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var collection = new Collection { FinancialAccountId = s.WalletId, Purpose = PaymentOperationPurpose.AccountFunding,
                Reference = "ROLLBACK-NOTICE-" + Guid.NewGuid().ToString("N"), CurrencyCode = "CAD", Amount = 12m,
                Status = CollectionStatus.Successful, PaymentMethod = PaymentMethod.Interac, ProviderCode = "Test" };
            db.Collections.Add(collection);
            var queue = new NotificationQueueService(db, Options.Create(new NotificationDeliveryOptions
                { Firebase = new FirebasePushDeliveryOptions { IsEnabled = true } }));
            var credits = new ConsumerWalletFundingCreditService(db, queue);
            await credits.ApplySuccessfulCollectionAsync(collection, null, "Test");
            await credits.ApplySuccessfulCollectionAsync(collection, null, "Test");
            Assert.Equal(2, db.NotificationMessages.Local.Count(x => x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
            Assert.Single(db.LedgerTransactions.Local.Where(x => x.RelatedEntityId == collection.Id));
            await transaction.RollbackAsync();
            await ProviderCollectionCreditGuard.RestoreMoneyTrackingAsync(db);
        }
        await db.SaveChangesAsync();
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == s.WalletId)).AvailableBalance);
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == s.UserId &&
            x.RelatedEntityType == WalletActivityNotificationFactory.FundingEntityType));
    }
}
