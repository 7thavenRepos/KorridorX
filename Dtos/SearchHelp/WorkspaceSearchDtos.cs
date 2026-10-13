namespace KorridorX.Dtos.SearchHelp;
public sealed record SearchItemDto(string Kind, string Category, string Key, string Title,
    string Reference, string Summary, string Status, string Route, string ModuleRoute,
    DateTime? CreatedAt, string? Amount, string AssetCode, int Score);
public sealed record SearchGroupDto(string Key, string Label, int Total, int Page, int PageSize,
    bool HasMore, IReadOnlyList<SearchItemDto> Items);
public sealed record WorkspaceSearchDto(string Query, string Portal, Guid? BusinessProfileId,
    int Total, IReadOnlyList<SearchGroupDto> Groups);
public sealed record GuideStepDto(string Title, string Instruction);
public sealed record GuideSummaryDto(string Slug, string Title, string Summary, string Category,
    string Route, string PageRoute, string ReviewedOn);
public sealed record GuideArticleDto(string Slug, string Title, string Summary, string Category,
    string PageRoute, string ReviewedOn, IReadOnlyList<string> Prerequisites,
    IReadOnlyList<GuideStepDto> Steps, string ExpectedResult, IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Troubleshooting, IReadOnlyList<GuideSummaryDto> Related);
public sealed record GuideLibraryDto(string Portal, Guid? BusinessProfileId, IReadOnlyList<GuideSummaryDto> Articles);
