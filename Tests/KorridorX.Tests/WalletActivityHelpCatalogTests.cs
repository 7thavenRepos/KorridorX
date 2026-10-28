using KorridorX.Services.SearchHelp;

namespace KorridorX.Tests;

public sealed class WalletActivityHelpCatalogTests
{
    [Fact]
    public void Embedded_catalog_loads_all_entries_and_new_wallet_fee_guides()
    {
        var catalog = new HelpCatalog();
        Assert.Equal(140, catalog.Entries.Count);
        Assert.Equal(100, catalog.Entries.Count(x => !x.IsPage));
        Assert.Equal(40, catalog.Entries.Count(x => x.IsPage));
        Assert.Equal(catalog.Entries.Count, catalog.Entries.Select(x => (x.Portal, x.Slug)).Distinct().Count());
        foreach (var slug in new[] { "automatic-wallet-receiving-accounts", "wallet-security-and-funding-notifications", "manage-transfer-fees" })
        {
            var guide = Assert.Single(catalog.Entries, x => x.Slug == slug);
            Assert.False(guide.IsPage);
            Assert.True(guide.Steps.Length >= 3);
            Assert.NotEmpty(guide.Prerequisites);
            Assert.NotEmpty(guide.ExpectedResult);
            Assert.NotEmpty(guide.Troubleshooting);
        }
    }

    [Fact]
    public void Receiving_setup_guide_retains_superadmin_boundary()
    {
        var catalog = new HelpCatalog();
        Assert.NotNull(catalog.Article(Admin("SuperAdmin"), "automatic-wallet-receiving-accounts"));
        Assert.Null(catalog.Article(Admin("Admin"), "automatic-wallet-receiving-accounts"));
        Assert.Null(catalog.Article(Admin("Operations"), "automatic-wallet-receiving-accounts"));
        Assert.Null(catalog.Article(Admin("Support"), "automatic-wallet-receiving-accounts"));
    }

    [Fact]
    public void Funding_notification_and_fee_guides_are_visible_to_permitted_operations_roles()
    {
        var catalog = new HelpCatalog();
        foreach (var slug in new[] { "wallet-security-and-funding-notifications", "manage-transfer-fees" })
        {
            Assert.NotNull(catalog.Article(Admin("SuperAdmin"), slug));
            Assert.NotNull(catalog.Article(Admin("Admin"), slug));
            Assert.NotNull(catalog.Article(Admin("Operations"), slug));
            Assert.Null(catalog.Article(Admin("Support"), slug));
            Assert.Null(catalog.Article(new SearchScope("business", Guid.NewGuid(), new HashSet<string> { "Business" }, null), slug));
        }
    }

    private static SearchScope Admin(string role) => new("admin", Guid.NewGuid(), new HashSet<string> { role }, null);
}
