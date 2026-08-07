using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Models.Enums;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Screening;

public sealed class ConfiguredWatchlistScreeningProvider : ISanctionsScreeningProvider
{
    private readonly ComplianceScreeningOptions _options;

    public ConfiguredWatchlistScreeningProvider(IOptions<ComplianceScreeningOptions> options)
    {
        _options = options.Value;
    }

    public string ProviderCode => _options.ProviderCode;

    public Task<ScreeningProviderResult> ScreenAsync(
        ScreeningProviderRequest request,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var candidates = new[] { request.Name }
            .Concat(request.Aliases)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matches = new List<ScreeningProviderMatch>();
        foreach (var entry in _options.Entries)
        {
            var names = new[] { entry.Name }
                .Concat(entry.Aliases)
                .Where(x => !string.IsNullOrWhiteSpace(x));

            decimal highest = 0m;
            string? matchedCandidate = null;
            string? matchedWatchlistName = null;
            foreach (var candidate in candidates)
            {
                foreach (var watchlistName in names)
                {
                    var score = ScreeningNameMatcher.CalculateScore(candidate, watchlistName);
                    if (score > highest)
                    {
                        highest = score;
                        matchedCandidate = candidate;
                        matchedWatchlistName = watchlistName;
                    }
                }
            }

            if (highest < _options.PotentialMatchScore)
                continue;

            var countryConsistent = string.IsNullOrWhiteSpace(entry.CountryCode) ||
                string.IsNullOrWhiteSpace(request.CountryCode) ||
                string.Equals(entry.CountryCode, request.CountryCode, StringComparison.OrdinalIgnoreCase);
            var dateConsistent = entry.DateOfBirth is null || request.DateOfBirth is null ||
                entry.DateOfBirth.Value.Date == request.DateOfBirth.Value.Date;
            var adjustedScore = highest;
            if (!countryConsistent)
                adjustedScore = Math.Max(0m, adjustedScore - 10m);
            if (!dateConsistent)
                adjustedScore = Math.Max(0m, adjustedScore - 15m);

            if (adjustedScore < _options.PotentialMatchScore)
                continue;

            var isBlocking = entry.IsBlocking && adjustedScore >= _options.BlockingMatchScore;
            var reason = $"Name '{matchedCandidate}' matched '{matchedWatchlistName}' at {adjustedScore:0.##}%.";
            matches.Add(new ScreeningProviderMatch(
                entry.Id,
                entry.WatchlistType,
                entry.ListName,
                entry.Name,
                adjustedScore,
                reason,
                entry.CountryCode,
                entry.DateOfBirth,
                isBlocking,
                JsonSerializer.Serialize(new
                {
                    entry.Id,
                    entry.Name,
                    entry.Aliases,
                    entry.WatchlistType,
                    entry.ListName,
                    adjustedScore,
                    countryConsistent,
                    dateConsistent
                })));
        }

        var highestScore = matches.Count == 0 ? 0m : matches.Max(x => x.MatchScore);
        var blocking = matches.Any(x => x.IsBlocking);
        var status = matches.Count == 0
            ? ScreeningStatus.Clear
            : blocking
                ? ScreeningStatus.ConfirmedMatch
                : ScreeningStatus.PotentialMatch;
        var screenedAt = DateTime.UtcNow;
        var reference = $"SCR-{screenedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..38];
        var raw = JsonSerializer.Serialize(new
        {
            request.SubjectType,
            request.SubjectId,
            request.Name,
            request.CountryCode,
            status,
            highestScore,
            blocking,
            matchCount = matches.Count
        });

        return Task.FromResult(new ScreeningProviderResult(
            ProviderCode,
            reference,
            status,
            highestScore,
            blocking,
            screenedAt,
            raw,
            matches));
    }
}
