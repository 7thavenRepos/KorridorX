using System.Linq.Expressions;
using KorridorX.Models.Enums;
using KorridorX.Services.SearchHelp;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Tests.SearchHelp;

public sealed class SearchCatalogTranslationTests
{
    // Case discovery constructs query expressions only. SearchHelpTestScopes.Database
    // uses an invalid loopback port, and ToQueryString does not execute database SQL.
    public static IEnumerable<object[]> AuthorizedSources()
    {
        using var db = SearchHelpTestScopes.Database();
        var catalog = new SearchRecordCatalog(db);
        return new[] { SearchHelpTestScopes.Admin("SuperAdmin"), SearchHelpTestScopes.Business() }
            .SelectMany(scope => catalog.Sources(scope)
                .Select(source => new object[] { scope.Portal, source.Key }))
            .ToArray();
    }

    [Theory]
    [MemberData(nameof(AuthorizedSources))]
    public void EveryAuthorizedProjectionAndSearchTranslatesWithoutDatabaseConnection(string portal, string category)
    {
        using var db = SearchHelpTestScopes.Database();
        var scope = portal == "admin" ? SearchHelpTestScopes.Admin("SuperAdmin") : SearchHelpTestScopes.Business();
        var source = Assert.Single(new SearchRecordCatalog(db).Sources(scope), x => x.Key == category);
        Assert.Contains("SELECT", source.Query.Take(1).ToQueryString());

        foreach (var text in new[] { "USD", "pending transfers", "100%", "R_1", "active", "supported", "USDC", SearchHelpTestScopes.User.ToString() })
        {
            var query = new SearchQuery(text);
            var sql = SearchRecordSource.Ranked(source.Match(query), query).Skip(3).Take(3).ToQueryString();
            Assert.Contains("SELECT", sql);
            Assert.Contains("LIMIT", sql);
            Assert.Contains("OFFSET", sql);
            if (portal == "business") Assert.Contains(SearchHelpTestScopes.BusinessId.ToString(), sql);
        }

        // Match the production record-summary lookup, for GUIDs and code keys.
        foreach (var key in new[] { SearchHelpTestScopes.User.ToString(), "USD", "NG", "not-an-id" })
        {
            var normalized = key.Trim().ToUpperInvariant();
            var isGuid = Guid.TryParse(key, out var id) && id != Guid.Empty;
            var sql = source.Query
                .Where(x => (isGuid && x.Id == id) || (x.Key != "" && x.Key == normalized))
                .Take(1).ToQueryString();
            Assert.Contains("SELECT", sql);
            Assert.Contains("LIMIT", sql);
            if (portal == "business") Assert.Contains(SearchHelpTestScopes.BusinessId.ToString(), sql);
        }
    }

    [Fact]
    public void EveryProjectionExplicitlyBindsAllSearchFields()
    {
        using var db = SearchHelpTestScopes.Database();
        var catalog = new SearchRecordCatalog(db);
        var expected = typeof(SearchRecordRow).GetProperties().Select(x => x.Name).OrderBy(x => x).ToArray();
        var keys = new HashSet<string>();
        foreach (var scope in new[] { SearchHelpTestScopes.Admin("SuperAdmin"), SearchHelpTestScopes.Business() })
        foreach (var source in catalog.Sources(scope))
        {
            keys.Add(source.Key);
            var select = Assert.IsAssignableFrom<MethodCallExpression>(source.Query.Expression);
            Assert.Equal(nameof(Queryable.Select), select.Method.Name);
            var quoted = Assert.IsAssignableFrom<UnaryExpression>(select.Arguments[1]);
            var selector = Assert.IsAssignableFrom<LambdaExpression>(quoted.Operand);
            var projection = Assert.IsAssignableFrom<MemberInitExpression>(selector.Body);
            var actual = projection.Bindings.Select(x => x.Member.Name).OrderBy(x => x).ToArray();
            Assert.True(expected.SequenceEqual(actual),
                $"{scope.Portal}/{source.Key}: every search field must be explicitly projected. " +
                $"Missing: {string.Join(", ", expected.Except(actual))}");

            var key = Assert.Single(projection.Bindings, x => x.Member.Name == nameof(SearchRecordRow.Key));
            var assignment = Assert.IsAssignableFrom<MemberAssignment>(key);
            if (source.Key is "assets" or "countries")
            {
                var member = Assert.IsAssignableFrom<MemberExpression>(assignment.Expression);
                Assert.Equal("Code", member.Member.Name);
            }
            else
            {
                var call = Assert.IsAssignableFrom<MethodCallExpression>(assignment.Expression);
                Assert.Equal(nameof(Guid.ToString), call.Method.Name);
                Assert.NotNull(call.Object);
                Assert.Equal(typeof(Guid), call.Object!.Type);
            }
        }
        Assert.Equal(30, keys.Count);
    }

    [Fact]
    public void BusinessPermissionCatalogIsExplicit()
    {
        using var db = SearchHelpTestScopes.Database();
        var catalog = new SearchRecordCatalog(db);
        var none = catalog.Sources(SearchHelpTestScopes.Business(BusinessPermission.None)).Select(x => x.Key).ToArray();
        Assert.Equal(new[] { "tickets", "disputes" }, none);
        var wallets = catalog.Sources(SearchHelpTestScopes.Business(BusinessPermission.ViewWallets)).Select(x => x.Key).ToArray();
        Assert.Contains("wallets", wallets);
        Assert.Contains("collections", wallets);
        Assert.DoesNotContain("transfers", wallets);
    }

    [Fact]
    public void InternalRolesDoNotGainSuperAdminDiscovery()
    {
        using var db = SearchHelpTestScopes.Database();
        var catalog = new SearchRecordCatalog(db);
        Assert.DoesNotContain(catalog.Sources(SearchHelpTestScopes.Admin("Admin")), x => x.Key == "provider-wallets");
        Assert.Equal(new[] { "tickets", "disputes" }, catalog.Sources(SearchHelpTestScopes.Admin("Support")).Select(x => x.Key).ToArray());
        Assert.DoesNotContain(catalog.Sources(SearchHelpTestScopes.Admin("InternalAudit")), x => x.Key == "users");
    }

    [Fact]
    public void BusinessSqlContainsCurrentOwnershipBoundary()
    {
        using var db = SearchHelpTestScopes.Database();
        var sources = new SearchRecordCatalog(db).Sources(SearchHelpTestScopes.Business());
        foreach (var source in sources)
        {
            var sql = source.Query.Take(1).ToQueryString();
            Assert.Contains(SearchHelpTestScopes.BusinessId.ToString(), sql);
            if (source.Key is "tickets" or "disputes") Assert.Contains(SearchHelpTestScopes.User.ToString(), sql);
        }
    }
}
