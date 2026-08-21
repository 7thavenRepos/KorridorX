using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedWebhookManagementService : IEmbeddedWebhookManagementService
{
    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _access;
    private readonly IDataProtector _protector;
    private readonly IEmbeddedWebhookUrlSecurityValidator _urlSecurity;

    public EmbeddedWebhookManagementService(
        AppDbContext db,
        IBusinessAccessService access,
        IDataProtectionProvider dataProtectionProvider,
        IEmbeddedWebhookUrlSecurityValidator urlSecurity)
    {
        _db = db;
        _access = access;
        _protector = dataProtectionProvider.CreateProtector("KorridorX.EmbeddedFinance.WebhookSigningSecret.v1");
        _urlSecurity = urlSecurity;
    }

    public async Task<IReadOnlyList<BusinessWebhookEndpointDto>> GetEndpointsAsync(Guid userId, CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        var rows = await _db.BusinessWebhookEndpoints.AsNoTracking()
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted)
            .OrderBy(x => x.Url).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<BusinessWebhookEndpointCreatedDto> CreateEndpointAsync(
        Guid userId,
        CreateBusinessWebhookEndpointRequestDto request,
        CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        var app = await _db.ApiApplications.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == request.ApiApplicationId &&
            x.BusinessProfileId == access.BusinessProfileId &&
            x.Status == ApiApplicationStatus.Active &&
            !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Active API application not found.");

        if (!app.Scopes.HasFlag(EmbeddedFinanceScope.WebhooksManage))
            throw new InvalidOperationException("API application must include the WebhooksManage scope.");

        var url = await _urlSecurity.ValidateAsync(request.Url, ct);
        var eventTypes = NormalizeEventTypes(request.EventTypes);
        var maxAttempts = Math.Clamp(request.MaxAttempts, 1, 20);

        if (await _db.BusinessWebhookEndpoints.AnyAsync(x =>
            x.BusinessProfileId == access.BusinessProfileId && x.Url == url && !x.IsDeleted, ct))
            throw new InvalidOperationException("This webhook URL is already configured.");

        var secret = EmbeddedWebhookSecret.Generate();
        var entity = new BusinessWebhookEndpoint
        {
            BusinessProfileId = access.BusinessProfileId,
            ApiApplicationId = app.Id,
            Url = url,
            EventTypesCsv = string.Join(',', eventTypes),
            SigningSecretProtected = _protector.Protect(secret),
            SigningSecretLastFour = secret[^4..],
            Status = BusinessWebhookEndpointStatus.Active,
            MaxAttempts = maxAttempts,
            CreatedByUserId = userId
        };

        _db.BusinessWebhookEndpoints.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new(entity.Id, entity.ApiApplicationId, entity.Url, eventTypes, entity.Status,
            secret, entity.SigningSecretLastFour, entity.MaxAttempts, entity.CreatedAt);
    }

    public async Task<RotateBusinessWebhookSecretDto> RotateSecretAsync(Guid userId, Guid endpointId, CancellationToken ct = default)
    {
        var endpoint = await GetOwnedEndpointAsync(userId, endpointId, ct);
        var secret = EmbeddedWebhookSecret.Generate();
        endpoint.SigningSecretProtected = _protector.Protect(secret);
        endpoint.SigningSecretLastFour = secret[^4..];
        endpoint.LastUpdatedAt = DateTime.UtcNow;
        endpoint.LastUpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return new(endpoint.Id, secret, endpoint.SigningSecretLastFour);
    }

    public async Task SetStatusAsync(Guid userId, Guid endpointId, bool enabled, CancellationToken ct = default)
    {
        var endpoint = await GetOwnedEndpointAsync(userId, endpointId, ct);
        endpoint.Status = enabled ? BusinessWebhookEndpointStatus.Active : BusinessWebhookEndpointStatus.Disabled;
        endpoint.LastUpdatedAt = DateTime.UtcNow;
        endpoint.LastUpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BusinessWebhookDeliveryDto>> GetDeliveriesAsync(
        Guid userId, BusinessWebhookDeliveryStatus? status, int take = 100, CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        take = Math.Clamp(take, 1, 200);
        var query = _db.BusinessWebhookDeliveries.AsNoTracking()
            .Include(x => x.BusinessWebhookEndpoint).Include(x => x.BusinessWebhookEvent)
            .Where(x => x.BusinessWebhookEndpoint.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted && !x.BusinessWebhookEndpoint.IsDeleted);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        return await query.OrderByDescending(x => x.CreatedAt).Take(take)
            .Select(x => new BusinessWebhookDeliveryDto(x.Id, x.BusinessWebhookEndpointId, x.BusinessWebhookEventId,
                x.BusinessWebhookEvent.EventId, x.BusinessWebhookEvent.EventType, x.Status, x.AttemptCount,
                x.NextAttemptAt, x.LastAttemptAt, x.DeliveredAt, x.DeadLetteredAt,
                x.LastResponseStatusCode, x.ErrorMessage, x.CreatedAt)).ToListAsync(ct);
    }

    public async Task RetryDeliveryAsync(Guid userId, Guid deliveryId, CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        var delivery = await _db.BusinessWebhookDeliveries.Include(x => x.BusinessWebhookEndpoint)
            .FirstOrDefaultAsync(x => x.Id == deliveryId && x.BusinessWebhookEndpoint.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Webhook delivery not found.");
        if (delivery.Status == BusinessWebhookDeliveryStatus.Delivered)
            throw new InvalidOperationException("Delivered webhook deliveries cannot be retried.");
        delivery.Status = BusinessWebhookDeliveryStatus.Pending;
        delivery.AttemptCount = 0;
        delivery.NextAttemptAt = DateTime.UtcNow;
        delivery.LockId = null; delivery.LockedAt = null; delivery.DeadLetteredAt = null;
        delivery.ErrorMessage = null; delivery.LastUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<BusinessWebhookEndpoint> GetOwnedEndpointAsync(Guid userId, Guid endpointId, CancellationToken ct)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        return await _db.BusinessWebhookEndpoints.FirstOrDefaultAsync(x =>
            x.Id == endpointId && x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Webhook endpoint not found.");
    }

    private static string ValidateUrl(string value)
    {
        var cleaned = (value ?? "").Trim();
        if (cleaned.Length > 1000) throw new InvalidOperationException("Webhook URL cannot exceed 1000 characters.");
        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Webhook URL must be an absolute HTTPS URL.");
        return uri.ToString();
    }

    private static IReadOnlyList<string> NormalizeEventTypes(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) throw new InvalidOperationException("At least one webhook event type is required.");
        var result = values.Select(x => (x ?? "").Trim().ToLowerInvariant())
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (result.Count == 0 || result.Any(x => x.Length > 150))
            throw new InvalidOperationException("Webhook event type is invalid.");
        return result;
    }

    private static BusinessWebhookEndpointDto ToDto(BusinessWebhookEndpoint x) => new(
        x.Id, x.ApiApplicationId, x.Url,
        x.EventTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        x.Status, x.SigningSecretLastFour, x.MaxAttempts, x.CreatedAt, x.LastUpdatedAt);
}
