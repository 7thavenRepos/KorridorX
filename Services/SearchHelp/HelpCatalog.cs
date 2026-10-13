using System.Text.Json;
using KorridorX.Dtos.SearchHelp;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
namespace KorridorX.Services.SearchHelp;
public sealed class HelpEntry
{
    public string Slug { get; set; } = "";
    public string Portal { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Category { get; set; } = "";
    public string PageRoute { get; set; } = "";
    public string[] Keywords { get; set; } = [];
    public string[] AdminRoles { get; set; } = [];
    public string[] Permissions { get; set; } = [];
    public string[] BusinessRoles { get; set; } = [];
    public bool AllowNoBusiness { get; set; }
    public string[] Prerequisites { get; set; } = [];
    public GuideStepDto[] Steps { get; set; } = [];
    public string ExpectedResult { get; set; } = "";
    public string[] Warnings { get; set; } = [];
    public string[] Troubleshooting { get; set; } = [];
    public string ReviewedOn { get; set; } = "2026-10-13";
    public bool IsPage { get; set; }
    public bool VisibleTo(SearchScope scope)
    {
        if (Portal != scope.Portal) return false;
        if (scope.IsAdmin) return AdminRoles.Length > 0 && scope.HasRole(AdminRoles);
        if (scope.Business is null) return AllowNoBusiness;
        return Permissions.All(p => scope.Can(Enum.Parse<BusinessPermission>(p))) &&
            (BusinessRoles.Length == 0 || BusinessRoles.Contains(scope.Business.Role.ToString(),StringComparer.Ordinal));
    }
    public GuideSummaryDto SummaryDto() => new(Slug,Title,Summary,Category,$"/{Portal}/help/{Slug}",PageRoute,ReviewedOn);
    public string SearchText => string.Join(' ',Keywords)+" "+Summary+" "+string.Join(' ',Prerequisites)+" "+
        string.Join(' ',Steps.Select(s => s.Title+" "+s.Instruction))+" "+ExpectedResult+" "+string.Join(' ',Troubleshooting);
}
public sealed class HelpCatalog
{
    public IReadOnlyList<HelpEntry> Entries { get; }
    public HelpCatalog()
    {
        using var stream = typeof(HelpCatalog).Assembly.GetManifestResourceStream("KorridorX.Content.SearchHelp.catalog.json")
            ?? throw new InvalidOperationException("The embedded help catalog is missing.");
        Entries = JsonSerializer.Deserialize<HelpEntry[]>(stream,new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The help catalog is empty.");
        var unique = new HashSet<string>();
        foreach (var e in Entries)
        {
            if (!unique.Add(e.Portal+":"+e.Slug) || !System.Text.RegularExpressions.Regex.IsMatch(e.Slug,"^[a-z0-9-]+$") ||
                e.Portal is not ("admin" or "business") || !e.PageRoute.StartsWith("/"+e.Portal+"/",StringComparison.Ordinal) ||
                e.PageRoute.Contains("..",StringComparison.Ordinal) || e.PageRoute.Contains('\\') || e.Title.Length==0 ||
                (!e.IsPage && (e.Steps.Length<3 || e.Prerequisites.Length==0 || e.ExpectedResult.Length==0 || e.Troubleshooting.Length==0)))
                throw new InvalidOperationException("Invalid help catalog entry: "+e.Slug);
            foreach (var p in e.Permissions) if (!Enum.TryParse<BusinessPermission>(p,out _)) throw new InvalidOperationException("Invalid guide permission.");
            foreach (var r in e.AdminRoles) if (!SearchScopeResolver.AdminRoles.Contains(r)) throw new InvalidOperationException("Invalid guide role.");
            foreach (var r in e.BusinessRoles) if (!Enum.TryParse<BusinessUserRole>(r,out _)) throw new InvalidOperationException("Invalid business guide role.");
        }
    }
    public IReadOnlyList<HelpEntry> Allowed(SearchScope scope) => Entries.Where(e => e.VisibleTo(scope)).ToList();
    public GuideLibraryDto Library(SearchScope scope,string? pageRoute)
    {
        var entries = Allowed(scope).Where(e => !e.IsPage);
        if (!string.IsNullOrWhiteSpace(pageRoute))
        {
            var path = pageRoute.Split('?','#')[0].TrimEnd('/');
            var best = entries.Select(e => e.PageRoute).Distinct().Where(p => path==p || path.StartsWith(p+"/",StringComparison.Ordinal))
                .OrderByDescending(p => p.Length).FirstOrDefault();
            entries = best is null ? entries.Where(e => e.Category=="Getting started") : entries.Where(e => e.PageRoute==best);
        }
        return new GuideLibraryDto(scope.Portal,scope.BusinessId,entries.OrderBy(e => e.Category).ThenBy(e => e.Title).Select(e => e.SummaryDto()).ToList());
    }
    public GuideArticleDto? Article(SearchScope scope,string slug)
    {
        var allowed = Allowed(scope); var e = allowed.FirstOrDefault(x => !x.IsPage && x.Slug==slug);
        return e is null ? null : new GuideArticleDto(e.Slug,e.Title,e.Summary,e.Category,e.PageRoute,e.ReviewedOn,
            e.Prerequisites,e.Steps,e.ExpectedResult,e.Warnings,e.Troubleshooting,
            allowed.Where(x => !x.IsPage && x.Slug!=slug && x.Category==e.Category).Take(5).Select(x => x.SummaryDto()).ToList());
    }
}
