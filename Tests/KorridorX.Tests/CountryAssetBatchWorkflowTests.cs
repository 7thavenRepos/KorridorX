using System.Security.Claims;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.MasterData;
using KorridorX.Infrastructure;
using KorridorX.Models.Lookups;
using KorridorX.Services.Audit;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class CountryAssetBatchWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    private async Task<(string Country, string[] Assets)> SetupAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var used = (await db.Countries.Select(x => x.Code).ToListAsync()).ToHashSet();
        var code = Enumerable.Range(0,676).Select(x => $"{(char)('A'+x/26)}{(char)('A'+x%26)}").First(x => !used.Contains(x));
        db.Countries.Add(new Country { Code=code, Name=code, IsSupported=true, IsSendCountry=true, IsReceiveCountry=true });
        var assets = Enumerable.Range(0,2).Select(_ => "CB" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()).ToArray();
        db.Assets.AddRange(assets.Select(x => new Asset { Code=x, Name=x, Symbol=x, IsSupported=true, DepositEnabled=true, WithdrawalEnabled=true, TradingEnabled=true, InstantEnabled=true }));
        await db.SaveChangesAsync(); return (code,assets);
    }
    private async Task<BatchCountryAssetResultDto> AddAsync(string country, string[] assets, bool trade = true, string reason = "approved test rollout")
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var controller = new AdminMasterDataController(db, scope.ServiceProvider.GetRequiredService<IAuditService>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "Test")) } };
        var result = Assert.IsType<OkObjectResult>(await controller.AddCountryAssets(country,
            new BatchCountryAssetRequestDto(assets,false,false,false,false,trade,false,reason), CancellationToken.None));
        return Assert.IsType<ApiResponse<BatchCountryAssetResultDto>>(result.Value).Data!;
    }
    [DatabaseIntegrationFact] public async Task Creates_multiple_mappings_with_per_row_audit_and_no_default()
    {
        var s=await SetupAsync(); var result=await AddAsync(s.Country,s.Assets); Assert.Equal(2,result.CreatedCount); Assert.Equal(0,result.ExistingCount);
        await using var scope=fixture.Factory.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.All(result.Mappings, x => { Assert.False(x.IsDefault); Assert.True(x.CanTrade); Assert.Equal(s.Country,x.CountryCode); });
        foreach(var row in result.Mappings) Assert.True(await db.AuditLogs.AnyAsync(x=>x.EntityId==row.Id.ToString() && x.Action=="COUNTRY_ASSET_MAPPING_CREATED"));
    }
    [DatabaseIntegrationFact] public async Task Replay_is_idempotent_and_does_not_duplicate_creation_audit()
    {
        var s=await SetupAsync(); var first=await AddAsync(s.Country,s.Assets); var next=await AddAsync(s.Country,s.Assets);
        Assert.Equal(0,next.CreatedCount); Assert.Equal(2,next.ExistingCount); Assert.Equal(first.Mappings.Select(x=>x.Id),next.Mappings.Select(x=>x.Id));
        await using var scope=fixture.Factory.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids=first.Mappings.Select(x=>x.Id.ToString()).ToArray(); Assert.Equal(2,await db.AuditLogs.CountAsync(x=>ids.Contains(x.EntityId!) && x.Action=="COUNTRY_ASSET_MAPPING_CREATED"));
    }
    [DatabaseIntegrationFact] public async Task Mixed_batch_preserves_existing_default_and_disabled_capabilities()
    {
        var s=await SetupAsync();
        await using(var scope=fixture.Factory.Services.CreateAsyncScope()) { var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CountryAssets.Add(new CountryAsset { CountryCode=s.Country,AssetCode=s.Assets[0],IsDefault=true,CanTrade=false,CanReceive=true }); await db.SaveChangesAsync(); }
        var result=await AddAsync(s.Country,s.Assets); Assert.Equal(1,result.CreatedCount); Assert.Equal(1,result.ExistingCount);
        var existing=Assert.Single(result.Mappings.Where(x=>x.AssetCode==s.Assets[0])); Assert.True(existing.IsDefault); Assert.False(existing.CanTrade); Assert.True(existing.CanReceive);
        Assert.False(result.Mappings.Single(x=>x.AssetCode==s.Assets[1]).IsDefault);
    }
    [DatabaseIntegrationFact] public async Task Invalid_capability_rejects_all_new_mappings()
    {
        var s=await SetupAsync(); await using(var scope=fixture.Factory.Services.CreateAsyncScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>(); (await db.Assets.SingleAsync(x=>x.Code==s.Assets[1])).TradingEnabled=false; await db.SaveChangesAsync(); }
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>AddAsync(s.Country,s.Assets)); Assert.Contains(s.Assets[1],error.Message);
        await using var check=fixture.Factory.Services.CreateAsyncScope(); Assert.False(await check.ServiceProvider.GetRequiredService<AppDbContext>().CountryAssets.AnyAsync(x=>x.CountryCode==s.Country));
    }
    [DatabaseIntegrationFact] public async Task Unknown_asset_rejects_batch_without_partial_creation()
    {
        var s=await SetupAsync(); await Assert.ThrowsAsync<KeyNotFoundException>(()=>AddAsync(s.Country,[s.Assets[0],"MISSING"+Guid.NewGuid().ToString("N")[..10]]));
        await using var scope=fixture.Factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().CountryAssets.AnyAsync(x=>x.CountryCode==s.Country));
    }
    [DatabaseIntegrationFact] public async Task Concurrent_overlapping_batches_create_each_country_asset_once()
    {
        var s=await SetupAsync(); var results=await Task.WhenAll(AddAsync(s.Country,s.Assets),AddAsync(s.Country,s.Assets.Reverse().ToArray()));
        Assert.Equal(2,results.Sum(x=>x.CreatedCount)); Assert.Equal(2,results.Sum(x=>x.ExistingCount));
        await using var scope=fixture.Factory.Services.CreateAsyncScope(); Assert.Equal(2,await scope.ServiceProvider.GetRequiredService<AppDbContext>().CountryAssets.CountAsync(x=>x.CountryCode==s.Country));
    }
    [DatabaseIntegrationFact] public async Task Blank_reason_is_rejected_without_creating_mappings()
    {
        var s=await SetupAsync(); await Assert.ThrowsAsync<ArgumentException>(()=>AddAsync(s.Country,s.Assets,reason:"   "));
        await using var scope=fixture.Factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().CountryAssets.AnyAsync(x=>x.CountryCode==s.Country));
    }
}
