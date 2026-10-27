using KorridorX.Dtos.SearchHelp;
using Microsoft.EntityFrameworkCore;
namespace KorridorX.Services.SearchHelp;
public sealed class WorkspaceSearchService(SearchRecordCatalog records, HelpCatalog help)
{
    public async Task<WorkspaceSearchDto> SearchAsync(SearchScope scope,string? text,string? category,int page,int pageSize,CancellationToken ct)
    {
        var query = new SearchQuery(text);
        if (page<1 || page>10000 || pageSize<1 || pageSize>25) throw new ArgumentException("Page must be 1-10000 and page size 1-25.");
        category = string.IsNullOrWhiteSpace(category) ? "all" : category.Trim();
        if (category=="all" && page!=1) throw new ArgumentException("Choose a category before paging.");
        var sources = records.Sources(scope);
        if (!query.CanSearch || (category!="all" && category!="pages" && category!="guides" && category!="errors" && !sources.Any(s => s.Key==category)))
            return new WorkspaceSearchDto(query.Text,scope.Portal,scope.BusinessId,0,[]);
        var groups = new List<SearchGroupDto>(); var size = category=="all" ? 3 : pageSize;
        foreach (var kind in new[] { "errors", "pages", "guides" })
        {
            if (category!="all" && category!=kind) continue;
            var matches = help.Allowed(scope).Where(e => kind == "errors" ? e.ErrorCodes.Length > 0 :
                    kind == "pages" ? e.IsPage : !e.IsPage && e.ErrorCodes.Length == 0)
                .Select(e => (Entry:e,Score:query.ScoreHelp(e))).Where(x => x.Score>0)
                .OrderByDescending(x => x.Score).ThenBy(x => x.Entry.Slug).ToList();
            var items = matches.Skip((page-1)*size).Take(size).Select(x => new SearchItemDto(kind=="pages"?"page":"guide",
                kind,x.Entry.Slug,x.Entry.Title,string.Join(", ",x.Entry.ErrorCodes),x.Entry.Summary,"",kind=="pages"?x.Entry.PageRoute:$"/{scope.Portal}/help/{x.Entry.Slug}",
                x.Entry.PageRoute,null,null,"",x.Score)).ToList();
            if (matches.Count>0 || category==kind) groups.Add(new SearchGroupDto(kind,kind=="pages"?"Pages and features":kind=="errors"?"Errors and fixes":"How-to guides",matches.Count,page,size,matches.Count>page*size,items));
        }
        // One scoped DbContext: deliberately sequential. Counts and rows use the SAME authorized predicate.
        foreach (var source in sources.Where(s => query.CanSearchRecords && (category=="all" || s.Key==category)))
        {
            var filtered=source.Match(query); var total=await filtered.CountAsync(ct);
            if (total==0 && category=="all") continue;
            var rows=await SearchRecordSource.Ranked(filtered,query).Skip((page-1)*size).Take(size).ToListAsync(ct);
            groups.Add(new SearchGroupDto(source.Key,source.Label,total,page,size,total>page*size,rows.Select(x => source.ToDto(x,scope.Portal,query)).ToList()));
        }
        return new WorkspaceSearchDto(query.Text,scope.Portal,scope.BusinessId,groups.Sum(x => x.Total),
            groups.OrderByDescending(x => x.Items.Select(i => i.Score).DefaultIfEmpty(0).Max()).ThenBy(x => x.Label).ToList());
    }
    public async Task<SearchItemDto?> RecordAsync(SearchScope scope,string category,string key,CancellationToken ct)
    {
        if (key.Length>120) return null;
        var source=records.Sources(scope).FirstOrDefault(x => x.Key==category); if (source is null) return null;
        var normalized=key.Trim().ToUpperInvariant(); var isGuid=Guid.TryParse(key,out var id) && id!=Guid.Empty;
        var row=await source.Query.FirstOrDefaultAsync(x => (isGuid && x.Id==id) || (x.Key!="" && x.Key==normalized),ct);
        return row is null ? null : source.ToDto(row,scope.Portal);
    }
}
