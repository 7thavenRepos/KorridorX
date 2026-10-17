using KorridorX.Services.SearchHelp;
namespace KorridorX.Tests.SearchHelp;

public sealed class WebOperationsGuideTests
{
    private readonly HelpCatalog catalog = new();
    [Fact] public void House_management_is_discoverable_to_operations()
    {
        var scope = SearchHelpTestScopes.Admin("Operations");
        Assert.Contains(catalog.Allowed(scope), x => x.IsPage && x.PageRoute == "/admin/treasury/house-accounts");
        var guide = catalog.Article(scope, "kx009-provision-house-accounts"); Assert.NotNull(guide); Assert.Equal("2026-10-17", guide.ReviewedOn);
    }
    [Theory][InlineData("Support")][InlineData("Compliance")][InlineData("InternalAudit")]
    public void Non_treasury_roles_cannot_read_provisioning_instructions(string role)
    { Assert.Null(catalog.Article(SearchHelpTestScopes.Admin(role), "kx009-provision-house-accounts")); }
    [Fact] public void Support_has_own_nonprivileged_triage_guide()
    { Assert.NotNull(catalog.Article(SearchHelpTestScopes.Admin("Support"), "kx009-web-operations-support-triage")); }
    [Fact] public void Both_portals_have_page_length_and_validation_guides()
    {
        foreach (var scope in new[] { SearchHelpTestScopes.Admin("SuperAdmin"), SearchHelpTestScopes.Business() })
        { Assert.NotNull(catalog.Article(scope, "kx009-web-list-pagination")); Assert.NotNull(catalog.Article(scope, "kx009-web-form-feedback")); }
    }
    [Fact] public void Business_cannot_read_admin_house_management()
    { Assert.Null(catalog.Article(SearchHelpTestScopes.Business(), "kx009-provision-house-accounts")); }
    [Fact] public void Existing_trading_entry_points_receive_new_setup_troubleshooting()
    {
        var entries = catalog.Entries.Where(x => x.Portal == "admin" && !x.IsPage && x.PageRoute == "/admin/trading-wallets").ToArray();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, x => x.SearchText.Contains("house accounts", StringComparison.OrdinalIgnoreCase));
    }
}
