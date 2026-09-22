using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Exceptions;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Providers;
using KorridorX.Services.BusinessContext;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessCollectionAccountPortalTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Anonymous_browser_cannot_read_customer_accounts()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, (await client.GetAsync("/api/business/embedded/customers")).StatusCode);
    }

    [DatabaseIntegrationFact]
    public async Task Owners_are_scoped_to_the_selected_business_and_cannot_select_another_owners_business()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var one = await Setup(db); var two = await Setup(db);
        var service = Service(db, scope.ServiceProvider, one);
        var rows = await service.GetCustomersAsync(one.Owner.Id, 1, 20, one.Customer.ExternalReference);
        Assert.Equal(one.Customer.Id, Assert.Single(rows.Items).Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAccountsAsync(one.Owner.Id, two.Customer.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service(db, scope.ServiceProvider, two).GetCustomersAsync(one.Owner.Id, 1, 20, null));
    }

    [DatabaseIntegrationFact]
    public async Task Read_only_members_can_view_but_cannot_create_or_provision()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var reader = new ApplicationUser { UserName = Guid.NewGuid().ToString(), CountryCode = "CA" };
        db.BusinessUsers.Add(new() { BusinessProfileId = s.Business.Id, User = reader, Role = BusinessUserRole.Finance,
            Permissions = BusinessPermission.ViewEmbeddedFinance, IsActive = true });
        await db.SaveChangesAsync();
        var service = Service(db, scope.ServiceProvider, s);
        Assert.False((await service.GetAccountsAsync(reader.Id, s.Customer.Id)).CanManage);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateCustomerAsync(reader.Id, Request()));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAccountAsync(reader.Id, s.Customer.Id, new("read-only", "EUR")));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.ProvisionAsync(reader.Id, s.Customer.Id, Guid.NewGuid()));
    }

    [DatabaseIntegrationFact]
    public async Task Unapproved_business_can_view_requirements_but_cannot_change_customers()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); s.Business.KybStatus = KybStatus.Pending; db.BusinessProfiles.Update(s.Business); await db.SaveChangesAsync();
        var service = Service(db, scope.ServiceProvider, s);
        Assert.False((await service.GetAccountsAsync(s.Owner.Id, s.Customer.Id)).BusinessVerified);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateCustomerAsync(s.Owner.Id, Request()));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, new("pending-kyb", "EUR")));
    }

    [DatabaseIntegrationFact]
    public async Task Customer_retry_reuses_reference_and_does_not_duplicate_the_outbox_event()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var service = Service(db, scope.ServiceProvider, s); var request = Request();
        var first = await service.CreateCustomerAsync(s.Owner.Id, request); var second = await service.CreateCustomerAsync(s.Owner.Id, request);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x => x.BusinessProfileId == s.Business.Id && x.EventType == "customer.created"));
        Assert.Equal(s.Owner.Id, (await db.BusinessCustomers.FindAsync(first.Id))!.CreatedByUserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCustomerAsync(s.Owner.Id, request with { DisplayName = "Different person" }));
    }

    [DatabaseIntegrationFact]
    public async Task Unsupported_country_and_currency_are_rejected_without_creating_financial_records()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var service = Service(db, scope.ServiceProvider, s);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCustomerAsync(s.Owner.Id, Request() with { CountryCode = "ZZ" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, new("usd", "USD")));
        Assert.False(await db.FinancialAccounts.AnyAsync(x => x.OwnerId == s.Customer.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Adding_account_is_idempotent_pending_and_does_not_contact_provider_or_change_money()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var provider = new Provider(); var service = Service(db, scope.ServiceProvider, s, provider);
        var request = new CreateCollectionAccountRequestDto("eur-" + Guid.NewGuid(), "EUR");
        var first = Assert.Single((await service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, request)).Accounts);
        var second = Assert.Single((await service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, request)).Accounts);
        Assert.Equal(first.Id, second.Id); Assert.Equal(CollectionAccountStatus.Pending, second.Status);
        Assert.Null(second.BankDetails); Assert.Equal(0m, second.AvailableBalance); Assert.Equal(0, provider.Calls);
        Assert.Equal(1, await db.FinancialAccounts.CountAsync(x => x.OwnerId == s.Customer.Id));
        Assert.Equal(1, await db.BusinessWebhookEvents.CountAsync(x => x.BusinessProfileId == s.Business.Id && x.EventType == "account.created"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, request with { AssetCode = "GBP" }));
    }

    [DatabaseIntegrationFact]
    public async Task Another_customer_cannot_use_an_existing_account_reference_or_account_id()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var service = Service(db, scope.ServiceProvider, s); var reference = Guid.NewGuid().ToString();
        var account = Assert.Single((await service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, new(reference, "EUR"))).Accounts);
        var other = await service.CreateCustomerAsync(s.Owner.Id, Request());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAccountAsync(s.Owner.Id, other.Id, new(reference, "EUR")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(s.Owner.Id, other.Id, account.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Missing_provider_verification_or_wallet_blocks_dispatch()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var provider = new Provider(); var service = Service(db, scope.ServiceProvider, s, provider);
        var account = Assert.Single((await service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, new(Guid.NewGuid().ToString(), "EUR"))).Accounts);
        Assert.False(account.CanRequestBankDetails); Assert.Contains(account.Requirements, x => x.Contains("onboarding"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(s.Owner.Id, s.Customer.Id, account.Id));
        await EnableProvider(db, s, account.Id);
        var wallet = await db.ProviderWalletConfigurations.SingleAsync(x => x.Environment == s.Environment);
        wallet.VerifiedConnectionKey = "old-connection"; await db.SaveChangesAsync();
        var blocked = Assert.Single((await service.GetAccountsAsync(s.Owner.Id, s.Customer.Id)).Accounts);
        Assert.False(blocked.CanRequestBankDetails);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(s.Owner.Id, s.Customer.Id, account.Id));
        Assert.Equal(0, provider.Calls);
    }

    [DatabaseIntegrationFact]
    public async Task Provision_uses_selected_business_and_pending_refresh_does_not_resubmit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var provider = new Provider(); var service = Service(db, scope.ServiceProvider, s, provider);
        var account = Assert.Single((await service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, new(Guid.NewGuid().ToString(), "EUR"))).Accounts);
        await EnableProvider(db, s, account.Id);
        Assert.True(Assert.Single((await service.GetAccountsAsync(s.Owner.Id, s.Customer.Id)).Accounts).CanRequestBankDetails);
        var pending = Assert.Single((await service.ProvisionAsync(s.Owner.Id, s.Customer.Id, account.Id)).Accounts);
        Assert.Equal(ProviderAccountMappingStatus.Pending, pending.ProviderStatus); Assert.False(pending.CanRequestBankDetails);
        Assert.False(pending.IsReady); Assert.Null(pending.BankDetails); Assert.Equal(1, provider.Calls);
        Assert.Equal(s.Business.Id, provider.Request!.BusinessProfileId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(s.Owner.Id, s.Customer.Id, account.Id));
        Assert.Equal(1, provider.Calls);
    }

    [DatabaseIntegrationFact]
    public async Task Ready_details_are_visible_but_hidden_again_for_suspended_or_unverified_routes()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var service = Service(db, scope.ServiceProvider, s);
        var account = Assert.Single((await service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, new(Guid.NewGuid().ToString(), "EUR"))).Accounts);
        await EnableProvider(db, s, account.Id);
        var entity = await db.CollectionAccounts.FindAsync(account.Id); entity!.Status = CollectionAccountStatus.Active;
        db.ProviderAccountMappings.Add(new() { CollectionAccountId = account.Id, ProviderCode = "Blaaiz", Status = ProviderAccountMappingStatus.Active,
            ProviderAccountId = Guid.NewGuid().ToString(), AccountNumber = "12345678", AccountName = "Customer", BankName = "Example",
            MetadataJson = "{\"Iban\":\"DE89370400440532013000\",\"SortCode\":\"123456\",\"secret\":\"never-return-raw-metadata\"}" });
        await db.SaveChangesAsync();
        var ready = Assert.Single((await service.GetAccountsAsync(s.Owner.Id, s.Customer.Id)).Accounts);
        Assert.True(ready.IsReady); Assert.Equal("12345678", ready.AccountNumber); Assert.NotNull(ready.BankDetails?.Iban);
        Assert.DoesNotContain("never-return-raw-metadata", System.Text.Json.JsonSerializer.Serialize(ready));
        entity.Status = CollectionAccountStatus.Suspended; await db.SaveChangesAsync();
        var suspended = Assert.Single((await service.GetAccountsAsync(s.Owner.Id, s.Customer.Id)).Accounts);
        Assert.False(suspended.IsReady); Assert.Null(suspended.BankDetails); Assert.Null(suspended.AccountNumber);
        entity.Status = CollectionAccountStatus.Active;
        var wallet = await db.ProviderWalletConfigurations.SingleAsync(x => x.Environment == s.Environment); wallet.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False(Assert.Single((await service.GetAccountsAsync(s.Owner.Id, s.Customer.Id)).Accounts).IsReady);
    }

    [DatabaseIntegrationFact]
    public async Task Deleted_customer_and_disabled_provider_prevent_account_requests()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await Setup(db); var service = Service(db, scope.ServiceProvider, s, enabled: false);
        var account = Assert.Single((await service.CreateAccountAsync(s.Owner.Id, s.Customer.Id, new(Guid.NewGuid().ToString(), "EUR"))).Accounts);
        await EnableProvider(db, s, account.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProvisionAsync(s.Owner.Id, s.Customer.Id, account.Id));
        s.Customer.IsDeleted = true; db.BusinessCustomers.Update(s.Customer); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAccountsAsync(s.Owner.Id, s.Customer.Id));
    }

    private BusinessCollectionAccountService Service(AppDbContext db, IServiceProvider services, Scenario s, Provider? provider = null, bool enabled = true)
    {
        provider ??= new Provider();
        return new(db, new BusinessAccessService(db, new Selected(s.Business.Id)),
            new CollectionAccountProvisioningService(db, new NoApiKey(), [provider]), [provider], new Wallets(s.Environment),
            services.GetRequiredService<IEmbeddedWebhookOutboxStager>(), Options.Create(new BlaaizOptions { IsEnabled = enabled }));
    }
    private static CreatePortalCustomerRequestDto Request() => new(Guid.NewGuid().ToString(), "New customer", "customer@example.test", null, "CA");
    private static async Task<Scenario> Setup(AppDbContext db)
    {
        var id = Guid.NewGuid().ToString("N");
        var owner = new ApplicationUser { UserName = "portal-" + id, CountryCode = "CA" };
        var business = new BusinessProfile { OwnerUser = owner, BusinessName = "Portal " + id, CountryCode = "CA", KybStatus = KybStatus.Approved };
        var customer = new BusinessCustomer { BusinessProfile = business, DisplayName = "Customer " + id, ExternalReference = id, CountryCode = "CA" };
        db.BusinessCustomers.Add(customer); await db.SaveChangesAsync();
        return new(owner, business, customer, "Portal-" + id);
    }
    private static async Task EnableProvider(AppDbContext db, Scenario s, Guid accountId)
    {
        var wallet = new ProviderWalletConfiguration { Environment = s.Environment, AssetCode = "EUR", ProviderWalletId = Guid.NewGuid().ToString(),
            DisplayName = "EUR test wallet", IsActive = true, DefaultForCollection = true, CollectionEnabled = true,
            VerifiedAt = DateTime.UtcNow, VerifiedConnectionKey = "portal-tests" };
        db.ProviderWalletSelections.Add(new() { OperationType = "CollectionAccount", OperationId = accountId,
            WalletConfiguration = wallet, ProviderWalletId = wallet.ProviderWalletId, Purpose = "COLLECTION" });
        db.ProviderCustomers.Add(new() { BusinessCustomerId = s.Customer.Id, ProviderCustomerId = Guid.NewGuid().ToString(), ProviderStatus = "VERIFIED" });
        await db.SaveChangesAsync();
    }
    private sealed record Scenario(ApplicationUser Owner, BusinessProfile Business, BusinessCustomer Customer, string Environment);
    private sealed class Selected(Guid id) : IBusinessContextAccessor { public Guid? GetSelectedBusinessProfileId() => id; }
    private sealed class NoApiKey : IEmbeddedFinanceContextAccessor
    { public EmbeddedFinancePrincipal GetRequiredPrincipal() => throw new InvalidOperationException("Portal routes must not impersonate an API credential."); }
    private sealed class Wallets(string environment) : IProviderWalletResolver
    {
        public string EnvironmentName => environment;
        public string ConnectionKey => "portal-tests";
        public Task<string> SelectAsync(string operationType, Guid operationId, string providerCode, string assetCode, string? networkCode,
            string purpose, bool previouslySubmitted, CancellationToken ct = default) => throw new InvalidOperationException("No external provider call in portal fixture.");
    }
    private sealed class Provider : ICollectionAccountProvisioner
    {
        public int Calls { get; private set; }
        public CollectionAccountProvisioningRequest? Request { get; private set; }
        public string ProviderCode => "Blaaiz";
        public bool Supports(string countryCode, string assetCode) => assetCode is "CAD" or "EUR" or "GBP";
        public Task<CollectionAccountProvisioningResult> ProvisionAsync(CollectionAccountProvisioningRequest request, CancellationToken ct = default)
        { Calls++; Request = request; return Task.FromResult(new CollectionAccountProvisioningResult("provider-customer", Guid.NewGuid().ToString(), null, null, null, null, null, ProviderAccountMappingStatus.Pending)); }
    }
}
