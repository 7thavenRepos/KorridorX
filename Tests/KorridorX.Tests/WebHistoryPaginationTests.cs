using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.BusinessContext;
using KorridorX.Dtos.Pagination;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using KorridorX.Services.Pagination;
using KorridorX.Tests.SearchHelp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests;

public sealed class WebHistoryPaginationTests
{
    [Fact]
    public void Paging_metadata_preserves_legacy_totalItems_and_adds_web_totalCount()
    {
        var meta = new PageMeta { Page = 2, PageSize = 20, TotalItems = 251 };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(meta, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(251, json.RootElement.GetProperty("totalItems").GetInt32());
        Assert.Equal(251, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(13, json.RootElement.GetProperty("totalPages").GetInt32());
        meta.TotalItems = 3;
        Assert.Equal(3, meta.TotalCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -20)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Invalid_page_inputs_are_rejected_before_offset_arithmetic(int page, int size) =>
        Assert.Throws<ValidationException>(() => new WebHistoryPageQuery { Page = page, PageSize = size }.ToMeta(500));

    [Theory]
    [InlineData(1, 0, 1, 0)]
    [InlineData(9, 21, 2, 2)]
    [InlineData(26, 651, 26, 33)]
    [InlineData(1_000_000, 2_000_000, 100_000, 100_000)]
    public void History_pages_can_pass_old_limits_and_clamp_after_deletions(int requested, int total, int expected, int pages)
    {
        var meta = new WebHistoryPageQuery { Page = requested, PageSize = 20 }.ToMeta(total);
        Assert.Equal(expected, meta.Page);
        Assert.Equal(total, meta.TotalCount);
        Assert.Equal(pages, meta.TotalPages);
    }

    [Fact]
    public void Mixed_activity_page_translates_to_a_single_ordered_union_without_connecting()
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = WebHistoryQueries.DigitalActivityKeys(db, SearchHelpTestScopes.BusinessId)
            .Skip(500).Take(20).ToQueryString();
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("500", sql);
        Assert.Contains("Kind", sql);
        Assert.Contains("ContextEntityId", sql);
        Assert.Contains("BusinessCustomerId", sql);
        Assert.DoesNotContain("RawPayloadJson", sql);
    }

    [Fact]
    public void Mixed_activity_total_count_query_translates_without_a_fixed_window()
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = WebHistoryQueries.DigitalActivityKeys(db, SearchHelpTestScopes.BusinessId)
            .GroupBy(x => 1).Select(group => group.Count()).ToQueryString();
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("count(", sql.ToLowerInvariant());
        Assert.DoesNotContain("LIMIT", sql);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(BusinessWebhookDeliveryStatus.Delivered)]
    public void Webhook_page_is_filtered_by_business_and_status_before_paging(BusinessWebhookDeliveryStatus? status)
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = WebHistoryQueries.WebhookDeliveries(db, SearchHelpTestScopes.BusinessId, status)
            .Skip(220).Take(10).ToQueryString();
        Assert.Contains("BusinessProfileId", sql);
        Assert.Contains("IsDeleted", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("PayloadJson", sql);
        Assert.DoesNotContain("SigningSecret", sql);
    }

    [Fact]
    public void Both_admin_exception_queries_translate_to_ordered_server_pages()
    {
        using var db = SearchHelpTestScopes.Database();
        var reconciliation = WebHistoryQueries.ReconciliationExceptions(db).Skip(500).Take(20).ToQueryString();
        var fees = WebHistoryQueries.FeeExceptions(db, 0.00000001m).Skip(500).Take(20).ToQueryString();
        foreach (var sql in new[] { reconciliation, fees })
        {
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("LIMIT", sql);
            Assert.Contains("OFFSET", sql);
            Assert.Contains("IsDeleted", sql);
        }
        Assert.Contains("LedgerTransactionId", reconciliation);
        Assert.Contains("NetworkFeeVariance", fees);
    }

    [Theory]
    [InlineData(true, BusinessPermission.ViewDigitalAssets)]
    [InlineData(false, BusinessPermission.ManageApiAccess)]
    public async Task Business_history_requires_the_existing_business_permission_before_database_access(bool digital, BusinessPermission expected)
    {
        using var db = SearchHelpTestScopes.Database();
        var access = new DenyAccess();
        var service = new WebHistoryPaginationService(db, access);
        if (digital)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDigitalActivityAsync(Guid.NewGuid(), new()));
        else
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetWebhookDeliveriesAsync(Guid.NewGuid(), new(), null));
        Assert.Equal(expected, access.RequestedPermission);
    }

    [Fact]
    public void The_read_controller_requires_authentication_and_sensitive_rate_limiting()
    {
        Assert.Single(typeof(WebHistoryPaginationController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Single(typeof(WebHistoryPaginationController).GetCustomAttributes<EnableRateLimitingAttribute>());
        Assert.Empty(typeof(WebHistoryPaginationController).GetMethods().SelectMany(x => x.GetCustomAttributes<AllowAnonymousAttribute>()));
    }

    [Theory]
    [InlineData(nameof(WebHistoryPaginationController.ReconciliationExceptions))]
    [InlineData(nameof(WebHistoryPaginationController.FeeExceptions))]
    public void Admin_history_retains_existing_operator_roles(string method)
    {
        var action = typeof(WebHistoryPaginationController).GetMethod(method)!;
        Assert.Equal("Admin,SuperAdmin,Compliance,Operations", Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>()).Roles);
    }

    [Theory]
    [InlineData(nameof(WebHistoryPaginationController.DigitalActivity), "business/digital-assets/activity/paged")]
    [InlineData(nameof(WebHistoryPaginationController.WebhookDeliveries), "business/embedded/webhook-deliveries/paged")]
    [InlineData(nameof(WebHistoryPaginationController.ReconciliationExceptions), "admin/digital-asset-providers/reconciliation-exceptions/paged")]
    [InlineData(nameof(WebHistoryPaginationController.FeeExceptions), "admin/digital-asset-providers/fee-exceptions/paged")]
    public void New_routes_are_explicit_gets_and_do_not_replace_legacy_contracts(string method, string route)
    {
        var action = typeof(WebHistoryPaginationController).GetMethod(method)!;
        Assert.Equal(route, Assert.Single(action.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Empty(action.GetCustomAttributes<HttpPostAttribute>());
    }

    private sealed class DenyAccess : IBusinessAccessService
    {
        public BusinessPermission? RequestedPermission { get; private set; }
        public Task<BusinessAccessContext> EnsurePermissionAsync(Guid userId, BusinessPermission permission, CancellationToken ct = default)
        {
            RequestedPermission = permission;
            return Task.FromException<BusinessAccessContext>(new UnauthorizedAccessException("Permission denied."));
        }
        public Task<BusinessAccessContext> GetAccessAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AvailableBusinessContextDto>> GetAvailableBusinessesAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
