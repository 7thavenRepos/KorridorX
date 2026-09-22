using System.Net;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.MasterData;
using KorridorX.Models.Lookups;
using KorridorX.Services.Audit;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessTypeMasterDataTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Anonymous_users_can_read_active_choices_but_cannot_manage_them()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/lookups/business-types")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/master-data/business-types")).StatusCode);
    }

    [DatabaseIntegrationFact]
    public async Task Migration_seeds_all_seven_supported_types()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var code in new[] { "corporation", "government_entity", "llc", "non_profit", "other", "partnership", "sole_proprietorship" })
            Assert.True(await db.BusinessTypes.AnyAsync(x => x.Code == code && x.IsActive));
    }

    [DatabaseIntegrationFact]
    public async Task Administrators_can_add_relabel_order_deactivate_and_reactivate_without_restart()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var controller = new AdminBusinessTypesController(db, scope.ServiceProvider.GetRequiredService<IAuditService>());
        var code = "test_" + Guid.NewGuid().ToString("N");
        var request = new SaveBusinessTypeRequest(code, "Test business type", true, 500);
        await controller.Create(request, default);
        Assert.True(await LookupContains(db, code));
        await controller.Update(code, request with { Name = "Updated label", IsActive = false, SortOrder = 600 }, default);
        Assert.False(await LookupContains(db, code));
        Assert.Equal("Updated label", (await db.BusinessTypes.FindAsync(code))!.Name);
        await db.Database.MigrateAsync(); // Restart/migration must not undo admin changes.
        Assert.False(await LookupContains(db, code));
        await controller.Update(code, request with { Name = "Reactivated", IsActive = true }, default);
        Assert.True(await LookupContains(db, code));
        Assert.Equal(3, await db.AuditLogs.CountAsync(x => x.EntityName == nameof(BusinessType) && x.EntityId == code));
    }

    [DatabaseIntegrationFact]
    public async Task Duplicate_and_changed_codes_are_rejected_without_overwriting_the_record()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var controller = new AdminBusinessTypesController(db, scope.ServiceProvider.GetRequiredService<IAuditService>());
        var request = new SaveBusinessTypeRequest("test_" + Guid.NewGuid().ToString("N"), "Original", true, 900);
        await controller.Create(request, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Create(request with { Name = "Overwrite" }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.Update(request.Code, request with { Code = "renamed" }, default));
        Assert.Equal("Original", (await db.BusinessTypes.FindAsync(request.Code))!.Name);
    }

    private static async Task<bool> LookupContains(AppDbContext db, string code)
    {
        var result = Assert.IsType<OkObjectResult>(await new LookupsController(db).GetBusinessTypes(default));
        return JsonSerializer.Serialize(result.Value).Contains('"' + code + '"');
    }
}
