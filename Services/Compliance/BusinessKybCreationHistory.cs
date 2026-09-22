using System.Text.Json;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;

namespace KorridorX.Services.Compliance;

public sealed record BusinessKybCreatedCustomer(string CustomerId, Guid RequestLogId);

/// <summary>Recovers only provider evidence already tied to this authorized local business.</summary>
public static class BusinessKybCreationHistory
{
    public static BusinessKybCreatedCustomer? Resolve(
        Guid businessProfileId, IEnumerable<ProviderRequestLog> logs)
    {
        var created = new List<BusinessKybCreatedCustomer>();
        var uncertain = false;
        foreach (var log in logs)
        {
            if (log.ProviderCode != ProviderCode.Blaaiz || log.IsDeleted ||
                log.HttpMethod != "POST" || log.Endpoint != "/api/external/customer") continue;
            try
            {
                using var request = JsonDocument.Parse(log.RequestBodyJson ?? "{}");
                if (!request.RootElement.TryGetProperty("businessProfileId", out var profile) ||
                    profile.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(profile.GetString(), out var recordedProfile) || recordedProfile != businessProfileId)
                    continue;
                if (!request.RootElement.TryGetProperty("Type", out var type) ||
                    !string.Equals(type.GetString(), "business", StringComparison.OrdinalIgnoreCase))
                    throw NeedsReview();

                if (log.ResponseStatusCode is >= 200 and <= 299)
                {
                    using var response = JsonDocument.Parse(log.ResponseBodyJson ?? "{}");
                    if (!response.RootElement.TryGetProperty("data", out var data) ||
                        data.ValueKind != JsonValueKind.Object ||
                        !data.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(id.GetString()) || id.GetString()!.Length > 150)
                        throw NeedsReview();
                    if (data.TryGetProperty("type", out var customerType) &&
                        customerType.ValueKind != JsonValueKind.Null &&
                        !string.Equals(customerType.GetString(), "business", StringComparison.OrdinalIgnoreCase))
                        throw NeedsReview();
                    created.Add(new BusinessKybCreatedCustomer(id.GetString()!, log.Id));
                }
                else if (log.ResponseStatusCode is not (400 or 401 or 403 or 404 or 405 or 422 or 429))
                {
                    // Timeouts, interrupted calls, 5xx and ambiguous conflicts must never
                    // trigger a second POST without establishing the first call's outcome.
                    uncertain = true;
                }
            }
            catch (JsonException) { throw NeedsReview(); }
            catch (InvalidOperationException ex) when (ex.Message != ReviewMessage) { throw NeedsReview(); }
        }
        var ids = created.Select(x => x.CustomerId).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length > 1) throw NeedsReview();
        if (ids.Length == 1) return created[0];
        if (uncertain) throw NeedsReview();
        return null;
    }

    private const string ReviewMessage =
        "An earlier provider customer-creation attempt needs recovery. Contact operations before trying again; no new customer was created by this attempt.";
    private static InvalidOperationException NeedsReview() => new(ReviewMessage);
}
