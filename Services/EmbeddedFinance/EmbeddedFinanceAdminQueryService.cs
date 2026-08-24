using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinanceAdminQueryService : IEmbeddedFinanceAdminQueryService
{
    private readonly AppDbContext _db;

    public EmbeddedFinanceAdminQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<EmbeddedFinanceAdminOverviewDto> GetOverviewAsync(
        CancellationToken ct = default)
    {
        var embeddedBusinessIds = await _db.ApiApplications
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => x.BusinessProfileId)
            .Union(
                _db.BusinessCustomers
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.BusinessProfileId))
            .Distinct()
            .CountAsync(ct);

        return new EmbeddedFinanceAdminOverviewDto(
            embeddedBusinessIds,
            await _db.ApiApplications.AsNoTracking().CountAsync(x => !x.IsDeleted, ct),
            await _db.ApiApplications.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == ApiApplicationStatus.Active, ct),
            await _db.ApiCredentials.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == ApiCredentialStatus.Active, ct),
            await _db.BusinessCustomers.AsNoTracking().CountAsync(x => !x.IsDeleted, ct),
            await _db.BusinessCustomers.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == BusinessCustomerStatus.Active, ct),
            await _db.CollectionAccounts.AsNoTracking().CountAsync(x => !x.IsDeleted, ct),
            await _db.CollectionAccounts.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == CollectionAccountStatus.Active, ct),
            await _db.BusinessWebhookEndpoints.AsNoTracking().CountAsync(x => !x.IsDeleted, ct),
            await _db.BusinessWebhookEndpoints.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == BusinessWebhookEndpointStatus.Active, ct),
            await _db.BusinessWebhookDeliveries.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == BusinessWebhookDeliveryStatus.Pending, ct),
            await _db.BusinessWebhookDeliveries.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == BusinessWebhookDeliveryStatus.Retry, ct),
            await _db.BusinessWebhookDeliveries.AsNoTracking().CountAsync(
                x => !x.IsDeleted && x.Status == BusinessWebhookDeliveryStatus.DeadLetter, ct));
    }

    public async Task<PagedResult<EmbeddedFinanceAdminBusinessDto>> GetBusinessesAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var value = Clean(search);

        var query = _db.BusinessProfiles
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (_db.ApiApplications.Any(a =>
                    a.BusinessProfileId == x.Id && !a.IsDeleted) ||
                 _db.BusinessCustomers.Any(c =>
                    c.BusinessProfileId == x.Id && !c.IsDeleted)));

        if (value is not null)
        {
            query = query.Where(x =>
                x.BusinessName.Contains(value) ||
                x.CountryCode.Contains(value) ||
                (x.ContactEmail != null && x.ContactEmail.Contains(value)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderBy(x => x.BusinessName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EmbeddedFinanceAdminBusinessDto(
                x.Id,
                x.BusinessName,
                x.CountryCode,
                x.KybStatus,
                _db.ApiApplications.Count(a =>
                    a.BusinessProfileId == x.Id && !a.IsDeleted),
                _db.ApiApplications.Count(a =>
                    a.BusinessProfileId == x.Id &&
                    !a.IsDeleted &&
                    a.Status == ApiApplicationStatus.Active),
                _db.ApiCredentials.Count(c =>
                    !c.IsDeleted &&
                    c.Status == ApiCredentialStatus.Active &&
                    !c.ApiApplication.IsDeleted &&
                    c.ApiApplication.BusinessProfileId == x.Id),
                _db.BusinessCustomers.Count(c =>
                    c.BusinessProfileId == x.Id && !c.IsDeleted),
                _db.CollectionAccounts.Count(a =>
                    a.BusinessProfileId == x.Id && !a.IsDeleted),
                _db.BusinessWebhookEndpoints.Count(e =>
                    e.BusinessProfileId == x.Id && !e.IsDeleted),
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToListAsync(ct);

        return Page(rows, total, page, pageSize);
    }

    public async Task<PagedResult<EmbeddedFinanceAdminApplicationDto>> GetApplicationsAsync(
        Guid? businessProfileId,
        ApiApplicationStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var value = Clean(search);

        var query = _db.ApiApplications
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (businessProfileId.HasValue)
            query = query.Where(x => x.BusinessProfileId == businessProfileId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (value is not null)
        {
            query = query.Where(x =>
                x.Name.Contains(value) ||
                x.BusinessProfile.BusinessName.Contains(value) ||
                (x.Description != null && x.Description.Contains(value)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.BusinessProfileId,
                BusinessName = x.BusinessProfile.BusinessName,
                x.Name,
                x.Description,
                x.Scopes,
                x.Status,
                x.AllowedIpRanges,
                CredentialCount = x.Credentials.Count(c => !c.IsDeleted),
                ActiveCredentialCount = x.Credentials.Count(c =>
                    !c.IsDeleted && c.Status == ApiCredentialStatus.Active),
                x.LastAuthenticatedAt,
                x.CreatedAt,
                x.LastUpdatedAt
            })
            .ToListAsync(ct);

        return Page(
            rows.Select(x => new EmbeddedFinanceAdminApplicationDto(
                x.Id,
                x.BusinessProfileId,
                x.BusinessName,
                x.Name,
                x.Description,
                x.Scopes,
                x.Status,
                ParseCsv(x.AllowedIpRanges),
                x.CredentialCount,
                x.ActiveCredentialCount,
                x.LastAuthenticatedAt,
                x.CreatedAt,
                x.LastUpdatedAt)).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<IReadOnlyList<EmbeddedFinanceAdminCredentialDto>> GetCredentialsAsync(
        Guid apiApplicationId,
        CancellationToken ct = default)
    {
        var exists = await _db.ApiApplications
            .AsNoTracking()
            .AnyAsync(x => x.Id == apiApplicationId && !x.IsDeleted, ct);

        if (!exists)
            throw new InvalidOperationException("API application not found.");

        return await _db.ApiCredentials
            .AsNoTracking()
            .Where(x =>
                x.ApiApplicationId == apiApplicationId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new EmbeddedFinanceAdminCredentialDto(
                x.Id,
                x.ApiApplicationId,
                x.Name,
                x.KeyId,
                x.SecretLastFour,
                x.Status,
                x.ExpiresAt,
                x.LastUsedAt,
                x.LastUsedIpAddress,
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<EmbeddedFinanceAdminCustomerDto>> GetCustomersAsync(
        Guid? businessProfileId,
        BusinessCustomerStatus? status,
        string? countryCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var value = Clean(search);
        var country = Code(countryCode);

        var query = _db.BusinessCustomers
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (businessProfileId.HasValue)
            query = query.Where(x => x.BusinessProfileId == businessProfileId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (country is not null)
            query = query.Where(x => x.CountryCode == country);

        if (value is not null)
        {
            query = query.Where(x =>
                x.ExternalReference.Contains(value) ||
                x.DisplayName.Contains(value) ||
                x.BusinessProfile.BusinessName.Contains(value) ||
                (x.Email != null && x.Email.Contains(value)) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(value)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EmbeddedFinanceAdminCustomerDto(
                x.Id,
                x.BusinessProfileId,
                x.BusinessProfile.BusinessName,
                x.ExternalReference,
                x.DisplayName,
                x.Email,
                x.PhoneNumber,
                x.CountryCode,
                x.Status,
                x.CollectionAccounts.Count(a => !a.IsDeleted),
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToListAsync(ct);

        return Page(rows, total, page, pageSize);
    }

    public async Task<PagedResult<EmbeddedFinanceAdminCollectionAccountDto>> GetAccountsAsync(
        Guid? businessProfileId,
        Guid? businessCustomerId,
        CollectionAccountStatus? status,
        string? assetCode,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var value = Clean(search);
        var asset = Code(assetCode);

        var query = _db.CollectionAccounts
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (businessProfileId.HasValue)
            query = query.Where(x => x.BusinessProfileId == businessProfileId.Value);

        if (businessCustomerId.HasValue)
            query = query.Where(x => x.BusinessCustomerId == businessCustomerId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (asset is not null)
            query = query.Where(x => x.AssetCode == asset);

        if (value is not null)
        {
            query = query.Where(x =>
                x.ExternalReference.Contains(value) ||
                x.AssetCode.Contains(value) ||
                x.BusinessCustomer.DisplayName.Contains(value) ||
                x.BusinessProfile.BusinessName.Contains(value) ||
                x.FinancialAccount.AccountCode.Contains(value));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EmbeddedFinanceAdminCollectionAccountDto(
                x.Id,
                x.BusinessProfileId,
                x.BusinessProfile.BusinessName,
                x.BusinessCustomerId,
                x.BusinessCustomer.DisplayName,
                x.ExternalReference,
                x.AssetCode,
                x.FinancialAccountId,
                x.Status,
                x.FinancialAccount.SettledBalance,
                x.FinancialAccount.AvailableBalance,
                x.FinancialAccount.HeldBalance,
                x.ProviderMappings.Count(m => !m.IsDeleted),
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToListAsync(ct);

        return Page(rows, total, page, pageSize);
    }

    public async Task<PagedResult<EmbeddedFinanceAdminWebhookEndpointDto>> GetWebhookEndpointsAsync(
        Guid? businessProfileId,
        BusinessWebhookEndpointStatus? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var value = Clean(search);

        var query =
            from endpoint in _db.BusinessWebhookEndpoints.AsNoTracking()
            join business in _db.BusinessProfiles.AsNoTracking()
                on endpoint.BusinessProfileId equals business.Id
            join application in _db.ApiApplications.AsNoTracking()
                on endpoint.ApiApplicationId equals application.Id
            where
                !endpoint.IsDeleted &&
                !business.IsDeleted &&
                !application.IsDeleted
            select new
            {
                Endpoint = endpoint,
                BusinessName = business.BusinessName,
                ApiApplicationName = application.Name
            };

        if (businessProfileId.HasValue)
            query = query.Where(x => x.Endpoint.BusinessProfileId == businessProfileId.Value);

        if (status.HasValue)
            query = query.Where(x => x.Endpoint.Status == status.Value);

        if (value is not null)
        {
            query = query.Where(x =>
                x.Endpoint.Url.Contains(value) ||
                x.ApiApplicationName.Contains(value) ||
                x.BusinessName.Contains(value) ||
                x.Endpoint.EventTypesCsv.Contains(value));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(x => x.Endpoint.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Endpoint.Id,
                x.Endpoint.BusinessProfileId,
                x.BusinessName,
                x.Endpoint.ApiApplicationId,
                x.ApiApplicationName,
                x.Endpoint.Url,
                x.Endpoint.EventTypesCsv,
                x.Endpoint.Status,
                x.Endpoint.SigningSecretLastFour,
                x.Endpoint.MaxAttempts,
                PendingDeliveries = x.Endpoint.Deliveries.Count(d =>
                    !d.IsDeleted && d.Status == BusinessWebhookDeliveryStatus.Pending),
                RetryDeliveries = x.Endpoint.Deliveries.Count(d =>
                    !d.IsDeleted && d.Status == BusinessWebhookDeliveryStatus.Retry),
                DeadLetterDeliveries = x.Endpoint.Deliveries.Count(d =>
                    !d.IsDeleted && d.Status == BusinessWebhookDeliveryStatus.DeadLetter),
                x.Endpoint.CreatedAt,
                x.Endpoint.LastUpdatedAt
            })
            .ToListAsync(ct);

        return Page(
            rows.Select(x => new EmbeddedFinanceAdminWebhookEndpointDto(
                x.Id,
                x.BusinessProfileId,
                x.BusinessName,
                x.ApiApplicationId,
                x.ApiApplicationName,
                x.Url,
                ParseCsv(x.EventTypesCsv),
                x.Status,
                x.SigningSecretLastFour,
                x.MaxAttempts,
                x.PendingDeliveries,
                x.RetryDeliveries,
                x.DeadLetterDeliveries,
                x.CreatedAt,
                x.LastUpdatedAt)).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<PagedResult<EmbeddedFinanceAdminWebhookDeliveryDto>> GetWebhookDeliveriesAsync(
        Guid? businessProfileId,
        BusinessWebhookDeliveryStatus? status,
        string? eventType,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);
        var value = Clean(search);
        var type = Clean(eventType);

        var query =
            from delivery in _db.BusinessWebhookDeliveries.AsNoTracking()
            join endpoint in _db.BusinessWebhookEndpoints.AsNoTracking()
                on delivery.BusinessWebhookEndpointId equals endpoint.Id
            join business in _db.BusinessProfiles.AsNoTracking()
                on endpoint.BusinessProfileId equals business.Id
            join webhookEvent in _db.BusinessWebhookEvents.AsNoTracking()
                on delivery.BusinessWebhookEventId equals webhookEvent.Id
            where
                !delivery.IsDeleted &&
                !endpoint.IsDeleted &&
                !business.IsDeleted
            select new
            {
                Delivery = delivery,
                Endpoint = endpoint,
                BusinessName = business.BusinessName,
                Event = webhookEvent
            };

        if (businessProfileId.HasValue)
        {
            query = query.Where(x =>
                x.Endpoint.BusinessProfileId == businessProfileId.Value);
        }

        if (status.HasValue)
            query = query.Where(x => x.Delivery.Status == status.Value);

        if (type is not null)
            query = query.Where(x => x.Event.EventType == type);

        if (value is not null)
        {
            query = query.Where(x =>
                x.Event.EventId.Contains(value) ||
                x.Event.EventType.Contains(value) ||
                x.Endpoint.Url.Contains(value) ||
                x.BusinessName.Contains(value) ||
                (x.Delivery.ErrorMessage != null &&
                 x.Delivery.ErrorMessage.Contains(value)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(x => x.Delivery.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new EmbeddedFinanceAdminWebhookDeliveryDto(
                x.Delivery.Id,
                x.Endpoint.BusinessProfileId,
                x.BusinessName,
                x.Endpoint.Id,
                x.Endpoint.Url,
                x.Event.EventId,
                x.Event.EventType,
                x.Delivery.Status,
                x.Delivery.AttemptCount,
                x.Delivery.NextAttemptAt,
                x.Delivery.LastAttemptAt,
                x.Delivery.DeliveredAt,
                x.Delivery.DeadLetteredAt,
                x.Delivery.LastResponseStatusCode,
                x.Delivery.ErrorMessage,
                x.Delivery.CreatedAt))
            .ToListAsync(ct);

        return Page(rows, total, page, pageSize);
    }

    private static (int Page, int PageSize) NormalizePage(
        int page,
        int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    private static PagedResult<T> Page<T>(
        IReadOnlyList<T> items,
        int total,
        int page,
        int pageSize) =>
        new()
        {
            Items = items.ToList(),
            Meta = new PageMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = total
            }
        };

    private static IReadOnlyList<string> ParseCsv(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Code(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
}
