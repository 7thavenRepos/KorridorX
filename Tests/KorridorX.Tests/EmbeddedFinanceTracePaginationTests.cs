using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Tests.SearchHelp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests;

public sealed class EmbeddedFinanceTracePaginationTests
{
    [Theory]
    [InlineData("providerTransactions")]
    [InlineData("providerRequests")]
    [InlineData("reservations")]
    [InlineData("ledgerTransactions")]
    [InlineData("journalEntries")]
    [InlineData("auditTimeline")]
    public void Trace_pages_translate_beyond_the_legacy_fifty_row_window(string section)
    {
        using var db = SearchHelpTestScopes.Database();
        var id = SearchHelpTestScopes.BusinessId;
        var ids = new HashSet<Guid> { id };
        var sql = section switch
        {
            "providerTransactions" => EmbeddedFinanceTraceQueries.ProviderTransactions(db, ids).Skip(200).Take(20).ToQueryString(),
            "providerRequests" => EmbeddedFinanceTraceQueries.ProviderRequests(db, ids).Skip(200).Take(20).ToQueryString(),
            "reservations" => EmbeddedFinanceTraceQueries.Reservations(db, id, ids).Skip(200).Take(20).ToQueryString(),
            "ledgerTransactions" => EmbeddedFinanceTraceQueries.Ledgers(db, ids).Include(x => x.Postings).Skip(200).Take(20).ToQueryString(),
            "journalEntries" => EmbeddedFinanceTraceQueries.Journals(db, id, ids).Include(x => x.Lines).Skip(200).Take(20).ToQueryString(),
            _ => EmbeddedFinanceTraceQueries.AuditTimeline(db, "Collection", id, id, id, id).Skip(200).Take(20).ToQueryString()
        };
        Assert.Contains("IsDeleted", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("200", sql);
        Assert.Contains("Id\" DESC", sql); // Unique tie-breaker, including equal timestamps.
    }

    [Fact]
    public void Journal_scope_uses_all_related_reservations_and_ledgers_not_their_visible_pages()
    {
        using var db = SearchHelpTestScopes.Database();
        var id = SearchHelpTestScopes.BusinessId;
        var query = EmbeddedFinanceTraceQueries.Journals(db, id, new() { id });
        var sql = query.ToQueryString();
        Assert.Contains("FinancialReservations", sql);
        Assert.Contains("LedgerTransactions", sql);
        Assert.Contains("SourceId", sql);
        Assert.Contains("RelatedEntityId", sql);
        Assert.DoesNotContain("LIMIT", sql);
        Assert.DoesNotContain("OFFSET", sql);
        var countSql = query.GroupBy(x => 1).Select(x => x.Count()).ToQueryString();
        Assert.Contains("count(", countSql.ToLowerInvariant());
        Assert.DoesNotContain("LIMIT", countSql);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Audit_scope_keeps_embedded_category_and_supports_missing_optional_links(bool linked)
    {
        using var db = SearchHelpTestScopes.Database();
        var id = SearchHelpTestScopes.BusinessId;
        var sql = EmbeddedFinanceTraceQueries.AuditTimeline(db, "Collection", id, id,
            linked ? id : null, linked ? id : null).Skip(80).Take(20).ToQueryString();
        Assert.Contains("EmbeddedFinance", sql);
        Assert.Contains("EntityName", sql);
        Assert.Contains("EntityId", sql);
        Assert.Contains("IsDeleted", sql);
        Assert.Contains("OFFSET", sql);
        if (linked) Assert.Contains("ProviderAccountMappings", sql);
    }

    [Theory]
    [InlineData("providerTransactions", nameof(EmbeddedFinanceTracePageQuery.ProviderTransactionsPage))]
    [InlineData("providerRequests", nameof(EmbeddedFinanceTracePageQuery.ProviderRequestsPage))]
    [InlineData("reservations", nameof(EmbeddedFinanceTracePageQuery.ReservationsPage))]
    [InlineData("ledgerTransactions", nameof(EmbeddedFinanceTracePageQuery.LedgerTransactionsPage))]
    [InlineData("journalEntries", nameof(EmbeddedFinanceTracePageQuery.JournalEntriesPage))]
    [InlineData("auditTimeline", nameof(EmbeddedFinanceTracePageQuery.AuditTimelinePage))]
    public void Each_section_has_its_own_page_and_clamps_after_a_shorter_refresh(string section, string property)
    {
        var request = new EmbeddedFinanceTracePageQuery { PageSize = 20 };
        typeof(EmbeddedFinanceTracePageQuery).GetProperty(property)!.SetValue(request, 8);
        var page = request.ToMeta(section, 151);
        Assert.Equal(8, page.Page);
        Assert.Equal(151, page.TotalItems);
        Assert.Equal(151, page.TotalCount);
        Assert.Equal(8, page.TotalPages);
        Assert.False(page.HasNextPage);
        Assert.Equal(2, request.ToMeta(section, 21).Page);
        Assert.Equal(1, request.ToMeta(section, 0).Page);
        Assert.Equal(0, request.ToMeta(section, 0).TotalPages);
    }

    [Theory]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.PageSize), 0)]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.PageSize), 101)]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.ProviderTransactionsPage), 0)]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.ProviderRequestsPage), -1)]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.ReservationsPage), 1_000_001)]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.LedgerTransactionsPage), 0)]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.JournalEntriesPage), int.MaxValue)]
    [InlineData(nameof(EmbeddedFinanceTracePageQuery.AuditTimelinePage), 0)]
    public void Invalid_paging_is_rejected_before_any_database_access(string property, int value)
    {
        using var db = SearchHelpTestScopes.Database();
        var request = new EmbeddedFinanceTracePageQuery();
        typeof(EmbeddedFinanceTracePageQuery).GetProperty(property)!.SetValue(request, value);
        var service = new EmbeddedFinanceAdminQueryService(db);
        Assert.Throws<ValidationException>(() => { _ = service.GetActivityDetailPageAsync("Collection", Guid.NewGuid(), request); });
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void Supported_user_page_lengths_produce_consistent_metadata(int size)
    {
        var query = new EmbeddedFinanceTracePageQuery { PageSize = size };
        var meta = query.ToMeta("providerRequests", 251);
        Assert.Equal(size, meta.PageSize);
        Assert.Equal((int)Math.Ceiling(251d / size), meta.TotalPages);
    }

    [Fact]
    public void Route_is_get_only_and_keeps_the_existing_internal_role_boundary()
    {
        var controller = typeof(AdminEmbeddedFinanceController);
        Assert.Equal("Compliance,InternalAudit,Operations,Admin,SuperAdmin", Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        var action = controller.GetMethod(nameof(AdminEmbeddedFinanceController.ActivityDetailPage))!;
        Assert.Equal("activity/{activityType}/{activityId:guid}/paged", Assert.Single(action.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(action.GetCustomAttributes<HttpPostAttribute>());
        Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Request_trace_dto_remains_sanitized_and_contains_no_provider_bodies_or_headers()
    {
        var json = JsonSerializer.Serialize(new EmbeddedFinanceAdminProviderRequestTraceDto(
            Guid.NewGuid(), "Test", "Completed", 200, false, DateTime.UtcNow, null, null));
        Assert.DoesNotContain("Headers", json);
        Assert.DoesNotContain("Body", json);
        Assert.DoesNotContain("Secret", json);
        Assert.DoesNotContain("Hash", json);
        Assert.DoesNotContain("ErrorMessage", json);
    }

    [Fact]
    public void Unknown_trace_sections_are_rejected() =>
        Assert.Throws<ArgumentException>(() => new EmbeddedFinanceTracePageQuery().ToMeta("rawPayload", 1));
}
