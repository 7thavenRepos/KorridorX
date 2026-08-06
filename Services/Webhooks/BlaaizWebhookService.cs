using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Webhooks;

public class BlaaizWebhookService : IBlaaizWebhookService
{
    private readonly AppDbContext _db;
    private readonly BlaaizOptions _options;

    public BlaaizWebhookService(
        AppDbContext db,
        IOptions<BlaaizOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<BlaaizWebhookResult> ProcessCollectionWebhookAsync(
        string rawPayload,
        string? signature,
        string? timestamp,
        CancellationToken ct = default)
    {
        if (!_options.IsEnabled)
        {
            throw new InvalidOperationException("Blaaiz integration is disabled.");
        }

        ValidateSignature(rawPayload, signature, timestamp);

        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;

        var eventType = ReadRequiredString(root, "event_type", "event");
        var eventId = ReadRequiredString(root, "event_id");

        var existing = await _db.WebhookEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == ProviderCode.Blaaiz &&
                x.ProviderEventId == eventId,
                ct);

        if (existing is not null)
        {
            return new BlaaizWebhookResult(
                existing.Id,
                existing.EventType,
                existing.ProcessingStatus.ToString(),
                true);
        }

        var webhook = new WebhookEvent
        {
            ProviderCode = ProviderCode.Blaaiz,
            ProviderEventId = eventId,
            EventType = eventType,
            SignatureHeader = signature,
            TimestampHeader = timestamp,
            RawPayloadJson = rawPayload,
            ProcessingStatus = WebhookProcessingStatus.Processing,
            ReceivedAt = DateTime.UtcNow
        };

        var attempt = new WebhookProcessingAttempt
        {
            WebhookEvent = webhook,
            WebhookEventId = webhook.Id,
            Status = WebhookProcessingStatus.Processing,
            StartedAt = DateTime.UtcNow
        };

        webhook.Attempts.Add(attempt);
        _db.WebhookEvents.Add(webhook);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();

            var duplicate = await _db.WebhookEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.ProviderCode == ProviderCode.Blaaiz &&
                    x.ProviderEventId == eventId,
                    ct);

            if (duplicate is not null)
            {
                return new BlaaizWebhookResult(
                    duplicate.Id,
                    duplicate.EventType,
                    duplicate.ProcessingStatus.ToString(),
                    true);
            }

            throw;
        }

        try
        {
            if (string.Equals(eventType, "customer.status_changed", StringComparison.OrdinalIgnoreCase))
            {
                var processed = await ProcessCustomerStatusChangedAsync(root, ct);
                webhook.ProcessingStatus = processed
                    ? WebhookProcessingStatus.Processed
                    : WebhookProcessingStatus.Ignored;
            }
            else
            {
                webhook.ProcessingStatus = WebhookProcessingStatus.Ignored;
            }

            var now = DateTime.UtcNow;
            webhook.ProcessedAt = now;
            attempt.Status = webhook.ProcessingStatus;
            attempt.FinishedAt = now;

            await _db.SaveChangesAsync(ct);

            return new BlaaizWebhookResult(
                webhook.Id,
                webhook.EventType,
                webhook.ProcessingStatus.ToString(),
                false);
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            webhook.ProcessingStatus = WebhookProcessingStatus.Failed;
            webhook.ErrorMessage = Truncate(ex.Message, 2000);
            webhook.ProcessedAt = now;
            attempt.Status = WebhookProcessingStatus.Failed;
            attempt.ErrorMessage = Truncate(ex.Message, 2000);
            attempt.FinishedAt = now;

            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task<bool> ProcessCustomerStatusChangedAsync(
        JsonElement root,
        CancellationToken ct)
    {
        var providerCustomerId = ReadRequiredString(root, "customer_id");
        var providerStatus = ReadRequiredString(root, "new_status").ToUpperInvariant();
        var comment = ReadOptionalString(root, "comment");
        var updatedAt = ReadOptionalDateTime(root, "updated_at") ?? DateTime.UtcNow;

        var providerCustomer = await _db.ProviderCustomers
            .Include(x => x.CustomerProfile)
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == ProviderCode.Blaaiz &&
                x.ProviderCustomerId == providerCustomerId &&
                !x.IsDeleted,
                ct);

        if (providerCustomer?.CustomerProfileId is null || providerCustomer.CustomerProfile is null)
        {
            return false;
        }

        var kycProfile = await _db.KycProfiles
            .Include(x => x.Applications)
            .ThenInclude(x => x.Documents)
            .FirstOrDefaultAsync(x =>
                x.CustomerProfileId == providerCustomer.CustomerProfileId.Value &&
                !x.IsDeleted,
                ct);

        if (kycProfile is null)
        {
            return false;
        }

        var application = kycProfile.Applications
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        var status = MapProviderStatus(providerStatus);
        var now = DateTime.UtcNow;

        providerCustomer.ProviderStatus = providerStatus;
        providerCustomer.LastSyncedAt = now;
        providerCustomer.LastUpdatedAt = now;

        kycProfile.Status = status;
        kycProfile.RejectionReason = status == KycStatus.Rejected ? comment : null;
        kycProfile.LastUpdatedAt = now;

        providerCustomer.CustomerProfile.KycStatus = status;
        providerCustomer.CustomerProfile.KycApprovedAt = status == KycStatus.Approved
            ? updatedAt
            : null;
        providerCustomer.CustomerProfile.LastUpdatedAt = now;

        if (status == KycStatus.Approved)
        {
            kycProfile.ApprovedAt = updatedAt;
            kycProfile.RejectedAt = null;
        }
        else if (status == KycStatus.Rejected)
        {
            kycProfile.RejectedAt = updatedAt;
            kycProfile.ApprovedAt = null;
        }
        else
        {
            kycProfile.ApprovedAt = null;
            kycProfile.RejectedAt = null;
        }

        if (application is not null)
        {
            application.Status = status;
            application.ReviewedAt = status is KycStatus.Approved or KycStatus.Rejected
                ? updatedAt
                : null;
            application.ReviewNote = status == KycStatus.Rejected ? comment : null;
            application.LastUpdatedAt = now;

            if (status == KycStatus.Rejected)
            {
                foreach (var kycDocument in application.Documents.Where(x => !x.IsDeleted))
                {
                    kycDocument.IsAttachedToProvider = false;
                    kycDocument.RejectionReason = comment;
                    kycDocument.LastUpdatedAt = now;
                }
            }
            else if (status == KycStatus.Approved)
            {
                foreach (var kycDocument in application.Documents.Where(x => !x.IsDeleted))
                {
                    kycDocument.RejectionReason = null;
                    kycDocument.LastUpdatedAt = now;
                }
            }
        }

        return true;
    }

    private void ValidateSignature(
        string rawPayload,
        string? receivedSignature,
        string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSigningSecret) ||
            string.IsNullOrWhiteSpace(receivedSignature) ||
            string.IsNullOrWhiteSpace(timestamp))
        {
            throw new UnauthorizedAccessException("Missing Blaaiz webhook signature information.");
        }

        var webhookTime = ParseTimestamp(timestamp);
        var tolerance = TimeSpan.FromMinutes(_options.WebhookTimestampToleranceMinutes);

        if ((DateTimeOffset.UtcNow - webhookTime).Duration() > tolerance)
        {
            throw new UnauthorizedAccessException("Blaaiz webhook timestamp is outside the allowed tolerance.");
        }

        using var json = JsonDocument.Parse(rawPayload);
        var canonicalPayload = JsonSerializer.Serialize(json.RootElement);

        var actual = receivedSignature.Trim();
        if (actual.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            actual = actual[7..];
        }

        var isValid = SignatureMatches(
            $"{timestamp}.{canonicalPayload}",
            actual,
            _options.WebhookSigningSecret);

        if (!isValid && !string.Equals(canonicalPayload, rawPayload, StringComparison.Ordinal))
        {
            isValid = SignatureMatches(
                $"{timestamp}.{rawPayload}",
                actual,
                _options.WebhookSigningSecret);
        }

        if (!isValid)
        {
            throw new UnauthorizedAccessException("Invalid Blaaiz webhook signature.");
        }
    }

    private static bool SignatureMatches(
        string signedContent,
        string receivedSignature,
        string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(signedContent)))
            .ToLowerInvariant();

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(receivedSignature.ToLowerInvariant());

        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static DateTimeOffset ParseTimestamp(string timestamp)
    {
        if (long.TryParse(timestamp, out var numeric))
        {
            return numeric > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(numeric)
                : DateTimeOffset.FromUnixTimeSeconds(numeric);
        }

        if (DateTimeOffset.TryParse(
                timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        throw new UnauthorizedAccessException("Invalid Blaaiz webhook timestamp.");
    }

    private static string ReadRequiredString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!;
            }
        }

        throw new InvalidOperationException(
            $"Blaaiz webhook payload is missing '{string.Join("' or '", names)}'.");
    }

    private static string? ReadOptionalString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static DateTime? ReadOptionalDateTime(JsonElement root, string name)
    {
        var value = ReadOptionalString(root, name);
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static KycStatus MapProviderStatus(string providerStatus) => providerStatus switch
    {
        "VERIFIED" => KycStatus.Approved,
        "REJECTED" => KycStatus.Rejected,
        "PROCESSING" => KycStatus.UnderReview,
        "PENDING" => KycStatus.Pending,
        _ => throw new InvalidOperationException($"Unsupported Blaaiz customer status '{providerStatus}'.")
    };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
