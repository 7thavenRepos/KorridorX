using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Models.Enums;
using Microsoft.Extensions.Options;

namespace KorridorX.Providers.Screening;

public sealed class OpenSanctionsScreeningProvider : ISanctionsScreeningProvider
{
    public const string HttpClientName = "OpenSanctions";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenSanctionsOptions _options;
    private readonly ComplianceScreeningOptions _screeningOptions;
    private readonly ILogger<OpenSanctionsScreeningProvider> _logger;

    public OpenSanctionsScreeningProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenSanctionsOptions> options,
        IOptions<ComplianceScreeningOptions> screeningOptions,
        ILogger<OpenSanctionsScreeningProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _screeningOptions = screeningOptions.Value;
        _logger = logger;
    }

    public string ProviderCode => "OpenSanctions";

    public async Task<ScreeningProviderResult> ScreenAsync(
        ScreeningProviderRequest request,
        CancellationToken ct = default)
    {
        if (!_options.IsEnabled)
            throw new InvalidOperationException("OpenSanctions screening is disabled.");
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("OpenSanctions API key is not configured.");

        var payload = BuildRequest(request);
        var json = JsonSerializer.Serialize(payload);
        var path = BuildMatchPath();
        string responseJson = "";
        HttpStatusCode statusCode = 0;

        for (var attempt = 0; attempt <= _options.RetryCount; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var message = new HttpRequestMessage(HttpMethod.Post, path);
            message.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", _options.ApiKey);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            message.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
                statusCode = response.StatusCode;
                responseJson = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                    break;

                if (!ShouldRetry(response.StatusCode) || attempt == _options.RetryCount)
                {
                    throw new InvalidOperationException(
                        $"OpenSanctions screening failed with HTTP {(int)response.StatusCode}. " +
                        TrimProviderError(responseJson));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (attempt < _options.RetryCount)
            {
                _logger.LogWarning(
                    ex,
                    "OpenSanctions request attempt {Attempt} failed and will be retried.",
                    attempt + 1);
            }

            if (attempt < _options.RetryCount)
            {
                var delay = TimeSpan.FromMilliseconds(
                    _options.RetryBaseDelayMilliseconds * Math.Pow(2, attempt));
                await Task.Delay(delay, ct);
            }
        }

        if (statusCode == 0 || string.IsNullOrWhiteSpace(responseJson))
            throw new InvalidOperationException("OpenSanctions returned an empty response.");

        var parsed = OpenSanctionsResultParser.Parse(
            responseJson,
            _screeningOptions.PotentialMatchScore,
            _screeningOptions.BlockingMatchScore,
            _options.BlockPepMatches);

        var screenedAt = DateTime.UtcNow;
        var reference = parsed.ProviderReference ??
            $"OS-{screenedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..38];

        return new ScreeningProviderResult(
            ProviderCode,
            reference,
            parsed.Status,
            parsed.HighestMatchScore,
            parsed.IsBlocking,
            screenedAt,
            responseJson,
            parsed.Matches);
    }

    private object BuildRequest(ScreeningProviderRequest request)
    {
        var properties = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new[] { request.Name }
                .Concat(request.Aliases)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        if (!string.IsNullOrWhiteSpace(request.CountryCode))
            properties[ResolveSchema(request.SubjectType) == "Person" ? "nationality" : "country"] = [request.CountryCode!];
        if (request.DateOfBirth.HasValue)
            properties["birthDate"] = [request.DateOfBirth.Value.ToString("yyyy-MM-dd")];
        if (!string.IsNullOrWhiteSpace(request.RegistrationNumberLastFour))
            properties["registrationNumber"] = [request.RegistrationNumberLastFour!];

        return new
        {
            queries = new Dictionary<string, object>
            {
                ["korridorx"] = new
                {
                    schema = ResolveSchema(request.SubjectType),
                    properties
                }
            }
        };
    }

    private string BuildMatchPath()
    {
        var path = $"{_options.MatchEndpoint.Trim('/')}/{Uri.EscapeDataString(_options.Dataset)}";
        var query = new List<string>
        {
            $"limit={_options.MaximumResults}"
        };
        query.AddRange(_options.IncludeDatasets
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => $"include_dataset={Uri.EscapeDataString(x.Trim())}"));
        query.AddRange(_options.ExcludeDatasets
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => $"exclude_dataset={Uri.EscapeDataString(x.Trim())}"));
        return $"{path}?{string.Join("&", query)}";
    }

    private static string ResolveSchema(ScreeningSubjectType subjectType) => subjectType switch
    {
        ScreeningSubjectType.Business => "Company",
        ScreeningSubjectType.BusinessBeneficiary => "LegalEntity",
        _ => "Person"
    };

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static string TrimProviderError(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "No provider error body was returned.";
        var compact = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 500 ? compact : compact[..500];
    }
}

public sealed record OpenSanctionsParsedResult(
    string? ProviderReference,
    ScreeningStatus Status,
    decimal HighestMatchScore,
    bool IsBlocking,
    IReadOnlyList<ScreeningProviderMatch> Matches);

public static class OpenSanctionsResultParser
{
    public static OpenSanctionsParsedResult Parse(
        string responseJson,
        decimal potentialMatchScore,
        decimal blockingMatchScore,
        bool blockPepMatches)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty("responses", out var responses) ||
            !responses.TryGetProperty("korridorx", out var queryResponse))
        {
            throw new InvalidOperationException("OpenSanctions response did not contain responses.korridorx.");
        }

        var matches = new List<ScreeningProviderMatch>();
        if (queryResponse.TryGetProperty("results", out var results) &&
            results.ValueKind == JsonValueKind.Array)
        {
            foreach (var result in results.EnumerateArray())
            {
                var score = ReadDecimal(result, "score") * 100m;
                if (score < potentialMatchScore)
                    continue;

                var id = ReadString(result, "id") ?? Guid.NewGuid().ToString("N");
                var caption = ReadString(result, "caption") ?? "OpenSanctions match";
                var topics = ReadStringArray(result, "properties", "topics");
                var datasets = ReadStringArray(result, "datasets");
                var watchlistType = ResolveWatchlistType(topics);
                var blockingCategory = watchlistType is WatchlistType.Sanctions or WatchlistType.LawEnforcement ||
                    (watchlistType == WatchlistType.Pep && blockPepMatches);
                var isBlocking = blockingCategory && score >= blockingMatchScore;
                var listName = datasets.Count == 0
                    ? "OpenSanctions"
                    : string.Join(", ", datasets.Take(5));
                var countryCode = ReadStringArray(result, "properties", "country")
                    .Concat(ReadStringArray(result, "properties", "nationality"))
                    .FirstOrDefault();
                var birthDate = ParseDate(ReadStringArray(result, "properties", "birthDate").FirstOrDefault());
                var reason = topics.Count == 0
                    ? $"OpenSanctions returned a {score:0.##}% entity match."
                    : $"OpenSanctions returned a {score:0.##}% entity match with topics: {string.Join(", ", topics)}.";

                matches.Add(new ScreeningProviderMatch(
                    id,
                    watchlistType,
                    listName,
                    caption,
                    Math.Round(score, 2, MidpointRounding.AwayFromZero),
                    reason,
                    countryCode,
                    birthDate,
                    isBlocking,
                    result.GetRawText()));
            }
        }

        var highest = matches.Count == 0 ? 0m : matches.Max(x => x.MatchScore);
        var blocking = matches.Any(x => x.IsBlocking);
        // OpenSanctions scores express match confidence, not a final compliance decision.
        // All provider hits remain potential matches until a compliance reviewer confirms them.
        var status = matches.Count == 0
            ? ScreeningStatus.Clear
            : ScreeningStatus.PotentialMatch;
        var reference = matches.FirstOrDefault()?.ProviderMatchId;
        return new OpenSanctionsParsedResult(reference, status, highest, blocking, matches);
    }

    private static WatchlistType ResolveWatchlistType(IReadOnlyList<string> topics)
    {
        if (topics.Any(x => x.Contains("sanction", StringComparison.OrdinalIgnoreCase)))
            return WatchlistType.Sanctions;
        if (topics.Any(x => x.Contains("pep", StringComparison.OrdinalIgnoreCase)))
            return WatchlistType.Pep;
        if (topics.Any(x =>
                x.Contains("crime", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("wanted", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("debar", StringComparison.OrdinalIgnoreCase)))
        {
            return WatchlistType.LawEnforcement;
        }
        if (topics.Any(x => x.Contains("adverse", StringComparison.OrdinalIgnoreCase)))
            return WatchlistType.AdverseMedia;
        return WatchlistType.Other;
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return 0m;
        if (property.TryGetDecimal(out var value))
            return value;
        return property.ValueKind == JsonValueKind.String &&
               decimal.TryParse(property.GetString(), out value)
            ? value
            : 0m;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
                return Array.Empty<string>();
        }
        if (current.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        return current.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, out var parsed) ? parsed.Date : null;
}
