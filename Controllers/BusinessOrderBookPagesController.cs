using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Marketplace;
using KorridorX.Dtos.Pagination;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTrading;
using KorridorX.Services.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Controllers;

/// <summary>Complete, aggregated price-level pages; never exposes individual owners or orders.</summary>
[ApiController]
[Authorize]
[Route("api/business/trading/marketplace/pairs/{pairId:guid}/orderbook/paged")]
[EnableRateLimiting(SecurityRateLimitPolicies.Sensitive)]
public sealed class BusinessOrderBookPagesController(AppDbContext db, IBusinessTradingService trading) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid pairId, [FromQuery] WebHistoryPageQuery page,
        [FromQuery, EnumDataType(typeof(TradeOrderSide))] TradeOrderSide side = TradeOrderSide.Buy,
        CancellationToken ct = default)
    {
        Validator.ValidateObject(page, new ValidationContext(page), validateAllProperties: true);
        if (side is not TradeOrderSide.Buy and not TradeOrderSide.Sell)
            return BadRequest("Choose Buy or Sell order-book levels.");
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            throw new UnauthorizedAccessException("Invalid authenticated user.");

        // Reuse the existing business permission, selected-business, country and asset-channel gates.
        // GetMarketplacePairsAsync returns only pairs discoverable in this business context.
        var eligiblePairs = await trading.GetMarketplacePairsAsync(userId, ct);
        if (!eligiblePairs.Any(x => x.Id == pairId))
            throw new InvalidOperationException("Marketplace pair is not available for this business.");

        var query = BusinessOrderBookPageQueries.Levels(db, pairId, side, DateTime.UtcNow);
        var meta = page.ToMeta(await query.CountAsync(ct));
        var rows = await query.Skip((meta.Page - 1) * meta.PageSize).Take(meta.PageSize)
            .Select(x => new OrderBookLevelDto(x.Price, x.Quantity, x.OrderCount)).ToListAsync(ct);
        return Ok(ApiResponses.OkPaged(rows, meta, "Order-book price levels retrieved successfully."));
    }
}
