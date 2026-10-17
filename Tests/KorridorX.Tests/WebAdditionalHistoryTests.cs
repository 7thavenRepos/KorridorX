using System.ComponentModel.DataAnnotations;
using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Dtos.BusinessContext;
using KorridorX.Dtos.Pagination;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Pagination;
using KorridorX.Tests.SearchHelp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests;

public sealed class WebAdditionalHistoryTests
{
    [Fact]
    public void Deposit_intents_are_business_scoped_and_include_completed_as_well_as_pending_records()
    {
        using var db = SearchHelpTestScopes.Database();
        var query = WebAdditionalHistoryQueries.DepositIntents(db, SearchHelpTestScopes.BusinessId);
        var sql = query.Skip(500).Take(20).ToQueryString();
        Assert.Contains("BusinessProfileId", sql);
        Assert.Contains("BusinessCustomerId", sql);
        Assert.Contains("IS NULL", sql);
        Assert.Contains("IsDeleted", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("LIMIT", sql);
        // This is the complete intent history, not the pending-only unified-activity source.
        Assert.DoesNotContain("CollectionId\" IS NULL", sql);
        Assert.DoesNotContain("RawPayloadJson", sql);
    }

    [Fact]
    public void Withdrawal_page_translates_without_per_record_database_fetches()
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = WebAdditionalHistoryQueries.Withdrawals(db, SearchHelpTestScopes.BusinessId)
            .Skip(500).Take(20).ToQueryString();
        foreach (var column in new[] { "BusinessProfileId", "BusinessCustomerId", "IsDeleted", "TransactionHash", "Confirmations", "RequiredConfirmations" })
            Assert.Contains(column, sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("COALESCE", sql);
        Assert.DoesNotContain("RawPayloadJson", sql);
    }

    [Fact]
    public void Settlement_page_preserves_failure_and_stale_pending_filters()
    {
        using var db = SearchHelpTestScopes.Database();
        var cutoff = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var sql = WebAdditionalHistoryQueries.SettlementExceptions(db, cutoff).Skip(1500).Take(20).ToQueryString();
        Assert.Contains("InstantTrades", sql);
        Assert.Contains("Status", sql);
        Assert.Contains("CreatedAt", sql);
        Assert.Contains("IsDeleted", sql);
        Assert.Contains("<=", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("1500", sql);
    }

    [Theory]
    [InlineData("intents")]
    [InlineData("withdrawals")]
    [InlineData("settlements")]
    public void Total_count_queries_translate_without_a_history_window(string kind)
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = kind switch
        {
            "intents" => WebAdditionalHistoryQueries.DepositIntents(db, SearchHelpTestScopes.BusinessId)
                .GroupBy(x => 1).Select(x => x.Count()).ToQueryString(),
            "withdrawals" => WebAdditionalHistoryQueries.Withdrawals(db, SearchHelpTestScopes.BusinessId)
                .GroupBy(x => 1).Select(x => x.Count()).ToQueryString(),
            _ => WebAdditionalHistoryQueries.SettlementExceptions(db, DateTime.UtcNow.AddMinutes(-5))
                .GroupBy(x => 1).Select(x => x.Count()).ToQueryString()
        };
        Assert.Contains("count(", sql.ToLowerInvariant());
        Assert.DoesNotContain("OFFSET", sql);
        // Any LIMIT 1 in the withdrawal row projector is a latest-confirmation scalar lookup,
        // never a limit on the full business-history count.
        Assert.DoesNotContain("LIMIT 100", sql);
        Assert.DoesNotContain("LIMIT 1000", sql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task New_business_histories_require_view_permission_before_database_access(bool withdrawals)
    {
        using var db = SearchHelpTestScopes.Database();
        var access = new DeniedAccess();
        var service = new WebHistoryPaginationService(db, access);
        if (withdrawals)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetWithdrawalsAsync(Guid.NewGuid(), new()));
        else
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDepositIntentsAsync(Guid.NewGuid(), new()));
        Assert.Equal(BusinessPermission.ViewDigitalAssets, access.Permission);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task Invalid_admin_paging_is_rejected_without_connecting(int page, int size)
    {
        using var db = SearchHelpTestScopes.Database();
        var service = new WebHistoryPaginationService(db, new DeniedAccess());
        await Assert.ThrowsAsync<ValidationException>(() => service.GetSettlementExceptionsAsync(new() { Page = page, PageSize = size }));
    }

    [Fact]
    public void Settlement_route_retains_the_exact_existing_admin_role_boundary()
    {
        var action = typeof(WebHistoryPaginationController).GetMethod(nameof(WebHistoryPaginationController.SettlementExceptions))!;
        Assert.Equal("Admin,SuperAdmin,Operations", Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(nameof(WebHistoryPaginationController.DepositIntents), "business/digital-assets/deposit-intents/paged")]
    [InlineData(nameof(WebHistoryPaginationController.Withdrawals), "business/digital-assets/withdrawals/paged")]
    [InlineData(nameof(WebHistoryPaginationController.SettlementExceptions), "admin/instant/settlement-exceptions/paged")]
    public void Additive_history_routes_are_get_only(string method, string route)
    {
        var action = typeof(WebHistoryPaginationController).GetMethod(method)!;
        Assert.Equal(route, Assert.Single(action.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Empty(action.GetCustomAttributes<HttpPostAttribute>());
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    private sealed class DeniedAccess : IBusinessAccessService
    {
        public BusinessPermission? Permission { get; private set; }
        public Task<BusinessAccessContext> EnsurePermissionAsync(Guid userId, BusinessPermission permission, CancellationToken ct = default)
        {
            Permission = permission;
            return Task.FromException<BusinessAccessContext>(new UnauthorizedAccessException("Denied"));
        }
        public Task<BusinessAccessContext> GetAccessAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AvailableBusinessContextDto>> GetAvailableBusinessesAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
