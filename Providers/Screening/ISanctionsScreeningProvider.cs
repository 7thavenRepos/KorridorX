using KorridorX.Models.Enums;

namespace KorridorX.Providers.Screening;

public interface ISanctionsScreeningProvider
{
    string ProviderCode { get; }

    Task<ScreeningProviderResult> ScreenAsync(
        ScreeningProviderRequest request,
        CancellationToken ct = default);
}

public sealed record ScreeningProviderRequest(
    ScreeningSubjectType SubjectType,
    Guid SubjectId,
    string Name,
    IReadOnlyList<string> Aliases,
    string? CountryCode,
    DateTime? DateOfBirth,
    string? RegistrationNumberLastFour,
    ScreeningReason Reason);

public sealed record ScreeningProviderResult(
    string ProviderCode,
    string ProviderReference,
    ScreeningStatus Status,
    decimal HighestMatchScore,
    bool IsBlocking,
    DateTime ScreenedAt,
    string RawResultJson,
    IReadOnlyList<ScreeningProviderMatch> Matches);

public sealed record ScreeningProviderMatch(
    string ProviderMatchId,
    WatchlistType WatchlistType,
    string ListName,
    string MatchedName,
    decimal MatchScore,
    string MatchReason,
    string? CountryCode,
    DateTime? DateOfBirth,
    bool IsBlocking,
    string RawJson);
