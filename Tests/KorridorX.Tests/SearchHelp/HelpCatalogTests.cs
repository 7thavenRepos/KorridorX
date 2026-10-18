using KorridorX.Models.Enums;
using KorridorX.Services.SearchHelp;
namespace KorridorX.Tests.SearchHelp;
public sealed class HelpCatalogTests
{
    private readonly HelpCatalog catalog = new();
    [Fact] public void CompleteManifestHasNoDuplicateEntries()
    {
        Assert.Equal(125,catalog.Entries.Count);Assert.Equal(85,catalog.Entries.Count(x=>!x.IsPage));Assert.Equal(40,catalog.Entries.Count(x=>x.IsPage));
        Assert.Equal(catalog.Entries.Count,catalog.Entries.Select(x=>x.Portal+":"+x.Slug).Distinct().Count());
    }
    [Fact] public void EveryExistingDestinationHasAnActionableGuide()
    {
        foreach(var page in catalog.Entries.Where(x=>x.IsPage)) Assert.Contains(catalog.Entries,x=>!x.IsPage&&x.Portal==page.Portal&&x.PageRoute==page.PageRoute);
        foreach(var guide in catalog.Entries.Where(x=>!x.IsPage)) { Assert.True(guide.Steps.Length>=3); Assert.NotEmpty(guide.Prerequisites);Assert.NotEmpty(guide.ExpectedResult);Assert.NotEmpty(guide.Troubleshooting); }
    }
    [Fact] public void SupportCannotSeeProviderWalletGuides()
    {
        var scope=SearchHelpTestScopes.Admin("Support");
        Assert.DoesNotContain(catalog.Allowed(scope),x=>x.PageRoute.Contains("provider-wallets"));
        Assert.Null(catalog.Article(scope,"add-missing-discovered-asset"));
        Assert.Contains(catalog.Allowed(scope),x=>x.Slug=="admin-support-ticket");
    }
    [Fact] public void BusinessCannotSeeAdminCatalog()
    { Assert.All(catalog.Allowed(SearchHelpTestScopes.Business()),x=>Assert.Equal("business",x.Portal)); }
    [Fact] public void ViewerDoesNotReceiveMutatingBeneficiaryGuide()
    {
        var scope=SearchHelpTestScopes.Business(BusinessPermission.ViewBeneficiaries);
        Assert.Contains(catalog.Allowed(scope),x=>x.Slug=="page-beneficiaries");Assert.Null(catalog.Article(scope,"manage-beneficiary"));
    }
    [Fact] public void OnboardingContextDoesNotExposePaymentInstructions()
    {
        var scope=SearchHelpTestScopes.Business() with { Business=null };
        Assert.Contains(catalog.Allowed(scope),x=>x.Slug=="complete-business-verification");
        Assert.DoesNotContain(catalog.Allowed(scope),x=>x.PageRoute=="/business/transfers");
    }
    [Fact] public void ContextUsesLongestMatchingModulePath()
    {
        var result=catalog.Library(SearchHelpTestScopes.Admin("SuperAdmin"),"/admin/treasury/rebalancing?status=Pending");
        Assert.Single(result.Articles);Assert.Equal("provider-rebalance-workflow",result.Articles[0].Slug);
    }
    [Fact] public void RelatedGuidesArePermissionFiltered()
    {
        var scope=SearchHelpTestScopes.Admin("Compliance");
        var article=catalog.Article(scope,"digital-reconciliation-exceptions");Assert.NotNull(article);
        Assert.DoesNotContain(article.Related,x=>x.Slug=="manage-digital-providers");
    }
    [Fact] public void MissingAssetGuideKeepsWalletCreationSeparate()
    {
        var article=catalog.Article(SearchHelpTestScopes.Admin("SuperAdmin"),"add-missing-discovered-asset");Assert.NotNull(article);
        Assert.Contains("Only the master-data",article.ExpectedResult);Assert.Contains(article.Warnings,x=>x.Contains("does not create"));
    }
}
