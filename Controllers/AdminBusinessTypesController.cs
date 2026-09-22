using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.MasterData;
using KorridorX.Infrastructure;
using KorridorX.Models.Lookups;
using KorridorX.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

[ApiController, Route("api/admin/master-data/business-types")]
[Authorize(Roles = "Admin,SuperAdmin")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class AdminBusinessTypesController(AppDbContext db, IAuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(ApiResponses.Ok(
        await db.BusinessTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new BusinessTypeDto(x.Code, x.Name, x.IsActive, x.SortOrder)).ToListAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create(SaveBusinessTypeRequest request, CancellationToken ct)
    {
        if (await db.BusinessTypes.AnyAsync(x => x.Code == request.Code, ct))
            throw new InvalidOperationException("This business type code already exists.");
        var item = new BusinessType { Code = request.Code };
        db.BusinessTypes.Add(item);
        return await Save(item, request, null, ct);
    }

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, SaveBusinessTypeRequest request, CancellationToken ct)
    {
        if (code != request.Code)
            throw new InvalidOperationException("A business type code cannot be changed. Add another type instead.");
        var item = await db.BusinessTypes.SingleOrDefaultAsync(x => x.Code == code, ct);
        if (item is null) return NotFound(ApiResponses.Fail("Business type not found.", "NOT_FOUND"));
        return await Save(item, request, new BusinessTypeDto(item.Code, item.Name, item.IsActive, item.SortOrder), ct);
    }

    private async Task<IActionResult> Save(BusinessType item, SaveBusinessTypeRequest request, object? before, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Business type name is required.");
        item.Name = request.Name.Trim();
        item.IsActive = request.IsActive;
        item.SortOrder = request.SortOrder;
        var after = new BusinessTypeDto(item.Code, item.Name, item.IsActive, item.SortOrder);
        audit.Stage(new AuditRecordRequest(
            Action: before is null ? "BUSINESS_TYPE_CREATED" : "BUSINESS_TYPE_UPDATED",
            Category: "MasterData", EntityName: nameof(BusinessType), EntityId: item.Code,
            OldValues: before, NewValues: after));
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponses.Ok(after, "Business type saved."));
    }
}
