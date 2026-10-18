using System.Text.Json;
using KorridorX.Models.Enums;
using KorridorX.Services.SearchHelp;

namespace KorridorX.Tests.SearchHelp;

public sealed class HelpCatalogResourceTests
{
    // Resolve the production assembly, not a file relative to the test runner.
    // These checks exercise the same names used by HelpCatalog in deployment.
    [Theory]
    [InlineData("KorridorX.Content.SearchHelp.catalog.json", 103)]
    [InlineData("KorridorX.Content.SearchHelp.web-operations-20261017.json", 15)]
    [InlineData("KorridorX.Content.SearchHelp.payment-channels-20261018.json", 3)]
    [InlineData("KorridorX.Content.SearchHelp.interac-funding-20261018.json", 4)]
    public void Required_help_resources_are_embedded_and_readable(
        string logicalName, int expectedEntryCount)
    {
        var assembly = typeof(HelpCatalog).Assembly;
        Assert.Single(assembly.GetManifestResourceNames(), name => name == logicalName);
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Required embedded guide resource is missing: {logicalName}");
        using var json = JsonDocument.Parse(stream);
        Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind);
        Assert.Equal(expectedEntryCount, json.RootElement.GetArrayLength());
    }

    [Fact]
    public void Payment_channel_admin_guide_loads_with_superadmin_only_visibility()
    {
        var catalog = new HelpCatalog();
        var superadmin = SearchHelpTestScopes.Admin("SuperAdmin");
        var guide = catalog.Article(superadmin, "kx011-payment-channels");
        Assert.NotNull(guide);
        Assert.Contains(catalog.Library(superadmin, "/admin/payment-channels").Articles,
            article => article.Slug == "kx011-payment-channels");
        Assert.Null(catalog.Article(SearchHelpTestScopes.Admin("Support"), "kx011-payment-channels"));
        Assert.Null(catalog.Article(SearchHelpTestScopes.Admin("Admin"), "kx011-payment-channels"));
        Assert.Null(catalog.Article(SearchHelpTestScopes.Business(), "kx011-payment-channels"));
    }

    [Fact]
    public void Business_deposit_guide_loads_only_for_the_permitted_business_scope()
    {
        var catalog = new HelpCatalog();
        var permitted = SearchHelpTestScopes.Business(BusinessPermission.ViewWallets);
        Assert.NotNull(catalog.Article(permitted, "kx011-business-deposits"));
        Assert.Contains(catalog.Library(permitted, "/business/funding").Articles,
            article => article.Slug == "kx011-business-deposits");
        Assert.Null(catalog.Article(SearchHelpTestScopes.Business((BusinessPermission)0), "kx011-business-deposits"));
        Assert.Null(catalog.Article(permitted with { Business = null }, "kx011-business-deposits"));
        Assert.Null(catalog.Article(SearchHelpTestScopes.Admin("SuperAdmin"), "kx011-business-deposits"));
    }

    [Fact]
    public void Interac_guides_preserve_admin_and_business_permission_boundaries()
    {
        var catalog = new HelpCatalog();
        var admin = SearchHelpTestScopes.Admin("SuperAdmin");
        Assert.NotNull(catalog.Article(admin, "kx012-interac-receiving"));
        Assert.NotNull(catalog.Article(admin, "kx012-interac-review"));
        Assert.Null(catalog.Article(SearchHelpTestScopes.Admin("Support"), "kx012-interac-receiving"));
        Assert.Null(catalog.Article(SearchHelpTestScopes.Business(), "kx012-interac-review"));
        var business = SearchHelpTestScopes.Business(BusinessPermission.ViewWallets);
        Assert.NotNull(catalog.Article(business, "kx012-interac-funding"));
        Assert.Null(catalog.Article(business with { Business = null }, "kx012-interac-funding"));
        Assert.Null(catalog.Article(SearchHelpTestScopes.Business((BusinessPermission)0), "kx012-interac-funding"));
        Assert.Contains(catalog.Library(business, "/business/funding").Articles, x => x.Slug == "kx012-interac-funding");
    }
}
