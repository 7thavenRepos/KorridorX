using System.Reflection;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Exceptions;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.BusinessContext;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Compliance;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

public sealed class BusinessKybCreationHistoryTests
{
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void Calendar_day_is_preserved_without_timezone_conversion(DateTimeKind kind)
    {
        var value = BusinessKybCalendarDate.Normalize(new DateTime(2023, 11, 1, 23, 59, 0, kind));
        Assert.Equal(new DateTime(2023, 11, 1, 0, 0, 0, DateTimeKind.Utc), value);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
    }
    [Fact]
    public void Browser_date_payload_reproduces_the_missed_unspecified_kind()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2023-11-01\"");
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
        Assert.Equal(DateTimeKind.Utc, BusinessKybCalendarDate.Normalize(value).Kind);
    }
    [Fact]
    public void Recovery_is_bound_to_the_recorded_business()
    {
        var profile = Guid.NewGuid(); var log = Log(profile, 201, "created");
        Assert.Equal("created", BusinessKybCreationHistory.Resolve(profile, [log])!.CustomerId);
        Assert.Null(BusinessKybCreationHistory.Resolve(Guid.NewGuid(), [log]));
    }
    [Theory]
    [InlineData(400)] [InlineData(401)] [InlineData(403)] [InlineData(422)] [InlineData(429)]
    public void Rejected_creation_allows_a_corrected_retry(int status)
    {
        var profile = Guid.NewGuid();
        Assert.Null(BusinessKybCreationHistory.Resolve(profile, [Log(profile, status)]));
    }
    [Theory]
    [InlineData(null)] [InlineData(409)] [InlineData(500)] [InlineData(503)]
    public void Unknown_creation_outcome_prevents_another_create(int? status)
    {
        var profile = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => BusinessKybCreationHistory.Resolve(profile, [Log(profile, status)]));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":{\"id\":\"\"}}")]
    [InlineData("{\"data\":{\"id\":42}}")]
    [InlineData("{\"data\":{\"id\":\"x\",\"type\":\"individual\"}}")]
    [InlineData("<html>invalid</html>")]
    public void Incompatible_success_evidence_requires_review(string body)
    {
        var profile = Guid.NewGuid(); var log = Log(profile, 201); log.ResponseBodyJson = body;
        Assert.Throws<InvalidOperationException>(() => BusinessKybCreationHistory.Resolve(profile, [log]));
    }
    [Fact]
    public void Multiple_different_customers_are_not_silently_selected()
    {
        var profile = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => BusinessKybCreationHistory.Resolve(profile,
            [Log(profile, 201, "one"), Log(profile, 201, "two")]));
    }
    [Fact]
    public void Repeated_evidence_and_later_validation_errors_resolve_one_customer()
    {
        var profile = Guid.NewGuid();
        Assert.Equal("one", BusinessKybCreationHistory.Resolve(profile,
            [Log(profile, 201, "one"), Log(profile, 200, "one"), Log(profile, 422)])!.CustomerId);
    }
    public static ProviderRequestLog Log(Guid profile, int? status, string customer = "created") => new()
    {
        ProviderCode = ProviderCode.Blaaiz, Endpoint = "/api/external/customer", HttpMethod = "POST",
        RequestBodyJson = JsonSerializer.Serialize(new { businessProfileId = profile, Type = "business" }),
        ResponseStatusCode = status, Status = status is >= 200 and <= 299 ? ProviderRequestStatus.Successful : ProviderRequestStatus.Failed,
        ResponseBodyJson = status is >= 200 and <= 299 ? JsonSerializer.Serialize(new { data = new { id = customer, type = "business" } }) : "{}"
    };
}

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessKybStartRecoveryDatabaseTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Browser_date_and_draft_are_saved_before_provider_contact()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope); var user = await User(db); var calls = 0;
        var service = Service(db, async request =>
        {
            calls++;
            await using var check = fixture.Factory.Services.CreateAsyncScope(); var stored = Db(check);
            var profile = await stored.BusinessProfiles.SingleAsync(x => x.Id == request.BusinessProfileId);
            Assert.Equal(DateTimeKind.Utc, profile.IncorporationDate!.Value.Kind);
            Assert.Equal(new DateTime(2023,11,1,0,0,0,DateTimeKind.Utc), profile.IncorporationDate);
            Assert.True(await stored.BusinessKybApplications.AnyAsync(x => x.BusinessProfileId == profile.Id));
            Assert.True(await stored.BusinessUsers.AnyAsync(x => x.BusinessProfileId == profile.Id && x.UserId == user.Id));
            return Result("provider-"+user.Id);
        });
        var result = await service.StartAsync(user.Id, Request());
        Assert.Equal(1, calls); Assert.Equal(KybStatus.Pending, result.Status);
        Assert.True(await db.ProviderCustomers.AnyAsync(x => x.BusinessProfileId == result.BusinessProfileId));
    }
    [DatabaseIntegrationFact]
    public Task Crash_after_201_recovers_the_same_customer_without_recreation() => CheckRecovery(false);

    [DatabaseIntegrationFact]
    public Task Reloaded_form_profile_update_recovers_without_another_create() => CheckRecovery(true);

    private async Task CheckRecovery(bool update)
    {
        Guid userId; var profileId = Guid.Empty; var customer = "created-"+Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = Db(scope); userId = (await User(db)).Id;
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, async request =>
            {
                Assert.Null(request.ExistingProviderCustomerId); profileId = request.BusinessProfileId;
                await Audit(BusinessKybCreationHistoryTests.Log(profileId, 201, customer));
                throw new InvalidOperationException("Simulated crash after provider success");
            }).StartAsync(userId, Request()));
        }
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = Db(scope); var calls = 0;
            var service = Service(db, request =>
            {
                calls++; Assert.Equal(profileId, request.BusinessProfileId); Assert.Equal(customer, request.ExistingProviderCustomerId);
                return Task.FromResult(Result(customer));
            });
            if (update)
            {
                var application = await db.BusinessKybApplications.SingleAsync(x => x.BusinessProfileId == profileId);
                var request = JsonSerializer.Deserialize<UpdateBusinessKybProfileRequestDto>(JsonSerializer.Serialize(Request()))!;
                await service.UpdateProfileAsync(userId, application.Id, request);
            }
            else await service.StartAsync(userId, Request());
            Assert.Equal(1, calls);
            Assert.Equal(1, await db.BusinessProfiles.CountAsync(x => x.OwnerUserId == userId));
            Assert.Equal(1, await db.BusinessKybApplications.CountAsync(x => x.BusinessProfileId == profileId));
            Assert.Equal(1, await db.ProviderCustomers.CountAsync(x => x.BusinessProfileId == profileId));
            Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == profileId.ToString() && x.Action == "RecoverProviderCustomerReference"));
        }
    }
    [DatabaseIntegrationFact]
    public async Task Validation_rejection_keeps_one_editable_draft()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope); var user = await User(db);
        var calls = 0; var profile = Guid.Empty;
        var service = Service(db, async request =>
        {
            if (++calls == 1)
            {
                profile = request.BusinessProfileId; await Audit(BusinessKybCreationHistoryTests.Log(profile, 422));
                throw new ProviderIntegrationException("Postal code invalid", 422);
            }
            Assert.Equal(profile, request.BusinessProfileId); Assert.Null(request.ExistingProviderCustomerId);
            return Result("provider-"+user.Id);
        });
        await Assert.ThrowsAsync<ProviderIntegrationException>(() => service.StartAsync(user.Id, Request()));
        await service.StartAsync(user.Id, Request());
        Assert.Equal(2, calls); Assert.Equal(1, await db.BusinessProfiles.CountAsync(x => x.OwnerUserId == user.Id));
    }
    [DatabaseIntegrationFact]
    public async Task Uncertain_creation_stops_retry_before_provider_contact()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope); var user = await User(db);
        await Assert.ThrowsAsync<TimeoutException>(() => Service(db, async request =>
        {
            await Audit(BusinessKybCreationHistoryTests.Log(request.BusinessProfileId, null)); throw new TimeoutException();
        }).StartAsync(user.Id, Request()));
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, _ =>
        { called = true; return Task.FromResult(Result("wrong")); }).StartAsync(user.Id, Request()));
        Assert.False(called);
    }
    [DatabaseIntegrationFact]
    public async Task Recovery_rejects_a_reference_linked_to_another_business()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope);
        var user = await User(db); var other = await User(db); var customer = "shared-"+Guid.NewGuid();
        await Service(db, _ => Task.FromResult(Result(customer))).StartAsync(other.Id, Request());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, async request =>
        {
            await Audit(BusinessKybCreationHistoryTests.Log(request.BusinessProfileId, 201, customer));
            throw new InvalidOperationException("Interrupted");
        }).StartAsync(user.Id, Request()));
        var called = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, _ =>
        { called = true; return Task.FromResult(Result(customer)); }).StartAsync(user.Id, Request()));
        Assert.False(called);
        Assert.Null((await db.BusinessProfiles.SingleAsync(x => x.OwnerUserId == user.Id)).BlaaizBusinessCustomerId);
    }
    [DatabaseIntegrationFact]
    public async Task Simultaneous_start_sends_only_one_provider_creation()
    {
        await using var first = fixture.Factory.Services.CreateAsyncScope(); var db = Db(first); var user = await User(db);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        var running = Service(db, async _ => { calls++; entered.SetResult(); await finish.Task; return Result("provider-"+user.Id); }).StartAsync(user.Id, Request());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await using var second = fixture.Factory.Services.CreateAsyncScope();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(Db(second), _ =>
            { calls++; return Task.FromResult(Result("duplicate")); }).StartAsync(user.Id, Request()));
            Assert.Contains("already being saved", error.Message);
        }
        finally { finish.TrySetResult(); await running; }
        Assert.Equal(1, calls);
    }
    [DatabaseIntegrationFact]
    public async Task Owner_calendar_dates_are_saved_and_updated_as_utc()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope); var user = await User(db);
        var service = Service(db, _ => Task.FromResult(Result("provider-"+user.Id)));
        var app = await service.StartAsync(user.Id, Request());
        var input = new CreateBusinessOwnerRequestDto("Test", "Owner", "owner@example.test", new DateTime(1990,2,3),
            "CA", "CA", "Director", 100, true, true, true, "passport", "TEST1234", "CA", new DateTime(2035,2,3), false);
        var owner = await service.AddOwnerAsync(user.Id, app.Id, input);
        await service.UpdateOwnerAsync(user.Id, app.Id, owner.Id, new UpdateBusinessOwnerRequestDto(
            input.FirstName, input.LastName, input.Email, new DateTime(1991,3,4), input.Nationality, input.CountryCode,
            input.Title, 100, true, true, true, input.IdDocumentType, input.IdDocumentNumber, input.IdDocumentCountry, new DateTime(2036,3,4), false));
        db.ChangeTracker.Clear(); var stored = await db.BusinessBeneficialOwners.SingleAsync(x => x.Id == owner.Id);
        Assert.Equal(DateTimeKind.Utc, stored.DateOfBirth.Kind); Assert.Equal(DateTimeKind.Utc, stored.IdExpiryDate.Kind);
        Assert.Equal(new DateTime(1991,3,4,0,0,0,DateTimeKind.Utc), stored.DateOfBirth);
    }
    [DatabaseIntegrationFact]
    public async Task New_document_upload_request_inserts_a_pending_record()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope); var user = await User(db);
        var providerCustomerId = "provider-" + user.Id; var calls = 0;
        var service = Service(db, _ => Task.FromResult(Result(providerCustomerId)), documentUpload: request =>
        {
            calls++; Assert.Equal(providerCustomerId, request.ProviderCustomerId);
            return Task.FromResult(new RemittanceBusinessUploadUrlResult(
                "test-file-id", "https://upload.example.test/document", new Dictionary<string, string>(), "{}", Guid.NewGuid()));
        });
        var application = await service.StartAsync(user.Id, Request());
        var upload = await service.RequestDocumentUploadUrlAsync(user.Id, application.Id,
            new BusinessDocumentUploadUrlRequestDto(BusinessKybDocumentType.CertificateOfIncorporation,
                "Company certificate", "certificate.pdf", "application/pdf", null));
        db.ChangeTracker.Clear();
        var stored = await db.BusinessKybDocuments.SingleAsync(x => x.Id == upload.RecordId);
        Assert.Equal(1, calls);
        Assert.Equal(application.Id, stored.BusinessKybApplicationId);
        Assert.Equal(upload.ProviderFileId, stored.ProviderFileId);
        Assert.Equal("PENDING", stored.ProviderStatus);
        Assert.False(stored.IsUploaded);
        Assert.False(stored.IsRegisteredWithProvider);
        Assert.Equal(1, await db.BusinessKybDocuments.CountAsync(x => x.BusinessKybApplicationId == application.Id));
    }
    [DatabaseIntegrationFact]
    public async Task An_unrelated_user_cannot_recover_another_selected_business()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope); var owner = await User(db); var intruder = await User(db);
        var profile = new BusinessProfile { OwnerUserId = owner.Id, BusinessName = "Other business", CountryCode = "CA" };
        db.BusinessProfiles.Add(profile); await db.SaveChangesAsync(); var called = false;
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db, _ =>
        { called = true; return Task.FromResult(Result("wrong")); }, profile.Id).StartAsync(intruder.Id, Request()));
        Assert.False(called);
    }
    private async Task Audit(ProviderRequestLog log)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = Db(scope); db.ProviderRequestLogs.Add(log); await db.SaveChangesAsync();
    }
    private static AppDbContext Db(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();
    private static async Task<ApplicationUser> User(AppDbContext db)
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = Guid.NewGuid().ToString(), CountryCode = "CA", UserType = UserType.Business };
        db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    private static BusinessKybService Service(AppDbContext db, Func<RemittanceBusinessCustomerRequest, Task<RemittanceBusinessCustomerResult>> callback,
        Guid? selected = null, Func<RemittanceBusinessDocumentUploadUrlRequest, Task<RemittanceBusinessUploadUrlResult>>? documentUpload = null)
    {
        var provider = DispatchProxy.Create<IRemittanceProvider, CallbackProvider>(); ((CallbackProvider)(object)provider).Callback = callback;
        ((CallbackProvider)(object)provider).DocumentUpload = documentUpload;
        var context = new Context(selected);
        return new BusinessKybService(db, provider, new EphemeralDataProtectionProvider(), Options.Create(new BlaaizOptions()),
            new NoOpComplianceScreeningService(), new BusinessAccessService(db, context), context);
    }
    private sealed class Context(Guid? selected) : IBusinessContextAccessor { public Guid? GetSelectedBusinessProfileId() => selected; }
    public class CallbackProvider : DispatchProxy
    {
        public Func<RemittanceBusinessCustomerRequest, Task<RemittanceBusinessCustomerResult>> Callback { get; set; } = null!;
        public Func<RemittanceBusinessDocumentUploadUrlRequest, Task<RemittanceBusinessUploadUrlResult>>? DocumentUpload { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_ProviderCode" => ProviderCode.Blaaiz,
            "SyncBusinessCustomerAsync" => Callback((RemittanceBusinessCustomerRequest)args![0]!),
            "RequestBusinessDocumentUploadUrlAsync" => (DocumentUpload ?? throw new NotSupportedException(method.Name))(
                (RemittanceBusinessDocumentUploadUrlRequest)args![0]!),
            _ => throw new NotSupportedException(method.Name)
        };
    }
    private static RemittanceBusinessCustomerResult Result(string id) => new(id, "PENDING", "FULL", [], [], "{}", Guid.NewGuid());
    private static StartBusinessKybRequestDto Request() => JsonSerializer.Deserialize<StartBusinessKybRequestDto>("""
        {"businessName":"Test Company","businessType":"corporation","registrationNumber":"RC-TEST",
         "countryCode":"CA","incorporationDate":"2023-11-01","kybScope":1,
         "contactEmail":"business@example.test","contactPhone":"+16135550123","city":"Ottawa","addressLine1":"1 Test Street"}
        """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}
