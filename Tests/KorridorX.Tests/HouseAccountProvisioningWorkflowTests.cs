using KorridorX.Data;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.Lookups;
using KorridorX.Services.Instant;
using KorridorX.Services.Treasury;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class HouseAccountProvisioningWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    private async Task<string[]> AssetsAsync(int count = 1)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var codes = Enumerable.Range(0, count).Select(_ => "HA" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()).ToArray();
        db.Assets.AddRange(codes.Select(code => new Asset { Code = code, Name = code, Symbol = code, IsSupported = true, InstantEnabled = true }));
        await db.SaveChangesAsync(); return codes;
    }
    private async Task<ProvisionHouseAccountsResultDto> ProvisionAsync(string[] codes)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HouseAccountProvisioningService>()
            .ProvisionAsync(Guid.NewGuid(), new() { AssetCodes = codes });
    }
    [DatabaseIntegrationFact] public async Task Bulk_provisioning_creates_zero_balances_with_atomic_audit_and_instant_visibility()
    {
        var codes = await AssetsAsync(2); var result = await ProvisionAsync(codes);
        Assert.Equal(2, result.CreatedCount); Assert.Equal(0, result.ExistingCount);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var account in result.Accounts)
        {
            Assert.Equal(0m, account.AvailableBalance); Assert.Equal(0m, account.SettledBalance); Assert.Equal(0m, account.HeldBalance);
            Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "HouseAccountProvisioned" && x.EntityId == account.Id.ToString()));
            var options = await scope.ServiceProvider.GetRequiredService<IInstantTradingService>().GetHouseAccountsAsync(account.AssetCode);
            Assert.Contains(options, x => x.Id == account.Id);
        }
    }
    [DatabaseIntegrationFact] public async Task Retry_reuses_accounts_and_does_not_duplicate_creation_audits()
    {
        var codes = await AssetsAsync(); var first = await ProvisionAsync(codes); var second = await ProvisionAsync(codes);
        Assert.Equal(0, second.CreatedCount); Assert.Equal(1, second.ExistingCount); Assert.Equal(first.Accounts[0].Id, second.Accounts[0].Id);
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = first.Accounts[0].Id.ToString(); Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "HouseAccountProvisioned"));
    }
    [DatabaseIntegrationFact] public async Task Concurrent_requests_create_one_account_only()
    {
        var codes = await AssetsAsync(); var results = await Task.WhenAll(ProvisionAsync(codes), ProvisionAsync(codes));
        Assert.Equal(1, results.Sum(x => x.CreatedCount)); Assert.Equal(results[0].Accounts[0].Id, results[1].Accounts[0].Id);
    }
    [DatabaseIntegrationFact] public async Task Invalid_asset_rejects_entire_batch()
    {
        var codes = await AssetsAsync(); await Assert.ThrowsAsync<InvalidOperationException>(() => ProvisionAsync([codes[0], "MISSING" + Guid.NewGuid().ToString("N")[..10]]));
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.FinancialAccounts.AnyAsync(x => x.AssetCode == codes[0]));
    }
    [DatabaseIntegrationFact] public async Task Existing_nonzero_frozen_account_is_never_reactivated_or_zeroed()
    {
        var codes = await AssetsAsync(); var first = await ProvisionAsync(codes);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.FinancialAccounts.SingleAsync(x => x.Id == first.Accounts[0].Id);
            row.Status = FinancialAccountStatus.Frozen; row.SettledBalance = 15m; row.AvailableBalance = 12m; row.HeldBalance = 3m; await db.SaveChangesAsync();
        }
        var second = await ProvisionAsync(codes); Assert.Equal(0, second.CreatedCount);
        Assert.Equal(FinancialAccountStatus.Frozen, second.Accounts[0].Status); Assert.Equal(15m, second.Accounts[0].SettledBalance);
        Assert.Equal(12m, second.Accounts[0].AvailableBalance); Assert.Equal(3m, second.Accounts[0].HeldBalance);
    }
    [DatabaseIntegrationFact] public async Task Deleted_canonical_identity_is_not_resurrected()
    {
        var codes = await AssetsAsync(); var first = await ProvisionAsync(codes);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.FinancialAccounts.SingleAsync(x => x.Id == first.Accounts[0].Id);
            row.IsDeleted = true; await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProvisionAsync(codes));
    }
    [DatabaseIntegrationFact] public async Task House_list_returns_filtered_page_and_total()
    {
        var codes = await AssetsAsync(); var created = await ProvisionAsync(codes);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<HouseAccountProvisioningService>();
        var page = await service.GetAsync(codes[0], 1, 10); Assert.Equal(1, page.Meta.TotalItems); Assert.Equal(created.Accounts[0].Id, Assert.Single(page.Items).Id);
        var empty = await service.GetAsync(codes[0], 2, 10); Assert.Empty(empty.Items); Assert.Equal(1, empty.Meta.TotalItems);
    }
}
