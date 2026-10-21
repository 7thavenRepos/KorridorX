using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Audit;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Models.Notifications;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Webhooks;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Audit;
using KorridorX.Services.DigitalAssets;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Notifications;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using KorridorX.Services.Wallets;
using KorridorX.Services.Webhooks;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

// Isolated PostgreSQL and synthetic signed receipts only. The provider proxy rejects
// every operation except read-only transaction verification. No SMTP/FCM is run.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class RetainedBankDepositRecoveryTests(ReleaseCandidateDatabaseFixture fixture)
{
    private const string Secret = "synthetic-retained-bank-recovery-signing-secret";
    private const string Reason = "Recover retained signed bank deposit after parser fix.";

    [DatabaseIntegrationFact]
    public async Task Preview_verifies_original_transaction_without_credit_or_receipt_mutation()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, "  " + Reason + "  ");
        Assert.Equal(scenario.TransactionId, preview.ProviderTransactionId);
        Assert.Equal(scenario.Bank.Id, preview.DepositAccountId);
        Assert.Equal(scenario.Wallet.Id, preview.FinancialAccountId);
        Assert.Equal(scenario.UserId, preview.OwnerId);
        Assert.Equal("User", preview.OwnerType);
        Assert.Equal("NGN", preview.CurrencyCode);
        Assert.Equal(1000m, preview.Amount);
        Assert.Equal(1000m, preview.CreditAmount);
        Assert.Equal(Reason, preview.Reason);
        Assert.False(preview.AlreadyCredited);
        Assert.Null(preview.CollectionId);
        Assert.Equal(1, scenario.Proxy.Calls);
        await AssertNoMoney(db, scenario);
        await AssertImmutableReceipt(db, scenario, WebhookProcessingStatus.Ignored);
        Assert.Single(await db.WebhookProcessingAttempts.Where(x => x.WebhookEventId == scenario.Receipt.Id).ToListAsync());
        Assert.False(await db.AuditLogs.AnyAsync(x => x.EntityId == scenario.Receipt.Id.ToString()));
    }

    [DatabaseIntegrationFact]
    public async Task Apply_repeat_and_distinct_signed_redelivery_credit_and_notify_exactly_once()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        var first = await service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken);
        var second = await service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken);
        Assert.False(first.AlreadyCredited);
        Assert.True(second.AlreadyCredited);
        Assert.Equal(first.CollectionId, second.CollectionId);
        var duplicate = await Signed(service.Webhook, Payload(scenario, eventId: Guid.NewGuid().ToString()));
        Assert.Equal("Processed", duplicate.ProcessingStatus);
        await AssertOneCredit(db, scenario);
        await AssertImmutableReceipt(db, scenario, WebhookProcessingStatus.Processed);
        var attempts = await db.WebhookProcessingAttempts.Where(x => x.WebhookEventId == scenario.Receipt.Id).OrderBy(x => x.StartedAt).ToListAsync();
        Assert.Equal(3, attempts.Count);
        Assert.Equal(WebhookProcessingStatus.Ignored, attempts[0].Status);
        Assert.All(attempts.Skip(1), x => Assert.Equal(WebhookProcessingStatus.Processed, x.Status));
        var audits = await db.AuditLogs.Where(x => x.EntityId == scenario.Receipt.Id.ToString()).ToListAsync();
        Assert.Equal(2, audits.Count);
        Assert.All(audits, x => { Assert.Equal("PROVIDER_BANK_DEPOSIT_RECOVERED", x.Action); Assert.Equal(scenario.UserId, x.UserId); Assert.Contains(Reason, x.MetadataJson); });
    }

    [DatabaseIntegrationFact]
    public async Task Failed_retained_event_is_recovered_without_a_provider_resend()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db, WebhookProcessingStatus.Failed);
        var service = Build(db, scenario);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        await service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken);
        await AssertOneCredit(db, scenario);
        await AssertImmutableReceipt(db, scenario, WebhookProcessingStatus.Processed);
    }

    [DatabaseIntegrationFact]
    public async Task Separate_new_deposit_does_not_mask_or_duplicate_the_original_recovery()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        var original = scenario.Proxy.Result;
        var newTransaction = Guid.NewGuid().ToString();
        scenario.Proxy.Result = original with { ProviderTransactionId = newTransaction,
            RawResponseJson = original.RawResponseJson.Replace(scenario.TransactionId, newTransaction, StringComparison.Ordinal) };
        await Signed(service.Webhook, Payload(scenario, transactionId: newTransaction, eventId: Guid.NewGuid().ToString()));
        scenario.Proxy.Result = original;
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        Assert.False(preview.AlreadyCredited);
        await service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken);
        Assert.Equal(2000m, await Balance(db, scenario));
        Assert.Equal(2, await db.Collections.CountAsync(x => x.FinancialAccountId == scenario.Wallet.Id));
        Assert.Equal(2, await db.LedgerTransactions.CountAsync(x => x.ContextEntityId == scenario.Wallet.Id));
        Assert.Equal(1, await db.ProviderTransactions.CountAsync(x => x.ProviderTransactionId == scenario.TransactionId));
        Assert.Equal(1, await db.ProviderTransactions.CountAsync(x => x.ProviderTransactionId == newTransaction));
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_recoveries_in_independent_contexts_share_one_credit_claim()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var preview = await Build(db, scenario).Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        await using var firstDb = NewContext(db);
        await using var secondDb = NewContext(db);
        var first = Build(firstDb, scenario).Recovery;
        var second = Build(secondDb, scenario).Recovery;
        var gate = new AsyncVerificationBarrier(2);
        scenario.Proxy.BeforeVerify = gate.Arrive;
        var results = await Task.WhenAll(
            first.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken),
            second.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken));
        Assert.Single(results.Where(x => !x.AlreadyCredited));
        Assert.Single(results.Where(x => x.AlreadyCredited));
        Assert.Equal(results[0].CollectionId, results[1].CollectionId);
        await AssertOneCredit(db, scenario);
        Assert.Equal(3, await db.WebhookProcessingAttempts.CountAsync(x => x.WebhookEventId == scenario.Receipt.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_live_redelivery_and_recovery_cannot_double_credit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var preview = await Build(db, scenario).Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        await using var recoveryDb = NewContext(db);
        await using var webhookDb = NewContext(db);
        var gate = new AsyncVerificationBarrier(2);
        scenario.Proxy.BeforeVerify = gate.Arrive;
        // Different event ID, identical provider transaction: separate handlers must
        // still use the same transaction lock, claim and wallet ledger identity.
        await Task.WhenAll(
            Build(recoveryDb, scenario).Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken),
            Signed(Build(webhookDb, scenario).Webhook, Payload(scenario, eventId: Guid.NewGuid().ToString())));
        await AssertOneCredit(db, scenario);
        await AssertImmutableReceipt(db, scenario, WebhookProcessingStatus.Processed);
    }

    [DatabaseIntegrationFact]
    public async Task Same_event_signed_redelivery_wins_without_recovery_overwriting_its_receipt()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var preview = await Build(db, scenario).Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        await using var recoveryDb = NewContext(db);
        await using var webhookDb = NewContext(db);
        var gate = new AsyncVerificationBarrier(2);
        scenario.Proxy.BeforeVerify = gate.Arrive;
        var recoveryTask = Record.ExceptionAsync(() => Build(recoveryDb, scenario).Recovery
            .ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken));
        await gate.FirstArrival.WaitAsync(TimeSpan.FromSeconds(20));
        // Recovery is already waiting at fresh verification; the public webhook now
        // claims the same retained event and supplies genuinely newer signed headers.
        var liveTask = Signed(Build(webhookDb, scenario).Webhook, scenario.OriginalPayload);
        await Task.WhenAll(recoveryTask, liveTask);
        Assert.IsType<InvalidOperationException>(await recoveryTask);
        Assert.Equal("Processed", (await liveTask).ProcessingStatus);
        await AssertOneCredit(db, scenario);
        var receipt = await db.WebhookEvents.AsNoTracking().SingleAsync(x => x.Id == scenario.Receipt.Id);
        Assert.Equal(WebhookProcessingStatus.Processed, receipt.ProcessingStatus);
        Assert.Equal(scenario.OriginalPayload, receipt.RawPayloadJson);
        Assert.False(await db.AuditLogs.AnyAsync(x => x.EntityId == scenario.Receipt.Id.ToString() &&
            x.Action == "PROVIDER_BANK_DEPOSIT_RECOVERED"));
    }

    [DatabaseIntegrationFact]
    public async Task Live_processing_status_is_preserved_when_recovery_cannot_claim_the_receipt()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        await db.WebhookEvents.Where(x => x.Id == scenario.Receipt.Id).ExecuteUpdateAsync(x =>
            x.SetProperty(p => p.ProcessingStatus, WebhookProcessingStatus.Processing));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken));
        await AssertImmutableReceipt(db, scenario, WebhookProcessingStatus.Processing);
        await AssertNoMoney(db, scenario);
        Assert.Equal("PROVIDER_BANK_DEPOSIT_RECOVERY_FAILED", (await db.AuditLogs.SingleAsync(x => x.EntityId == scenario.Receipt.Id.ToString())).Action);
    }

    [DatabaseIntegrationFact]
    public async Task Expired_tampered_wrong_actor_and_wrong_event_tokens_cannot_start_a_recovery()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var service = Build(db, scenario, clock);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.ApplyAsync(scenario.Receipt.Id, Guid.NewGuid(), preview.PreviewToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.ApplyAsync(Guid.NewGuid(), scenario.UserId, preview.PreviewToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, "tampered" + preview.PreviewToken));
        clock.Current = clock.Current.AddMinutes(16);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken));
        Assert.Equal(1, scenario.Proxy.Calls);
        Assert.Single(await db.WebhookProcessingAttempts.Where(x => x.WebhookEventId == scenario.Receipt.Id).ToListAsync());
        await AssertNoMoney(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Preview_requires_a_reason_and_authenticates_the_original_signature_at_receipt_time()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, "  "));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, new string('x', 1001)));
        scenario.Receipt.SignatureHeader = "not-the-provider-signature";
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason));
        Assert.Contains("signature", error.Message);
        Assert.Equal(0, scenario.Proxy.Calls);
        await AssertNoMoney(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Actual_persisted_redelivery_time_authenticates_latest_retained_signature()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db, WebhookProcessingStatus.Failed);
        var retryAt = DateTime.UtcNow.AddMinutes(-10);
        scenario.Receipt.TimestampHeader = new DateTimeOffset(retryAt).ToUnixTimeSeconds().ToString();
        scenario.Receipt.SignatureHeader = BlaaizWebhookSignature.Create(scenario.Receipt.TimestampHeader, scenario.Receipt.RawPayloadJson, Secret);
        db.WebhookProcessingAttempts.Add(new WebhookProcessingAttempt { WebhookEventId = scenario.Receipt.Id,
            Status = WebhookProcessingStatus.Failed, StartedAt = retryAt, FinishedAt = retryAt });
        await db.SaveChangesAsync();
        var preview = await Build(db, scenario).Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        Assert.Equal(scenario.TransactionId, preview.ProviderTransactionId);
        await AssertNoMoney(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Fresh_provider_mismatch_after_preview_retains_failed_attempt_without_money()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        scenario.Proxy.Result = scenario.Proxy.Result with { Amount = 999m };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken));
        await AssertNoMoney(db, scenario);
        await AssertImmutableReceipt(db, scenario, WebhookProcessingStatus.Failed);
        var attempts = await db.WebhookProcessingAttempts.Where(x => x.WebhookEventId == scenario.Receipt.Id).OrderBy(x => x.StartedAt).ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.Equal(WebhookProcessingStatus.Ignored, attempts[0].Status);
        Assert.Equal(WebhookProcessingStatus.Failed, attempts[1].Status);
        Assert.Equal("PROVIDER_BANK_DEPOSIT_RECOVERY_FAILED", (await db.AuditLogs.SingleAsync(x => x.EntityId == scenario.Receipt.Id.ToString())).Action);
    }

    [DatabaseIntegrationFact]
    public async Task Changed_receiving_account_details_invalidate_preview_without_credit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        scenario.Bank.AccountName = "Different receiver details";
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken));
        Assert.Contains("preview", error.Message);
        await AssertNoMoney(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Prior_connection_account_and_unapproved_owner_are_not_recovered()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        scenario.Bank.ConnectionKey = new string('D', 64);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason));
        scenario.Bank.ConnectionKey = scenario.Resolver.ConnectionKey;
        var owner = await db.CustomerProfiles.SingleAsync(x => x.UserId == scenario.UserId);
        owner.KycStatus = KycStatus.Rejected;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason));
        Assert.Equal(0, scenario.Proxy.Calls);
        await AssertNoMoney(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Save_failure_after_completed_callback_rolls_back_money_notices_and_success_audit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var preview = await Build(db, scenario).Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        var interceptor = new FailCompletedRecoveryOnce();
        await using var failingDb = NewContext(db, interceptor);
        var failing = Build(failingDb, scenario);
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken));
        Assert.True(interceptor.Failed);
        await AssertNoMoney(db, scenario);
        await AssertImmutableReceipt(db, scenario, WebhookProcessingStatus.Failed);
        var attempts = await db.WebhookProcessingAttempts.Where(x => x.WebhookEventId == scenario.Receipt.Id).ToListAsync();
        Assert.Equal(2, attempts.Count);
        Assert.DoesNotContain(attempts, x => x.Status == WebhookProcessingStatus.Processed);
        var audit = Assert.Single(await db.AuditLogs.Where(x => x.EntityId == scenario.Receipt.Id.ToString()).ToListAsync());
        Assert.Equal("PROVIDER_BANK_DEPOSIT_RECOVERY_FAILED", audit.Action);
        // Retry the same still-valid preview after the failed database transaction.
        await failing.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken);
        await AssertOneCredit(db, scenario);
    }

    [DatabaseIntegrationFact]
    public async Task Reversed_or_deleted_ledger_is_not_reported_as_completed_credit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scenario = await Setup(db);
        var service = Build(db, scenario);
        var preview = await service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason);
        var result = await service.Recovery.ApplyAsync(scenario.Receipt.Id, scenario.UserId, preview.PreviewToken);
        var ledger = await db.LedgerTransactions.SingleAsync(x => x.RelatedEntityId == result.CollectionId);
        ledger.ReversedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason));
        ledger.ReversedAt = null;
        ledger.IsDeleted = true;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Recovery.PreviewAsync(scenario.Receipt.Id, scenario.UserId, Reason));
        Assert.Equal(1000m, await Balance(db, scenario));
        Assert.Single(await db.ProviderTransactions.Where(x => x.ProviderTransactionId == scenario.TransactionId).ToListAsync());
    }

    private static async Task AssertNoMoney(AppDbContext db, Scenario scenario)
    {
        Assert.Equal(0m, await Balance(db, scenario));
        Assert.False(await db.Collections.AnyAsync(x => x.FinancialAccountId == scenario.Wallet.Id));
        Assert.False(await db.LedgerTransactions.AnyAsync(x => x.ContextEntityId == scenario.Wallet.Id));
        Assert.False(await db.ProviderTransactions.AnyAsync(x => x.ProviderTransactionId == scenario.TransactionId));
        Assert.False(await db.Set<ProviderCollectionCreditClaim>().AnyAsync(x => x.ProviderTransactionId == scenario.TransactionId));
        Assert.False(await db.NotificationMessages.AnyAsync(x => x.UserId == scenario.UserId));
    }

    private static async Task AssertOneCredit(AppDbContext db, Scenario scenario)
    {
        Assert.Equal(1000m, await Balance(db, scenario));
        var collection = Assert.Single(await db.Collections.AsNoTracking().Where(x => x.FinancialAccountId == scenario.Wallet.Id).ToListAsync());
        Assert.Equal(scenario.TransactionId, collection.ProviderCollectionId);
        Assert.Single(await db.LedgerTransactions.Where(x => x.RelatedEntityId == collection.Id).ToListAsync());
        Assert.Equal(2, await db.LedgerPostings.CountAsync(x => x.LedgerTransactionId ==
            db.LedgerTransactions.Where(t => t.RelatedEntityId == collection.Id).Select(t => t.Id).Single()));
        Assert.Single(await db.ProviderTransactions.Where(x => x.ProviderTransactionId == scenario.TransactionId).ToListAsync());
        Assert.Single(await db.Set<ProviderCollectionCreditClaim>().Where(x => x.ProviderTransactionId == scenario.TransactionId).ToListAsync());
        var notices = await db.NotificationMessages.Where(x => x.UserId == scenario.UserId).ToListAsync();
        Assert.Equal(2, notices.Count);
        Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Email));
        Assert.Single(notices.Where(x => x.Channel == NotificationChannels.Push));
    }

    private static Task<decimal> Balance(AppDbContext db, Scenario scenario) => db.FinancialAccounts.AsNoTracking()
        .Where(x => x.Id == scenario.Wallet.Id).Select(x => x.AvailableBalance).SingleAsync();

    private static async Task AssertImmutableReceipt(AppDbContext db, Scenario scenario, WebhookProcessingStatus status)
    {
        var receipt = await db.WebhookEvents.AsNoTracking().SingleAsync(x => x.Id == scenario.Receipt.Id);
        Assert.Equal(scenario.OriginalPayload, receipt.RawPayloadJson);
        Assert.Equal(scenario.OriginalSignature, receipt.SignatureHeader);
        Assert.Equal(scenario.OriginalTimestamp, receipt.TimestampHeader);
        Assert.Equal(scenario.OriginalReceivedAt, receipt.ReceivedAt);
        Assert.Equal(status, receipt.ProcessingStatus);
    }

    private static AppDbContext NewContext(AppDbContext db, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(db.Database.GetConnectionString());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new AppDbContext(options.Options);
    }

    private static async Task<Scenario> Setup(AppDbContext db, WebhookProcessingStatus status = WebhookProcessingStatus.Ignored)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        db.Users.Add(new ApplicationUser { Id = userId, UserName = "recovery-" + suffix, NormalizedUserName = "RECOVERY-" + suffix,
            Email = suffix + "@example.test", NormalizedEmail = suffix + "@EXAMPLE.TEST", EmailConfirmed = true, SecurityStamp = suffix });
        db.CustomerProfiles.Add(new CustomerProfile { UserId = userId, CountryCode = "NG", FirstName = "Synthetic", LastName = "Recovery", KycStatus = KycStatus.Approved });
        db.MobilePushDevices.Add(new MobilePushDevice { UserId = userId, Platform = "android", PushToken = "fake-" + suffix,
            PushTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(suffix))), DeviceFingerprint = suffix });
        var resolver = new Resolver();
        var wallet = new FinancialAccount { OwnerType = FinancialAccountOwnerType.User, OwnerId = userId, AssetCode = "NGN",
            AccountCode = "recovery-" + suffix, AccountType = FinancialAccountType.Customer, Status = FinancialAccountStatus.Active };
        var providerWallet = new ProviderWalletConfiguration { ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName,
            AssetCode = "NGN", ProviderWalletId = Guid.NewGuid().ToString(), DisplayName = "Synthetic NGN", NetworkCode = "" };
        var channel = new PaymentChannel { Environment = resolver.EnvironmentName, AssetCode = "NGN", CountryCode = "*",
            Direction = PaymentChannelDirection.Deposit, CapabilityCode = "virtual-account", DisplayName = "Synthetic bank deposits",
            ProviderWalletConfigurationId = providerWallet.Id, IndividualEnabled = true, HasBeenActivated = true, Status = PaymentChannelStatus.Active };
        var bank = new WalletDepositAccount { FinancialAccountId = wallet.Id, PaymentChannelId = channel.Id,
            ProviderWalletConfigurationId = providerWallet.Id, ProviderWalletId = providerWallet.ProviderWalletId,
            ProviderCustomerId = Guid.NewGuid().ToString(), ProviderAccountId = Guid.NewGuid().ToString(),
            Environment = resolver.EnvironmentName, ConnectionKey = resolver.ConnectionKey, AssetCode = "NGN", Status = "Active",
            AccountNumber = "0123456789", AccountName = "Synthetic Recovery", BankName = "Synthetic Bank", CreatedByUserId = userId };
        db.AddRange(wallet, providerWallet, channel, bank);
        var provider = DispatchProxy.Create<IRemittanceProvider, ProviderProxy>();
        var proxy = (ProviderProxy)(object)provider;
        var transactionId = Guid.NewGuid().ToString();
        proxy.Result = new(transactionId, "synthetic-retained-reference", "collection", "SUCCESSFUL", "NGN", 1000m, null, null,
            JsonSerializer.Serialize(new { data = new { id = transactionId, customer_id = bank.ProviderCustomerId,
                wallet_id = bank.ProviderWalletId, virtual_bank_account = new { id = bank.ProviderAccountId, account_number = bank.AccountNumber } } }), Guid.NewGuid());
        // PostgreSQL timestamps have microsecond precision, so round before retaining.
        var receivedAt = new DateTime(DateTime.UtcNow.AddHours(-2).Ticks / 10 * 10, DateTimeKind.Utc);
        var receipt = new WebhookEvent { ProviderCode = ProviderCode.Blaaiz, ProviderEventId = Guid.NewGuid().ToString(),
            EventType = "collection.completed", ProcessingStatus = status, ReceivedAt = receivedAt, ProcessedAt = receivedAt };
        var scenario = new Scenario(wallet, bank, receipt, userId, transactionId, provider, proxy, resolver,
            new EphemeralDataProtectionProvider(), "", "", "", receivedAt);
        receipt.RawPayloadJson = Payload(scenario);
        receipt.TimestampHeader = new DateTimeOffset(receivedAt).ToUnixTimeSeconds().ToString();
        receipt.SignatureHeader = BlaaizWebhookSignature.Create(receipt.TimestampHeader, receipt.RawPayloadJson, Secret);
        receipt.Attempts.Add(new WebhookProcessingAttempt { WebhookEventId = receipt.Id, Status = status, StartedAt = receivedAt, FinishedAt = receivedAt });
        db.WebhookEvents.Add(receipt);
        await db.SaveChangesAsync();
        return scenario with { OriginalPayload = receipt.RawPayloadJson, OriginalSignature = receipt.SignatureHeader,
            OriginalTimestamp = receipt.TimestampHeader };
    }

    private static Bundle Build(AppDbContext db, Scenario scenario, TimeProvider? clock = null)
    {
        var settings = Options.Create(new BlaaizOptions { IsEnabled = true, WebhookSigningSecret = Secret });
        var notifications = new NotificationQueueService(db, Options.Create(new NotificationDeliveryOptions
            { Firebase = new FirebasePushDeliveryOptions { IsEnabled = true } }));
        var channels = new PaymentChannelService(db, scenario.Resolver, settings, new CollectionPaymentMethodPolicy());
        var deposits = new WalletDepositAccountService(db, channels, null!, scenario.Resolver, null!, scenario.Provider,
            new ConsumerWalletFundingCreditService(db, notifications), notifications);
        var recovery = new RetainedBankDepositRecoveryService(db, scenario.Resolver, scenario.Provider, deposits,
            new AuditService(db, new HttpContextAccessor()), scenario.Protection, settings, clock);
        var webhook = new BlaaizWebhookService(db, settings, null!, null!, scenario.Provider, null!, new NoEmbeddedDeposits(),
            null!, new NoDigitalDeposits(), null!, deposits,
            new InteracDepositService(db, scenario.Resolver, null!, new ProviderCollectionCreditGuard(db, scenario.Resolver, null!), null!), null!);
        return new(recovery, webhook);
    }

    private static string Payload(Scenario scenario, string? transactionId = null, string? eventId = null) => JsonSerializer.Serialize(new
    {
        transaction_id = transactionId ?? scenario.TransactionId, transaction_reference = "synthetic-retained-reference",
        transaction_status = "SUCCESSFUL", transaction_amount = 1000, transaction_amount_without_fee = 1000,
        transaction_fee = 0, transaction_currency = "NGN", customer_id = scenario.Bank.ProviderCustomerId,
        virtual_bank_account = new { id = scenario.Bank.ProviderAccountId, account_number = scenario.Bank.AccountNumber },
        event_id = eventId ?? scenario.Receipt.ProviderEventId, type = "collection"
    });

    private static Task<BlaaizWebhookResult> Signed(BlaaizWebhookService webhook, string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        return webhook.ProcessCollectionWebhookAsync(payload, BlaaizWebhookSignature.Create(timestamp, payload, Secret), timestamp);
    }

    private sealed record Scenario(FinancialAccount Wallet, WalletDepositAccount Bank, WebhookEvent Receipt, Guid UserId,
        string TransactionId, IRemittanceProvider Provider, ProviderProxy Proxy, Resolver Resolver, IDataProtectionProvider Protection,
        string OriginalPayload, string OriginalSignature, string OriginalTimestamp, DateTime OriginalReceivedAt);
    private sealed record Bundle(RetainedBankDepositRecoveryService Recovery, BlaaizWebhookService Webhook);
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    { public DateTimeOffset Current { get; set; } = now; public override DateTimeOffset GetUtcNow() => Current; }
    private sealed class Resolver : IProviderWalletResolver
    {
        public string EnvironmentName { get; } = "BankRecovery-" + Guid.NewGuid().ToString("N");
        public string ConnectionKey => new('C', 64);
        public Task<string> SelectAsync(string a, Guid b, string c, string d, string? e, string f, bool g, CancellationToken ct = default) =>
            throw new InvalidOperationException("No provider account creation is allowed in retained bank recovery tests.");
    }
    public class ProviderProxy : DispatchProxy
    {
        public RemittanceTransactionStatusResult Result { get; set; } = null!;
        public Func<Task>? BeforeVerify { get; set; }
        private int _calls;
        public int Calls => _calls;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != nameof(IRemittanceProvider.GetTransactionAsync))
                throw new InvalidOperationException("Only read-only provider transaction verification is allowed.");
            Assert.Equal(Result.ProviderTransactionId, args![0]);
            Interlocked.Increment(ref _calls);
            return Read();
        }
        private async Task<RemittanceTransactionStatusResult> Read()
        { if (BeforeVerify is not null) await BeforeVerify(); return Result; }
    }
    private sealed class AsyncVerificationBarrier(int count)
    {
        private int _remaining = count;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstArrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task FirstArrival => _firstArrival.Task;
        public async Task Arrive()
        {
            _firstArrival.TrySetResult();
            if (Interlocked.Decrement(ref _remaining) == 0) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }
    private sealed class FailCompletedRecoveryOnce : SaveChangesInterceptor
    {
        public bool Failed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Failed && eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(x =>
                    x.State == EntityState.Added && x.Entity.Action == "PROVIDER_BANK_DEPOSIT_RECOVERED"))
            { Failed = true; throw new InvalidOperationException("Synthetic SaveChanges failure after recovery completion callback."); }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
    private sealed class NoEmbeddedDeposits : IEmbeddedInboundCollectionService
    {
        public Task<bool> TryProcessBlaaizDepositAsync(EmbeddedInboundCollectionRequest request, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
    private sealed class NoDigitalDeposits : IDigitalAssetDepositIntentSettlementService
    {
        public Task<bool> TryProcessProviderCollectionAsync(string providerCode, string providerCollectionId, string? providerReference,
            string providerStatus, string? assetCode, decimal? amount, string rawPayload, DateTime occurredAt, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
