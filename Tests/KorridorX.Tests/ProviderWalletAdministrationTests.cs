using System.Reflection;
using KorridorX.Configuration;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance.Blaaiz;
using KorridorX.Providers.Remittance.Blaaiz.Models;
using KorridorX.Services.Audit;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ProviderWalletAdministrationTests(ReleaseCandidateDatabaseFixture fixture)
{
    [Fact]
    public void Wallet_administration_is_restricted_to_SuperAdmin()
    {
        var controller = typeof(AdminProviderWalletConfigurationsController);
        Assert.Equal("SuperAdmin", controller.GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.Empty(controller.GetMethods().Where(x => x.GetCustomAttribute<AllowAnonymousAttribute>() is not null));
    }

    [Fact]
    public void Credential_rotation_keeps_wallet_verification_but_account_or_endpoint_change_invalidates_it()
    {
        using var db = Context();
        var original = new BlaaizOptions { ClientId = "account-a", ClientSecret = "old" };
        var key = new ProviderWalletResolver(db, Options.Create(original)).ConnectionKey;
        original.ClientSecret = "rotated";
        Assert.Equal(key, new ProviderWalletResolver(db, Options.Create(original)).ConnectionKey);
        original.ClientId = "account-b";
        Assert.NotEqual(key, new ProviderWalletResolver(db, Options.Create(original)).ConnectionKey);
        original.ClientId = "account-a";
        original.BaseUrl = "https://different.example.test";
        Assert.NotEqual(key, new ProviderWalletResolver(db, Options.Create(original)).ConnectionKey);
    }

    [Theory]
    [InlineData("Unknown", "COLLECTION")]
    [InlineData("Payout", "Unknown")]
    public async Task Invalid_operation_or_purpose_is_rejected_before_database_access(string operation, string purpose)
    {
        await using var db = Context();
        var resolver = new ProviderWalletResolver(db, Options.Create(new BlaaizOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.SelectAsync(operation, Guid.NewGuid(), "BLAAIZ", "USD", null, purpose, false));
    }

    [Theory]
    [InlineData("{\"data\":{\"id\":\"account\",\"wallet_id\":\"different\"}}")]
    [InlineData("{\"data\":[{\"id\":\"account\",\"wallet_id\":\"different\"}]}")]
    [InlineData("{\"data\":[{\"id\":\"a\"},{\"id\":\"b\"}]}")]
    [InlineData("{\"data\":[{\"id\":\"a\",\"wallet_id\":\"selected\"},{\"id\":\"b\",\"wallet_id\":\"selected\"}]}")]
    [InlineData("{\"data\":{\"id\":\"account\",\"wallet_id\":\"selected\",\"business_wallet_id\":\"different\"}}")]
    [InlineData("{\"data\":[]}")]
    public void Bank_account_response_rejects_mismatched_or_ambiguous_wallets(string json)
    {
        var parser = typeof(BlaaizCollectionAccountProvisioner).GetMethod("ParseVirtualAccount", BindingFlags.NonPublic | BindingFlags.Static)!;
        var error = Assert.Throws<TargetInvocationException>(() => parser.Invoke(null, [json, "selected"]));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Theory]
    [InlineData("{\"data\":[{\"id\":\"other\",\"wallet_id\":\"different\"},{\"id\":\"account\",\"wallet_id\":\"selected\"}]}")]
    [InlineData("{\"data\":{\"id\":\"account\"}}")]
    [InlineData("{\"data\":[{\"id\":\"account\"}]}")]
    public void Bank_account_response_accepts_matching_wallet_or_single_wallet_scoped_account(string json)
    {
        var parser = typeof(BlaaizCollectionAccountProvisioner).GetMethod("ParseVirtualAccount", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = parser.Invoke(null, [json, "selected"]);
        Assert.NotNull(result);
        Assert.Equal("account", result.GetType().GetProperty("Id")!.GetValue(result));
    }

    [DatabaseIntegrationFact]
    public async Task Changing_default_preserves_existing_payment_wallet_and_routes_new_payments_to_new_default()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        var first = await ActivateAsync(s, "first");
        var operation = Guid.NewGuid();
        Assert.Equal(first.ProviderWalletId, await SelectAsync(s, operation));
        var second = await ActivateAsync(s, "second");
        Assert.Equal(first.ProviderWalletId, await SelectAsync(s, operation, true));
        Assert.Equal(second.ProviderWalletId, await SelectAsync(s, Guid.NewGuid()));
        Assert.Single(await s.Db.ProviderWalletSelections.Where(x => x.OperationId == operation).ToListAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Suspended_wallet_blocks_old_retries_instead_of_using_new_default()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        var first = await ActivateAsync(s, "first");
        var operation = Guid.NewGuid();
        await SelectAsync(s, operation);
        await ActivateAsync(s, "second");
        first = (await s.Admin.ListAsync(default)).Single(x => x.Id == first.Id);
        await s.Admin.ConfigureAsync(first.Id, s.Actor, Configuration(first, false, false), default);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SelectAsync(s, operation, true));
        Assert.Contains("retries cannot switch wallets", error.Message);
    }

    [DatabaseIntegrationFact]
    public async Task Concurrent_requests_record_one_immutable_selection()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        var wallet = await ActivateAsync(s, "concurrent");
        var id = Guid.NewGuid();
        var other = new ProviderWalletResolver(s.Db, Options.Create(s.Options), new TestHostEnvironment { EnvironmentName = s.Resolver.EnvironmentName });
        var results = await Task.WhenAll(SelectAsync(s, id), other.SelectAsync("Payout", id, "BLAAIZ", s.Asset, null, ProviderWalletResolver.Payout, false));
        Assert.All(results, value => Assert.Equal(wallet.ProviderWalletId, value));
        Assert.Single(await s.Db.ProviderWalletSelections.Where(x => x.OperationId == id).ToListAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Selection_survives_surrounding_business_transaction_rollback()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        var wallet = await ActivateAsync(s, "rollback");
        var id = Guid.NewGuid();
        await using (var tx = await s.Db.Database.BeginTransactionAsync())
        {
            Assert.Equal(wallet.ProviderWalletId, await SelectAsync(s, id));
            await tx.RollbackAsync();
        }
        Assert.Equal(wallet.ProviderWalletId, await SelectAsync(s, id, true));
    }

    [DatabaseIntegrationFact]
    public async Task Legacy_attempt_without_selection_cannot_adopt_current_default()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        await ActivateAsync(s, "legacy");
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => SelectAsync(s, id, true));
        Assert.False(await s.Db.ProviderWalletSelections.AnyAsync(x => x.OperationId == id));
    }

    [DatabaseIntegrationFact]
    public async Task Wallet_cannot_be_activated_before_verification_and_stale_revisions_are_rejected()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        var draft = await s.Admin.RegisterAsync(s.Actor, new("unverified", s.Asset, null, "Draft", "test"), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Admin.ConfigureAsync(draft.Id, s.Actor, Configuration(draft, true, true), default));
        s.Db.ChangeTracker.Clear();
        var current = await s.Admin.ConfigureAsync(draft.Id, s.Actor, Configuration(draft, false, false), default);
        Assert.NotEqual(draft.Revision, current.Revision);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => s.Admin.ConfigureAsync(draft.Id, s.Actor, Configuration(draft, false, false), default));
    }

    [DatabaseIntegrationFact]
    public async Task Provider_verification_rejects_mismatched_asset_and_revokes_disappeared_wallet()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        var wallet = await ActivateAsync(s, "disappeared");
        s.Proxy.Wallets[0].Currency = "OTHER";
        var verified = await s.Admin.VerifyAsync(wallet.Id, s.Actor, new(wallet.Revision, "refresh"), default);
        Assert.False(verified.VerifiedForCurrentConnection);
        Assert.False(verified.IsActive);
        Assert.False(verified.DefaultForPayout);
    }

    [DatabaseIntegrationFact]
    public async Task Environment_and_provider_account_changes_cannot_reuse_old_selection()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        await ActivateAsync(s, "scope");
        var id = Guid.NewGuid();
        await SelectAsync(s, id);
        var changedEnvironment = new ProviderWalletResolver(s.Db, Options.Create(s.Options), new TestHostEnvironment { EnvironmentName = "AnotherEnvironment" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => changedEnvironment.SelectAsync("Payout", id, "BLAAIZ", s.Asset, null, ProviderWalletResolver.Payout, true));
        s.Options.ClientId = "different-account";
        var changedAccount = new ProviderWalletResolver(s.Db, Options.Create(s.Options), new TestHostEnvironment { EnvironmentName = s.Resolver.EnvironmentName });
        await Assert.ThrowsAsync<InvalidOperationException>(() => changedAccount.SelectAsync("Payout", id, "BLAAIZ", s.Asset, null, ProviderWalletResolver.Payout, true));
    }

    [DatabaseIntegrationFact]
    public async Task Explicit_legacy_import_is_idempotent_and_does_not_activate_wallets()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        s.Options.CollectionWalletIds[s.Asset] = "legacy-wallet";
        s.Options.PayoutWalletIds[s.Asset] = "legacy-wallet";
        await s.Admin.ImportLegacyAsync(s.Actor, "import", default);
        await s.Admin.ImportLegacyAsync(s.Actor, "repeat", default);
        var wallet = Assert.Single(await s.Admin.ListAsync(default));
        Assert.False(wallet.IsActive);
        Assert.False(wallet.VerifiedForCurrentConnection);
        await Assert.ThrowsAsync<InvalidOperationException>(() => SelectAsync(s, Guid.NewGuid()));
    }

    [DatabaseIntegrationFact]
    public async Task Database_rejects_a_second_active_default_for_same_route()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var s = await SetupAsync(scope.ServiceProvider);
        await ActivateAsync(s, "unique");
        s.Db.ProviderWalletConfigurations.Add(new ProviderWalletConfiguration
        {
            Environment = s.Resolver.EnvironmentName, AssetCode = s.Asset, ProviderWalletId = "duplicate-default",
            DisplayName = "Duplicate", IsActive = true, DefaultForPayout = true, PayoutEnabled = true
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => s.Db.SaveChangesAsync());
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options);

    private static ConfigureProviderWalletRequest Configuration(ProviderWalletConfigurationDto wallet, bool active, bool defaults) =>
        new(wallet.Revision, wallet.DisplayName, true, true, defaults, defaults, active, "Wallet regression test");

    private static Task<string> SelectAsync(Setup s, Guid id, bool previous = false) =>
        s.Resolver.SelectAsync("Payout", id, "BLAAIZ", s.Asset, null, ProviderWalletResolver.Payout, previous);

    private static async Task<ProviderWalletConfigurationDto> ActivateAsync(Setup s, string name)
    {
        var externalId = name + "-" + Guid.NewGuid().ToString("N");
        s.Proxy.Wallets.Add(new BlaaizWalletData { Id = externalId, Currency = s.Asset, IsActive = true, Amount = 100m });
        var draft = await s.Admin.RegisterAsync(s.Actor, new(externalId, s.Asset, null, name, "register"), default);
        var verified = await s.Admin.VerifyAsync(draft.Id, s.Actor, new(draft.Revision, "verify"), default);
        return await s.Admin.ConfigureAsync(verified.Id, s.Actor, Configuration(verified, true, true), default);
    }

    private static async Task<Setup> SetupAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var unique = Guid.NewGuid().ToString("N");
        var asset = "W" + unique[..12].ToUpperInvariant();
        db.Assets.Add(new Asset { Code = asset, Name = "Wallet test asset", Type = AssetType.Fiat, IsSupported = true, DepositEnabled = true, WithdrawalEnabled = true });
        await db.SaveChangesAsync();
        var options = new BlaaizOptions { IsEnabled = true, ClientId = unique };
        var resolver = new ProviderWalletResolver(db, Options.Create(options), new TestHostEnvironment { EnvironmentName = "WalletTest-" + unique });
        var api = DispatchProxy.Create<IBlaaizApiClient, WalletApiProxy>();
        var proxy = (WalletApiProxy)(object)api;
        var admin = new ProviderWalletAdminService(db, api, Options.Create(options), resolver, services.GetRequiredService<IAuditService>());
        return new(db, admin, resolver, options, asset, proxy, Guid.NewGuid());
    }

    private sealed record Setup(AppDbContext Db, ProviderWalletAdminService Admin, ProviderWalletResolver Resolver,
        BlaaizOptions Options, string Asset, WalletApiProxy Proxy, Guid Actor);

    public class WalletApiProxy : DispatchProxy
    {
        public List<BlaaizWalletData> Wallets { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IBlaaizApiClient.ListWalletsAsync) => Task.FromResult(new BlaaizApiResult<List<BlaaizWalletData>>(Wallets, "{}", Guid.NewGuid())),
            _ => throw new InvalidOperationException("Unexpected external provider operation in wallet tests.")
        };
    }
}
