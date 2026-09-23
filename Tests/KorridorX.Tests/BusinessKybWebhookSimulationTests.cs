using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.BackgroundJobs;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Compliance;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Notifications;
using KorridorX.Models.Providers;
using KorridorX.Models.Webhooks;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Compliance;
using KorridorX.Services.Notifications;
using KorridorX.Services.Webhooks;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

// Real HTTP routing, signature verification, services and PostgreSQL persistence.
// Only provider responses and email delivery are test doubles. No staging/live call.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessKybWebhookSimulationTests(ReleaseCandidateDatabaseFixture fixture)
{
    private const string Secret = "isolated-kyb-simulation-signing-secret-not-a-provider-credential";
    private const string Portal = "https://business.example.test";

    [DatabaseIntegrationFact]
    public async Task Signed_approval_HTTP_updates_KYB_and_delivers_one_owner_email()
    {
        var s = await Seed();
        var provider = Provider(s, "VERIFIED");
        var capture = new CaptureDelivery();
        using var app = Application(provider, capture: capture);
        using var client = app.CreateClient();
        var payload = Payload(s, "VERIFIED");
        var first = await Send(client, payload);
        Assert.Equal("Processed", first.ProcessingStatus);
        Assert.False(first.IsDuplicate);
        var replay = await Send(client, payload);
        Assert.True(replay.IsDuplicate);
        await Send(client, Payload(s, "VERIFIED")); // Same state, a different event ID.

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = Db(scope);
            Assert.Equal(KybStatus.Approved, (await db.BusinessKybApplications.FindAsync(s.ApplicationId))!.Status);
            Assert.Equal(KybStatus.Approved, (await db.BusinessProfiles.FindAsync(s.BusinessId))!.KybStatus);
            Assert.Equal("VERIFIED", (await db.ProviderCustomers.SingleAsync(x => x.BusinessProfileId == s.BusinessId)).ProviderStatus);
            var mail = Assert.Single(await Messages(db, s));
            Assert.Equal(s.OwnerId, mail.UserId);
            Assert.Equal(s.OwnerEmail, mail.Recipient);
            Assert.NotEqual(s.MemberEmail, mail.Recipient);
            Assert.Contains("approved", mail.Subject);
            Assert.Contains(Portal + "/business/verification", mail.Body);
            Assert.Equal(NotificationStatuses.Pending, mail.Status);
            // Make this scenario's email the first due item in the shared fixture.
            mail.CreatedAt = DateTime.UnixEpoch.AddDays(-100);
            await db.SaveChangesAsync();
        }
        await DeliverOne(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var mail = Assert.Single(await Messages(Db(scope), s));
            Assert.Equal(NotificationStatuses.Sent, mail.Status);
            Assert.Equal(1, mail.AttemptCount);
            Assert.NotNull(mail.SentAt);
            Assert.Contains(mail.Id, capture.Delivered);
        }
        Assert.Equal(2, provider.ReadCalls);
    }

    [DatabaseIntegrationFact]
    public async Task Signed_rejection_keeps_feedback_in_portal_and_sends_safe_owner_notice()
    {
        var s = await Seed();
        using var app = Application(Provider(s, "REJECTED"));
        using var client = app.CreateClient();
        await Send(client, Payload(s, "REJECTED", comment: "Private passport review detail"));
        await using var scope = app.Services.CreateAsyncScope();
        var db = Db(scope);
        var application = await db.BusinessKybApplications.FindAsync(s.ApplicationId);
        Assert.Equal(KybStatus.Rejected, application!.Status);
        Assert.Contains("Private passport", application.ReviewNote);
        var mail = Assert.Single(await Messages(db, s));
        Assert.Contains("needs attention", mail.Subject);
        Assert.DoesNotContain("passport", mail.Body);
    }

    [DatabaseIntegrationFact]
    public async Task Missing_bad_and_expired_signatures_cannot_change_KYB_or_queue_email()
    {
        var s = await Seed();
        var provider = Provider(s, "VERIFIED");
        using var app = Application(provider);
        using var client = app.CreateClient();
        foreach (var mode in new[] { "missing", "bad", "expired" })
        {
            using var response = await Post(client, Payload(s, "VERIFIED"), mode);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal(KybStatus.UnderReview, (await Db(scope).BusinessKybApplications.FindAsync(s.ApplicationId))!.Status);
        Assert.Empty(await Messages(Db(scope), s));
        Assert.Equal(0, provider.ReadCalls);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_must_confirm_approval_then_identical_failed_event_can_retry()
    {
        var s = await Seed();
        var provider = Provider(s, "PROCESSING");
        using var app = Application(provider);
        using var client = app.CreateClient();
        var eventId = "event-" + Guid.NewGuid().ToString("N");
        var payload = Payload(s, "VERIFIED", eventId: eventId);
        using (var failed = await Post(client, payload)) Assert.False(failed.IsSuccessStatusCode);
        await AssertUnchangedAndFailed(app, s, eventId);
        provider.Status = "VERIFIED";
        var retry = await Send(client, payload);
        Assert.Equal("Processed", retry.ProcessingStatus);
        Assert.False(retry.IsDuplicate);
        await using var scope = app.Services.CreateAsyncScope();
        var receipt = await Db(scope).WebhookEvents.Include(x => x.Attempts).SingleAsync(x => x.ProviderEventId == eventId);
        Assert.Equal(2, receipt.Attempts.Count);
        Assert.Single(await Messages(Db(scope), s));
    }

    [DatabaseIntegrationFact]
    public async Task Notification_failure_rolls_back_status_and_receipt_can_be_retried()
    {
        var s = await Seed();
        var failure = new FailureControl { Fail = true };
        using var app = Application(Provider(s, "VERIFIED"), failure: failure);
        using var client = app.CreateClient();
        var eventId = "event-" + Guid.NewGuid().ToString("N");
        var payload = Payload(s, "VERIFIED", eventId: eventId);
        using (var failed = await Post(client, payload)) Assert.False(failed.IsSuccessStatusCode);
        await AssertUnchangedAndFailed(app, s, eventId);
        failure.Fail = false;
        Assert.Equal("Processed", (await Send(client, payload)).ProcessingStatus);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal(KybStatus.Approved, (await Db(scope).BusinessProfiles.FindAsync(s.BusinessId))!.KybStatus);
        Assert.Single(await Messages(Db(scope), s));
    }

    [DatabaseIntegrationFact]
    public async Task Older_decision_cannot_overwrite_approval_or_send_a_rejection_email()
    {
        var s = await Seed();
        var provider = Provider(s, "VERIFIED");
        using var app = Application(provider);
        using var client = app.CreateClient();
        var decision = DateTime.UtcNow.AddMinutes(-1);
        await Send(client, Payload(s, "VERIFIED", occurredAt: decision));
        var old = await Send(client, Payload(s, "REJECTED", occurredAt: decision.AddMinutes(-1)));
        Assert.Equal("Ignored", old.ProcessingStatus);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Equal(KybStatus.Approved, (await Db(scope).BusinessKybApplications.FindAsync(s.ApplicationId))!.Status);
        Assert.Single(await Messages(Db(scope), s));
        Assert.Equal(1, provider.ReadCalls);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_different_event_ids_for_one_approval_queue_one_email()
    {
        var s = await Seed();
        using var app = Application(Provider(s, "VERIFIED"));
        using var client = app.CreateClient();
        var timestamp = DateTime.UtcNow.AddSeconds(-1);
        var results = await Task.WhenAll(Send(client, Payload(s, "VERIFIED", occurredAt: timestamp)),
            Send(client, Payload(s, "VERIFIED", occurredAt: timestamp)));
        Assert.All(results, r => Assert.Equal("Processed", r.ProcessingStatus));
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Single(await Messages(Db(scope), s));
    }

    [DatabaseIntegrationFact]
    public async Task Successful_submission_and_processing_callback_send_one_receipt()
    {
        var s = await Seed(KybStatus.Pending);
        var provider = Provider(s, "PENDING");
        using var app = Application(provider);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IBusinessKybService>().SubmitAsync(s.OwnerId, s.ApplicationId);
            Assert.Equal(KybStatus.UnderReview, result.Status);
        }
        using var client = app.CreateClient();
        await Send(client, Payload(s, "PROCESSING"));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var mail = Assert.Single(await Messages(Db(scope), s));
            Assert.Contains("under review", mail.Subject);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<IBusinessKybService>().SubmitAsync(s.OwnerId, s.ApplicationId));
        }
        Assert.Equal(1, provider.SubmitCalls);
    }

    [DatabaseIntegrationFact]
    public async Task Resubmission_can_send_a_new_receipt_and_a_new_decision()
    {
        var s = await Seed();
        var provider = Provider(s, "REJECTED");
        using var app = Application(provider);
        using var client = app.CreateClient();
        await Send(client, Payload(s, "REJECTED"));
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IBusinessKybService>().SubmitAsync(s.OwnerId, s.ApplicationId);
        provider.Status = "VERIFIED";
        await Send(client, Payload(s, "VERIFIED"));
        await using var verify = app.Services.CreateAsyncScope();
        var messages = await Messages(Db(verify), s);
        Assert.Equal(3, messages.Count);
        Assert.Single(messages, x => x.Subject.Contains("needs attention"));
        Assert.Single(messages, x => x.Subject.Contains("under review"));
        Assert.Single(messages, x => x.Subject.Contains("approved"));
    }

    [DatabaseIntegrationFact]
    public async Task Unknown_customer_is_ignored_without_provider_or_notification_contact()
    {
        var s = await Seed();
        var provider = Provider(s, "VERIFIED");
        using var app = Application(provider);
        using var client = app.CreateClient();
        var result = await Send(client, Payload(s with { CustomerId = "unmapped-" + Guid.NewGuid() }, "VERIFIED"));
        Assert.Equal("Ignored", result.ProcessingStatus);
        Assert.Equal(0, provider.ReadCalls);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Empty(await Messages(Db(scope), s));
    }

    [DatabaseIntegrationFact]
    public async Task A_processed_event_id_cannot_be_reused_with_a_changed_payload()
    {
        var s = await Seed();
        using var app = Application(Provider(s, "VERIFIED"));
        using var client = app.CreateClient();
        var eventId = "event-" + Guid.NewGuid().ToString("N");
        await Send(client, Payload(s, "VERIFIED", eventId: eventId));
        using var changed = await Post(client, Payload(s, "REJECTED", eventId: eventId));
        Assert.False(changed.IsSuccessStatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.Single(await Messages(Db(scope), s));
    }

    [DatabaseIntegrationFact]
    public async Task Delivery_failure_uses_existing_retry_without_repeating_KYB_transition()
    {
        var s = await Seed();
        var capture = new CaptureDelivery { Fail = true };
        using var app = Application(Provider(s, "VERIFIED"), capture: capture);
        using var client = app.CreateClient();
        await Send(client, Payload(s, "VERIFIED"));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var mail = Assert.Single(await Messages(Db(scope), s));
            mail.CreatedAt = DateTime.UnixEpoch.AddDays(-100);
            await Db(scope).SaveChangesAsync();
        }
        await DeliverOne(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = Db(scope);
            var mail = Assert.Single(await Messages(db, s));
            Assert.Equal(NotificationStatuses.Retry, mail.Status);
            Assert.Equal(1, mail.AttemptCount);
            Assert.NotNull(mail.NextAttemptAt);
            Assert.Equal(KybStatus.Approved, (await db.BusinessProfiles.FindAsync(s.BusinessId))!.KybStatus);
            mail.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        capture.Fail = false;
        await DeliverOne(app);
        await using var verify = app.Services.CreateAsyncScope();
        var delivered = Assert.Single(await Messages(Db(verify), s));
        Assert.Equal(NotificationStatuses.Sent, delivered.Status);
        Assert.Equal(2, delivered.AttemptCount);
    }

    private async Task<Scenario> Seed(KybStatus status = KybStatus.UnderReview)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = Db(scope);
        var unique = Guid.NewGuid().ToString("N");
        var owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = "owner-" + unique,
            Email = "owner-" + unique + "@example.test", EmailConfirmed = true, CountryCode = "CA", UserType = UserType.Business };
        var member = new ApplicationUser { Id = Guid.NewGuid(), UserName = "member-" + unique,
            Email = "member-" + unique + "@example.test", CountryCode = "CA", UserType = UserType.Business };
        var business = new BusinessProfile { OwnerUser = owner, BusinessName = "Isolated webhook test " + unique,
            CountryCode = "CA", BusinessType = "corporation", RegistrationNumber = "TEST-" + unique,
            ContactEmail = owner.Email, ContactPhone = "+16135550123", KybStatus = status, KybScope = BusinessKybScope.Full,
            AddressLine1 = "1 Test Street", City = "Toronto", PostalCode = "M5V 2T6",
            BlaaizBusinessCustomerId = "test-customer-" + unique };
        var application = new BusinessKybApplication { BusinessProfile = business, Status = status,
            KybScope = BusinessKybScope.Full, ProviderApplicationId = "test-customer-" + unique,
            SubmittedAt = status == KybStatus.UnderReview ? DateTime.UtcNow.AddHours(-1) : null };
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("KorridorX.BusinessKyb.OwnerIdentity.v1");
        application.Owners.Add(new BusinessBeneficialOwner { FirstName = "Test", LastName = "Owner", Email = owner.Email,
            DateOfBirth = new DateTime(1990,1,1,0,0,0,DateTimeKind.Utc), Nationality = "CA", CountryCode = "CA",
            OwnershipPercentage = 100m, IdDocumentType = "passport", IdentityNumberEncrypted = protector.Protect("TEST-IDENTITY"),
            IdDocumentCountry = "CA", IdExpiryDate = DateTime.UtcNow.AddYears(3), IsIdentityFrontUploaded = true,
            IdentityFrontProviderFileId = "test-owner-file", AreIdentityFilesAttachedToProvider = true, ProviderOwnerId = "test-owner-" + unique });
        application.Documents.Add(new BusinessKybDocument { DocumentType = BusinessKybDocumentType.CertificateOfIncorporation,
            Name = "Synthetic test formation document", FileName = "fixture.pdf", MimeType = "application/pdf",
            IsUploaded = true, IsRegisteredWithProvider = true, ProviderFileId = "test-doc-file", ProviderDocumentId = "test-doc-" + unique });
        db.BusinessKybApplications.Add(application);
        db.BusinessUsers.Add(new BusinessUser { BusinessProfile = business, User = member, Role = BusinessUserRole.Member });
        db.ProviderCustomers.Add(new ProviderCustomer { BusinessProfile = business, ProviderCustomerId = application.ProviderApplicationId,
            ProviderStatus = status == KybStatus.Pending ? "PENDING" : "PROCESSING" });
        await db.SaveChangesAsync();
        return new Scenario(application.Id, business.Id, owner.Id, owner.Email, member.Email, application.ProviderApplicationId);
    }

    private WebApplicationFactory<Program> Application(TestProvider provider, FailureControl? failure = null, CaptureDelivery? capture = null) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>(); // No background provider calls or external email.
            services.RemoveAll<IRemittanceProvider>();
            services.AddSingleton((IRemittanceProvider)(object)provider);
            services.PostConfigure<BlaaizOptions>(o =>
            {
                o.IsEnabled = true; o.BaseUrl = "https://provider.invalid"; o.ClientId = "test-only";
                o.ClientSecret = "test-only"; o.WebhookSigningSecret = Secret;
                o.ReconciliationEnabled = false; o.AutomaticPayoutDispatchEnabled = false;
            });
            services.PostConfigure<SecurityOptions>(o => o.Accounts.FrontendBaseUrl = Portal);
            services.PostConfigure<NotificationDeliveryOptions>(o => { o.WorkerEnabled = false; o.Smtp.IsEnabled = false; o.BatchSize = 1; });
            if (failure is not null)
            {
                services.RemoveAll<INotificationQueueService>();
                services.AddScoped<INotificationQueueService>(sp =>
                {
                    var proxy = DispatchProxy.Create<INotificationQueueService, ControlledQueue>();
                    var queue = (ControlledQueue)(object)proxy;
                    queue.Inner = new NotificationQueueService(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IOptions<NotificationDeliveryOptions>>());
                    queue.Control = failure;
                    return proxy;
                });
            }
            services.RemoveAll<INotificationDeliveryProvider>();
            services.AddSingleton<INotificationDeliveryProvider>(capture ?? new CaptureDelivery());
        }));

    private static TestProvider Provider(Scenario s, string status)
    {
        var service = DispatchProxy.Create<IRemittanceProvider, TestProvider>();
        var stub = (TestProvider)(object)service;
        stub.CustomerId = s.CustomerId; stub.Status = status; stub.BusinessId = s.BusinessId;
        return stub;
    }
    private static string Payload(Scenario s, string status, string? eventId = null, DateTime? occurredAt = null, string? comment = null) =>
        JsonSerializer.Serialize(new { message = "Isolated verification test", event_type = "customer.status_changed",
            customer_id = s.CustomerId, old_status = "PROCESSING", new_status = status, comment,
            updated_at = (occurredAt ?? DateTime.UtcNow).ToString("O"), event_id = eventId ?? "test-event-" + Guid.NewGuid().ToString("N"), type = "customer" });
    private static async Task<HttpResponseMessage> Post(HttpClient client, string payload, string mode = "valid")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/blaaiz/collection")
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        if (mode != "missing")
        {
            var timestamp = (mode == "expired" ? DateTimeOffset.UtcNow.AddHours(-1) : DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString();
            // Independently implement the documented wire signature, not the production helper.
            var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(timestamp + "." + payload))).ToLowerInvariant();
            request.Headers.Add("x-blaaiz-timestamp", timestamp);
            request.Headers.Add("x-blaaiz-signature", mode == "bad" ? "00" : signature);
        }
        return await client.SendAsync(request);
    }
    private static async Task<BlaaizWebhookResult> Send(HttpClient client, string payload)
    {
        using var response = await Post(client, payload);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadApiResponseAsync<BlaaizWebhookResult>()).Data!;
    }
    private static AppDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();
    private static Task<List<NotificationMessage>> Messages(AppDbContext db, Scenario s) =>
        db.NotificationMessages.Where(x => x.RelatedEntityType == BusinessKybNotificationService.EntityType && x.RelatedEntityId == s.ApplicationId.ToString()).ToListAsync();
    private static async Task AssertUnchangedAndFailed(WebApplicationFactory<Program> app, Scenario s, string eventId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = Db(scope);
        Assert.Equal(KybStatus.UnderReview, (await db.BusinessKybApplications.FindAsync(s.ApplicationId))!.Status);
        Assert.Equal(KybStatus.UnderReview, (await db.BusinessProfiles.FindAsync(s.BusinessId))!.KybStatus);
        Assert.Equal("PROCESSING", (await db.ProviderCustomers.SingleAsync(x => x.BusinessProfileId == s.BusinessId)).ProviderStatus);
        Assert.Empty(await Messages(db, s));
        Assert.Equal(WebhookProcessingStatus.Failed, (await db.WebhookEvents.SingleAsync(x => x.ProviderEventId == eventId)).ProcessingStatus);
    }
    private static async Task DeliverOne(WebApplicationFactory<Program> app)
    {
        using var worker = new NotificationDeliveryWorker(app.Services.GetRequiredService<IServiceScopeFactory>(),
            app.Services.GetRequiredService<IOptions<NotificationDeliveryOptions>>(), NullLogger<NotificationDeliveryWorker>.Instance);
        var method = typeof(NotificationDeliveryWorker).GetMethod("ProcessBatchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(worker, [CancellationToken.None])!;
    }
    private sealed record Scenario(Guid ApplicationId, Guid BusinessId, Guid OwnerId, string OwnerEmail, string MemberEmail, string CustomerId);
    public sealed class FailureControl { public bool Fail { get; set; } }
    public class ControlledQueue : DispatchProxy
    {
        public INotificationQueueService Inner { get; set; } = null!;
        public FailureControl Control { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Control.Fail && method!.Name == nameof(INotificationQueueService.QueueUserAsync))
                throw new InvalidOperationException("Synthetic queue failure");
            return method!.Invoke(Inner, args);
        }
    }
    public sealed class CaptureDelivery : INotificationDeliveryProvider
    {
        public bool Fail { get; set; }
        public List<Guid> Delivered { get; } = [];
        public Task<NotificationDeliveryResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
        {
            if (Fail) return Task.FromResult(new NotificationDeliveryResult(false, ErrorMessage: "Synthetic delivery failure"));
            Delivered.Add(message.Id);
            return Task.FromResult(new NotificationDeliveryResult(true, ProviderMessageId: "captured-" + message.Id));
        }
    }
    public class TestProvider : DispatchProxy
    {
        public string CustomerId { get; set; } = "";
        public string Status { get; set; } = "";
        public Guid BusinessId { get; set; }
        public int ReadCalls;
        public int SubmitCalls;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "get_ProviderCode": return ProviderCode.Blaaiz;
                case nameof(IRemittanceProvider.GetBusinessCustomerAsync):
                    Assert.Equal(BusinessId, (Guid)args![0]!); Assert.Equal(CustomerId, args[1]);
                    Interlocked.Increment(ref ReadCalls); return Task.FromResult(Result());
                case nameof(IRemittanceProvider.SyncBusinessCustomerAsync):
                    Assert.Equal(BusinessId, ((RemittanceBusinessCustomerRequest)args![0]!).BusinessProfileId);
                    return Task.FromResult(Result());
                case nameof(IRemittanceProvider.SubmitBusinessKybAsync):
                    Assert.Equal(BusinessId, (Guid)args![0]!); Assert.Equal(CustomerId, args[1]);
                    Interlocked.Increment(ref SubmitCalls); Status = "PROCESSING";
                    return Task.FromResult(new RemittanceBusinessKybSubmissionResult(Status, "{}", Guid.NewGuid()));
                default: throw new InvalidOperationException("Unexpected external provider operation in isolated simulation: " + method.Name);
            }
        }
        private RemittanceBusinessCustomerResult Result() => new(CustomerId, Status, "FULL", [], [], "{}", Guid.NewGuid());
    }
}
