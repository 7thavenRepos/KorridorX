using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.PaymentChannels;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.Transfers;
using KorridorX.Services.PaymentChannels;
using KorridorX.Services.Payments;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class PaymentChannelWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    private sealed class Resolver : IProviderWalletResolver
    {
        public string EnvironmentName { get; } = "Channels-" + Guid.NewGuid().ToString("N");
        public string ConnectionKey => new('A', 64);
        public Task<string> SelectAsync(string a, Guid b, string c, string d, string? e, string f, bool g, CancellationToken ct = default) =>
            throw new InvalidOperationException("This test never initiates provider operations.");
    }
    private static async Task<(PaymentChannelService Service, ProviderWalletConfiguration Wallet)> Setup(AppDbContext db, Resolver resolver)
    {
        var wallet = new ProviderWalletConfiguration { ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName,
            ProviderWalletId = "pc-" + Guid.NewGuid().ToString("N"), AssetCode = "CAD", NetworkCode = "", DisplayName = "CAD test wallet",
            IsActive = true, CollectionEnabled = true, DefaultForCollection = true, VerifiedAt = DateTime.UtcNow, VerifiedConnectionKey = resolver.ConnectionKey };
        db.ProviderWalletConfigurations.Add(wallet); await db.SaveChangesAsync();
        return (new PaymentChannelService(db, resolver, Options.Create(new BlaaizOptions { IsEnabled = true, BaseUrl = "https://api.blaaiz.com" }), new CollectionPaymentMethodPolicy()), wallet);
    }
    private static SavePaymentChannelRequest Request(ProviderWalletConfiguration wallet, PaymentChannelStatus status = PaymentChannelStatus.Draft, string country = "CA") =>
        new(null, wallet.Id, "CAD", country, PaymentChannelDirection.Deposit, "interac-request", "Interac test", "Test channel",
            true, true, true, status, 10, 1m, 100m, "Automated acceptance test; no provider request.");

    [DatabaseIntegrationFact] public async Task Draft_preserves_legacy_then_activation_hands_over_and_pause_does_not_restore_legacy()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolver = new Resolver(); var (service, wallet) = await Setup(db, resolver);
        var row = await service.SaveAsync(null, Guid.NewGuid(), Request(wallet), default);
        Assert.False(await service.IsManagedAsync("CAD", PaymentChannelDirection.Deposit));
        Assert.True(Assert.Single(await service.DepositMethodsAsync("CA", "CAD", false, true, 10m, default)).IsLegacy);
        row = await service.SetStatusAsync(row.Id, Guid.NewGuid(), new(row.Revision, PaymentChannelStatus.Active, "activate"), default);
        Assert.Equal(row.Id, Assert.Single(await service.DepositMethodsAsync("CA", "CAD", false, true, 10m, default)).ChannelId);
        await service.SetStatusAsync(row.Id, Guid.NewGuid(), new(row.Revision, PaymentChannelStatus.Paused, "pause"), default);
        Assert.True(await service.IsManagedAsync("CAD", PaymentChannelDirection.Deposit));
        Assert.Empty(await service.DepositMethodsAsync("CA", "CAD", false, true, 10m, default));
    }
    [DatabaseIntegrationFact] public async Task Amount_and_country_filters_apply_before_discovery()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (service, wallet) = await Setup(db, new Resolver());
        await service.SaveAsync(null, Guid.NewGuid(), Request(wallet, PaymentChannelStatus.Active), default);
        Assert.Empty(await service.DepositMethodsAsync("US", "CAD", false, true, 10m, default));
        Assert.Empty(await service.DepositMethodsAsync("CA", "CAD", false, true, 101m, default));
        Assert.Single(await service.DepositMethodsAsync("CA", "CAD", true, true, 100m, default));
    }
    [DatabaseIntegrationFact] public async Task Multiple_channels_same_method_require_specific_identity()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (service, wallet) = await Setup(db, new Resolver());
        var first = await service.SaveAsync(null, Guid.NewGuid(), Request(wallet, PaymentChannelStatus.Active), default);
        await service.SaveAsync(null, Guid.NewGuid(), Request(wallet, PaymentChannelStatus.Active, "*"), default);
        Assert.Equal(2, (await service.DepositMethodsAsync("CA", "CAD", false, true, 10m, default)).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequireDepositAsync(null, "CA", "CAD", false, true, PaymentMethod.Interac, 10m, default));
        Assert.Equal(first.Id, (await service.RequireDepositAsync(first.Id, "CA", "CAD", false, true, PaymentMethod.Interac, 10m, default))!.Channel.Id);
    }
    [DatabaseIntegrationFact] public async Task Stale_edit_is_rejected_and_route_identity_cannot_be_changed()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (service, wallet) = await Setup(db, new Resolver()); var row = await service.SaveAsync(null, Guid.NewGuid(), Request(wallet), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(row.Id, Guid.NewGuid(), Request(wallet) with { Revision = Guid.NewGuid() }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(row.Id, Guid.NewGuid(), Request(wallet) with { Revision = row.Revision, CountryCode = "*" }, default));
    }
    [DatabaseIntegrationFact] public async Task Duplicate_route_is_rejected_without_overwriting_settings()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (service, wallet) = await Setup(db, new Resolver()); await service.SaveAsync(null, Guid.NewGuid(), Request(wallet), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(null, Guid.NewGuid(), Request(wallet), default));
        Assert.Equal(1, await db.PaymentChannels.CountAsync(x => x.ProviderWalletConfigurationId == wallet.Id));
    }
    [DatabaseIntegrationFact] public async Task Suspended_wallet_removes_channel_from_discovery()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (service, wallet) = await Setup(db, new Resolver()); await service.SaveAsync(null, Guid.NewGuid(), Request(wallet, PaymentChannelStatus.Active), default);
        wallet.IsActive = false; await db.SaveChangesAsync();
        Assert.Empty(await service.DepositMethodsAsync("CA", "CAD", false, true, 10m, default));
    }
    [DatabaseIntegrationFact] public async Task Chosen_route_is_persisted_and_pause_blocks_new_initiation_without_retargeting()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (service, wallet) = await Setup(db, new Resolver()); var row = await service.SaveAsync(null, Guid.NewGuid(), Request(wallet, PaymentChannelStatus.Active), default);
        var choice = (await service.RequireDepositAsync(row.Id, "CA", "CAD", false, true, PaymentMethod.Interac, 10m, default))!;
        var id = Guid.NewGuid(); service.Record(choice, "Collection", id, "CA", false, Guid.NewGuid(), null, 10m); await db.SaveChangesAsync();
        await service.SetStatusAsync(row.Id, Guid.NewGuid(), new(row.Revision, PaymentChannelStatus.Paused, "pause"), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ValidateInitiationAsync("Collection", id, default));
        Assert.Equal(wallet.ProviderWalletId, (await db.PaymentChannelSelections.SingleAsync(x => x.OperationId == id)).ProviderWalletId);
    }

    [DatabaseIntegrationFact]
    public Task CAD_to_NGN_uses_the_existing_NGN_bank_payout_wallet() =>
        CheckDestinationWalletRouteAsync("CAD", "NGN", "NG", "bank-payout", PaymentMethod.BankTransfer);

    [DatabaseIntegrationFact]
    public Task NGN_to_CAD_uses_the_existing_CAD_Interac_payout_wallet() =>
        CheckDestinationWalletRouteAsync("NGN", "CAD", "CA", "interac-payout", PaymentMethod.Interac);

    private async Task CheckDestinationWalletRouteAsync(
        string source, string destination, string country, string capability, PaymentMethod method)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolver = new Resolver();
        var (service, wallet) = await Setup(db, resolver);
        wallet.AssetCode = destination;
        wallet.PayoutEnabled = true;
        await db.SaveChangesAsync();
        var request = new SavePaymentChannelRequest(null, wallet.Id, destination, country,
            PaymentChannelDirection.Withdrawal, capability, "Receiving-currency route", "",
            true, true, true, PaymentChannelStatus.Active, 10, 0m, null, "Destination-wallet regression fixture.");
        var route = await service.SaveAsync(null, Guid.NewGuid(), request, default);
        var transfer = new Transfer { SourceCurrencyCode = source, DestinationCurrencyCode = destination,
            DestinationCountryCode = country, DestinationAmount = 8m };
        var choice = await service.ResolvePayoutAsync(transfer, null, default, method);
        Assert.NotNull(choice);
        Assert.Equal(route.Id, choice.Channel.Id);
        Assert.Equal(destination, choice.Wallet.AssetCode);

        var sourceWallet = new ProviderWalletConfiguration {
            ProviderCode = "BLAAIZ", Environment = resolver.EnvironmentName,
            ProviderWalletId = "pc-source-" + Guid.NewGuid().ToString("N"),
            AssetCode = source, NetworkCode = "", DisplayName = "Sending-currency wallet",
            IsActive = true, PayoutEnabled = true, VerifiedAt = DateTime.UtcNow,
            VerifiedConnectionKey = resolver.ConnectionKey };
        db.ProviderWalletConfigurations.Add(sourceWallet);
        await db.SaveChangesAsync();
        var draft = await service.SaveAsync(null, Guid.NewGuid(), request with {
            ProviderWalletConfigurationId = sourceWallet.Id, Status = PaymentChannelStatus.Draft
        }, default);
        Assert.False(draft.Ready);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetStatusAsync(draft.Id,
            Guid.NewGuid(), new(draft.Revision, PaymentChannelStatus.Active, "Invalid currency fixture."), default));
        await service.SetStatusAsync(route.Id, Guid.NewGuid(),
            new(route.Revision, PaymentChannelStatus.Paused, "Pause fixture."), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ResolvePayoutAsync(transfer, null, default, method));
    }

    [DatabaseIntegrationFact] public async Task Paged_admin_list_has_actual_totals()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (service, wallet) = await Setup(db, new Resolver()); await service.SaveAsync(null, Guid.NewGuid(), Request(wallet), default);
        var first = await service.ListAsync(1, 1, "CAD", default); var next = await service.ListAsync(2, 1, "CAD", default);
        Assert.Single(first.Items); Assert.Equal(1, next.Meta.TotalItems); Assert.Empty(next.Items);
    }
}
