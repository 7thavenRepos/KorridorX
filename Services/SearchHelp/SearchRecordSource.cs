using KorridorX.Dtos.SearchHelp;
namespace KorridorX.Services.SearchHelp;
// Allowlisted, read-only metadata. Never add secrets, account numbers, raw provider JSON or document bodies.
public sealed class SearchRecordRow
{
    public Guid Id { get; init; }
    public string Key { get; init; } = "";
    public string Title { get; init; } = "";
    public string Reference { get; init; } = "";
    public string Context { get; init; } = "";
    public int? StateNumber { get; init; }
    public string StateText { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public decimal? Amount { get; init; }
    public string AssetCode { get; init; } = "";
}
public sealed record SearchRecordSource(string Key, string Label, string ModuleRoute,
    IQueryable<SearchRecordRow> Query, Type? StateType = null, string? DetailPrefix = null)
{
    public IQueryable<SearchRecordRow> Match(SearchQuery query)
    {
        var result = Query;
        foreach (var word in query.Terms)
        {
            var term = word;
            if (Label.ToLowerInvariant().Split(' ').Contains(term) || Key == term) continue;
            var states = StateType is null ? Array.Empty<int>() : Enum.GetValues(StateType).Cast<object>()
                .Where(x => x.ToString()!.ToLowerInvariant().Contains(term)).Select(x => Convert.ToInt32(x)).ToArray();
            var exactId = query.ExactId;
            result = result.Where(x => (exactId.HasValue && x.Id == exactId.Value) ||
                x.Title.ToLower().Contains(term) || x.Reference.ToLower().Contains(term) ||
                x.Context.ToLower().Contains(term) || x.AssetCode.ToLower().Contains(term) ||
                x.StateText.ToLower().Contains(term) || (x.StateNumber.HasValue && states.Contains(x.StateNumber.Value)));
        }
        return result;
    }
    public static IOrderedQueryable<SearchRecordRow> Ranked(IQueryable<SearchRecordRow> query, SearchQuery search)
    {
        var folded = search.Folded; var id = search.ExactId;
        return query.OrderByDescending(x => (id.HasValue && x.Id == id.Value) || x.Reference.ToLower() == folded)
            .ThenByDescending(x => x.Title.ToLower() == folded)
            .ThenByDescending(x => x.Reference.ToLower().StartsWith(folded))
            .ThenByDescending(x => x.CreatedAt).ThenBy(x => x.Key).ThenBy(x => x.Id);
    }
    public SearchItemDto ToDto(SearchRecordRow row, string portal, SearchQuery? query = null)
    {
        var key = row.Key.Length > 0 ? row.Key : row.Id.ToString();
        var status = row.StateNumber.HasValue && StateType is not null ? Enum.GetName(StateType,row.StateNumber.Value) ?? "Unknown" : row.StateText;
        var route = DetailPrefix is null ? $"/{portal}/search/record/{Key}/{Uri.EscapeDataString(key)}" : DetailPrefix + Uri.EscapeDataString(key);
        var score = query is not null && (query.ExactId == row.Id || row.Reference.Equals(query.Text,StringComparison.OrdinalIgnoreCase)) ? 1000 :
            query?.ScoreText(row.Title,row.Reference,row.Context+" "+row.AssetCode+" "+status) ?? 0;
        return new SearchItemDto("record",Key,key,row.Title,row.Reference,row.Context,status,route,ModuleRoute,
            row.CreatedAt,row.Amount?.ToString(System.Globalization.CultureInfo.InvariantCulture),row.AssetCode,score);
    }
}
