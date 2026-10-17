using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Models.Enums;
using KorridorX.Services.Pagination;
using KorridorX.Tests.SearchHelp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests;

public sealed class BusinessOrderBookPageTests
{
    [Theory]
    [InlineData(TradeOrderSide.Buy)]
    [InlineData(TradeOrderSide.Sell)]
    public void Complete_depth_is_grouped_and_paged_in_sql(TradeOrderSide side)
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = BusinessOrderBookPageQueries.Levels(db, Guid.NewGuid(), side, DateTime.UtcNow)
            .Skip(120).Take(20).ToQueryString();
        foreach (var fragment in new[] { "GROUP BY", "ORDER BY", "OFFSET", "LIMIT", "RemainingQuantity", "ExpiresAt", "IsDeleted" })
            Assert.Contains(fragment, sql);
        Assert.Contains("sum(", sql.ToLowerInvariant());
        Assert.Contains("count(", sql.ToLowerInvariant());
        if (side == TradeOrderSide.Buy) Assert.Contains("DESC", sql);
        Assert.DoesNotContain("OwnerId", sql);
    }

    [Theory]
    [InlineData(TradeOrderSide.Buy)]
    [InlineData(TradeOrderSide.Sell)]
    public void Source_has_no_100_level_cap(TradeOrderSide side)
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = BusinessOrderBookPageQueries.Levels(db, Guid.NewGuid(), side, DateTime.UtcNow).ToQueryString();
        Assert.DoesNotContain("LIMIT", sql);
        Assert.DoesNotContain("OFFSET", sql);
    }

    [Fact]
    public void Undefined_side_is_rejected_before_database_access()
    {
        using var db = SearchHelpTestScopes.Database();
        Assert.Throws<ArgumentOutOfRangeException>(() => BusinessOrderBookPageQueries.Levels(db, Guid.NewGuid(), (TradeOrderSide)123, DateTime.UtcNow));
    }

    [Fact]
    public void Read_only_route_requires_authentication()
    {
        var type = typeof(BusinessOrderBookPagesController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/business/trading/marketplace/pairs/{pairId:guid}/orderbook/paged", type.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.NotNull(type.GetMethod("Get")!.GetCustomAttribute<HttpGetAttribute>());
    }
}
