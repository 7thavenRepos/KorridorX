using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using KorridorX.Controllers;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.EmbeddedFinance;
using KorridorX.Tests.SearchHelp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests;

public sealed class EmbeddedFinanceExceptionPaginationTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 18, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("Activity", false)]
    [InlineData("Activity", true)]
    [InlineData("WebhookDelivery", false)]
    [InlineData("WebhookDelivery", true)]
    [InlineData("ProviderProvisioning", false)]
    [InlineData("ProviderProvisioning", true)]
    public void Complete_filtered_queue_and_count_translate_without_recent_source_windows(string? source, bool filtered)
    {
        using var db = SearchHelpTestScopes.Database();
        var query = EmbeddedFinanceExceptionQueries.Build(db, filtered ? SearchHelpTestScopes.BusinessId : null,
            source, filtered ? "legacy_%\\reference" : null, Now);
        var visitor = new WindowVisitor();
        visitor.Visit(query.Expression);
        Assert.Empty(visitor.Windows); // No Take/Skip anywhere before the caller requests a page.
        var sql = EmbeddedFinanceExceptionQueries.Order(query).Skip(1600).Take(20).ToQueryString();
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("1600", sql);
        Assert.Contains("IsDeleted", sql);
        if (source is null) Assert.Contains("UNION ALL", sql);
        if (filtered)
        {
            Assert.Contains("ILIKE", sql);
            Assert.Contains("BusinessProfileId", sql);
        }
        // Exercise an aggregate over the very same full filtered query, without connecting.
        var countSql = query.GroupBy(x => 1).Select(x => x.Count()).ToQueryString();
        Assert.Contains("count(", countSql.ToLowerInvariant());
        Assert.DoesNotContain("OFFSET", countSql);
        Assert.DoesNotContain("LIMIT 250", countSql);
        Assert.DoesNotContain("RawPayloadJson", sql);
        Assert.DoesNotContain("SigningSecret", sql);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(" activity ", "Collections")]
    [InlineData("wEbHoOkDeLiVeRy", "BusinessWebhookDeliveries")]
    [InlineData(" PROVIDERPROVISIONING ", "ProviderAccountMappings")]
    public void Source_filters_are_case_insensitive_and_do_not_query_unrelated_sources(string source, string table)
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = EmbeddedFinanceExceptionQueries.Build(db, null, source, null, Now).ToQueryString();
        Assert.Contains(table, sql);
        if (!source.Trim().Equals("activity", StringComparison.OrdinalIgnoreCase)) Assert.DoesNotContain("\"Collections\"", sql);
        if (!source.Trim().Equals("WebhookDelivery", StringComparison.OrdinalIgnoreCase)) Assert.DoesNotContain("\"BusinessWebhookDeliveries\"", sql);
    }

    [Fact]
    public void Activity_query_covers_all_seven_activity_types_and_customer_scopes()
    {
        using var db = SearchHelpTestScopes.Database();
        var sql = EmbeddedFinanceExceptionQueries.ActivityCandidates(db, SearchHelpTestScopes.BusinessId).ToQueryString();
        foreach (var table in new[] { "Collections", "Payouts", "Transfers", "DigitalAssetDepositIntents", "DigitalAssetWithdrawals", "FinancialReservations" })
            Assert.Contains(table, sql);
        Assert.Contains("MarketplaceTradeReservation", sql);
        Assert.Contains("InstantTradeReservation", sql);
        Assert.Contains("BusinessCustomer", sql);
        Assert.Contains("OwnerType", sql);
        Assert.Contains("Purpose", sql);
        Assert.Contains("IsDeleted", sql);
    }

    [Theory]
    [InlineData("Collection", typeof(CollectionStatus))]
    [InlineData("Payout", typeof(PayoutStatus))]
    [InlineData("Transfer", typeof(TransferStatus))]
    [InlineData("DigitalAssetDeposit", typeof(CollectionStatus))]
    [InlineData("DigitalAssetWithdrawal", typeof(DigitalAssetWithdrawalStatus))]
    [InlineData("MarketplaceTradeReservation", typeof(FinancialReservationStatus))]
    [InlineData("InstantTradeReservation", typeof(FinancialReservationStatus))]
    public void Activity_eligibility_severity_and_reason_match_the_existing_rules(string type, Type statusEnum)
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var legacyNeeds = typeof(EmbeddedFinanceAdminQueryService).GetMethod("NeedsAttention", flags)!;
        var legacyStale = typeof(EmbeddedFinanceAdminQueryService).GetMethod("IsStaleActivity", flags)!;
        var legacySeverity = typeof(EmbeddedFinanceAdminQueryService).GetMethod("ExceptionSeverity", flags)!;
        foreach (var status in Enum.GetNames(statusEnum).Append("987"))
        foreach (var age in new[] { 29, 30, 31 })
        foreach (var finalized in new[] { false, true })
        foreach (var reason in new string?[] { null, "", " \t", "Provider needs review" })
        {
            var candidate = new EmbeddedFinanceExceptionActivityRow
            {
                Id = Guid.NewGuid(), ActivityType = type, BusinessProfileId = SearchHelpTestScopes.BusinessId,
                BusinessName = "Business", BusinessCustomerId = SearchHelpTestScopes.User,
                BusinessCustomerName = "Customer", Reference = "REF", Status = status,
                OccurredAt = Now.AddMinutes(-age), FinalizedAt = finalized ? Now : null, FailureReason = reason
            };
            var legacy = new EmbeddedFinanceAdminActivityDto(
                Id: candidate.Id, ActivityType: type, Direction: "Outbound", BusinessProfileId: candidate.BusinessProfileId,
                BusinessName: candidate.BusinessName, BusinessCustomerId: candidate.BusinessCustomerId,
                BusinessCustomerName: candidate.BusinessCustomerName, CollectionAccountId: null,
                CollectionAccountReference: null, FinancialAccountId: null, Reference: "REF", ExternalReference: null,
                AssetCode: "CAD", DestinationAssetCode: null, Amount: 1m, SecondaryAmount: null, FeeAmount: null,
                Status: status, ProviderCode: null, ProviderReference: null, ProviderRequestLogId: null,
                RelatedEntityType: null, RelatedEntityId: null, OccurredAt: candidate.OccurredAt,
                FinalizedAt: candidate.FinalizedAt, FailureReason: reason);
            var expected = (bool)legacyNeeds.Invoke(null, new object[] { legacy, Now })!;
            var actual = EmbeddedFinanceExceptionQueries.ActivityExceptions(new[] { candidate }.AsQueryable(), Now).ToList();
            Assert.Equal(expected ? 1 : 0, actual.Count);
            if (!expected) continue;
            var stale = (bool)legacyStale.Invoke(null, new object[] { legacy, Now })!;
            var dto = EmbeddedFinanceExceptionQueries.ToDto(actual[0]);
            Assert.Equal((string)legacySeverity.Invoke(null, new object[] { legacy, stale })!, dto.Severity);
            Assert.Equal(reason ?? (stale ? EmbeddedFinanceExceptionQueries.StaleReason : null), dto.Reason);
            Assert.Equal(type, dto.ActivityType);
            Assert.Equal(candidate.Id, dto.ActivityId);
            Assert.Equal(candidate.BusinessProfileId, dto.BusinessProfileId);
        }
    }

    [Fact]
    public void Older_exceptions_are_not_hidden_by_more_than_five_hundred_healthy_activities()
    {
        var activities = Enumerable.Range(0, 1200).Select(i => new EmbeddedFinanceExceptionActivityRow
        {
            Id = Id(i), ActivityType = "Collection", Status = "Successful", OccurredAt = Now.AddSeconds(-i), FinalizedAt = Now
        }).Concat(new[] { new EmbeddedFinanceExceptionActivityRow
        {
            Id = Id(2000), ActivityType = "Collection", Status = "Failed", OccurredAt = Now.AddYears(-1)
        } });
        var items = EmbeddedFinanceExceptionQueries.ActivityExceptions(activities.AsQueryable(), Now).ToList();
        Assert.Equal(Id(2000), Assert.Single(items).SourceId);
    }

    [Fact]
    public void Global_pages_include_all_sources_and_have_stable_unique_tie_breakers()
    {
        var rows = Enumerable.Range(0, 1800).Select(i => new EmbeddedFinanceExceptionRow
        {
            SourceId = Id(i), SourceType = i % 3 == 0 ? "Activity" : i % 3 == 1 ? "WebhookDelivery" : "ProviderProvisioning",
            ActivityType = i % 3 == 0 ? "Transfer" : null,
            SeverityRank = i % 3 + 1, OccurredAt = Now, Reference = $"REF-{i}"
        }).ToArray();
        var expected = EmbeddedFinanceExceptionQueries.Order(rows.AsQueryable()).Select(x => x.SourceId).ToArray();
        var actual = Enumerable.Range(0, 90).SelectMany(page =>
            EmbeddedFinanceExceptionQueries.Order(rows.Reverse().AsQueryable()).Skip(page * 20).Take(20)).ToArray();
        Assert.Equal(expected, actual.Select(x => x.SourceId).ToArray());
        Assert.Equal(1800, actual.Select(x => x.SourceId).Distinct().Count());
        Assert.Equal(3, actual[0].SeverityRank);
        Assert.Equal(1, actual[^1].SeverityRank);
    }

    [Theory]
    [InlineData(200, 20, 1800, 90, 90)]
    [InlineData(0, 20, 1800, 1, 90)]
    [InlineData(10, 20, 21, 2, 2)]
    [InlineData(10, 20, 0, 1, 0)]
    [InlineData(int.MaxValue, 100, 1801, 19, 19)]
    [InlineData(1, 200, 1801, 1, 19)]
    [InlineData(1, 0, 4, 1, 4)]
    public void Paging_is_bounded_and_clamps_after_shorter_refresh(int page, int size, int total, int expectedPage, int expectedPages)
    {
        var meta = EmbeddedFinanceExceptionQueries.Page(page, size, total);
        Assert.Equal(expectedPage, meta.Page);
        Assert.Equal(expectedPages, meta.TotalPages);
        Assert.Equal(total, meta.TotalCount);
        Assert.InRange(meta.PageSize, 1, 100);
        Assert.InRange(checked((meta.Page - 1) * meta.PageSize), 0, int.MaxValue);
    }

    [Theory]
    [InlineData("ordinary", "%ordinary%")]
    [InlineData("%_", "%\\%\\_%")]
    [InlineData("a\\b", "%a\\\\b%")]
    [InlineData("'", "%'%")]
    public void Search_escapes_SQL_wildcards_but_keeps_parameterized_literal_text(string input, string expected) =>
        Assert.Equal(expected, EmbeddedFinanceExceptionQueries.LiteralContainsPattern(input));

    [Fact]
    public void Existing_read_only_route_and_authorization_are_unchanged()
    {
        var controller = typeof(AdminEmbeddedFinanceController);
        Assert.Equal("Compliance,InternalAudit,Operations,Admin,SuperAdmin", Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        var action = controller.GetMethod(nameof(AdminEmbeddedFinanceController.Exceptions))!;
        Assert.Equal("exceptions", Assert.Single(action.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
        Assert.Empty(action.GetCustomAttributes<HttpPostAttribute>());
        Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Response_shape_preserves_exception_links_and_excludes_provider_payloads()
    {
        var dto = EmbeddedFinanceExceptionQueries.ToDto(new EmbeddedFinanceExceptionRow
        {
            SourceType = "Activity", SeverityRank = 2, SourceId = Id(1), ActivityId = Id(1), ActivityType = "Transfer",
            BusinessProfileId = SearchHelpTestScopes.BusinessId, BusinessName = "Example", Status = "Failed", OccurredAt = Now
        });
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal("High", json.RootElement.GetProperty("severity").GetString());
        Assert.Equal(Id(1), json.RootElement.GetProperty("activityId").GetGuid());
        Assert.Equal(15, json.RootElement.EnumerateObject().Count());
        Assert.False(json.RootElement.TryGetProperty("payloadJson", out _));
        Assert.False(json.RootElement.TryGetProperty("headers", out _));
    }

    private static Guid Id(int i) => Guid.Parse($"00000000-0000-0000-0000-{i:D12}");

    private sealed class WindowVisitor : ExpressionVisitor
    {
        public List<string> Windows { get; } = new();
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) && node.Method.Name is "Take" or "Skip") Windows.Add(node.Method.Name);
            return base.VisitMethodCall(node);
        }
    }
}
