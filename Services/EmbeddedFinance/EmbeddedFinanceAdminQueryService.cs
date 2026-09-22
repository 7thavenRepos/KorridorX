using System.Text;
using System.Text.Json;
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
        var now = DateTime.UtcNow;

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
                x =>
                    !x.IsDeleted &&
                    x.Status == ApiCredentialStatus.Active &&
                    (!x.ExpiresAt.HasValue || x.ExpiresAt.Value > now),
                ct),
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
        var now = DateTime.UtcNow;

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
                    (!c.ExpiresAt.HasValue || c.ExpiresAt.Value > now) &&
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
        var now = DateTime.UtcNow;

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
                    !c.IsDeleted &&
                    c.Status == ApiCredentialStatus.Active &&
                    (!c.ExpiresAt.HasValue || c.ExpiresAt.Value > now)),
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

        var now = DateTime.UtcNow;

        var rows = await _db.ApiCredentials
            .AsNoTracking()
            .Where(x =>
                x.ApiApplicationId == apiApplicationId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
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
                x.LastUpdatedAt
            })
            .ToListAsync(ct);

        return rows
            .Select(x => new EmbeddedFinanceAdminCredentialDto(
                x.Id,
                x.ApiApplicationId,
                x.Name,
                x.KeyId,
                x.SecretLastFour,
                EffectiveCredentialStatus(
                    x.Status,
                    x.ExpiresAt,
                    now),
                x.ExpiresAt,
                x.LastUsedAt,
                x.LastUsedIpAddress,
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToList();
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
                x.FinancialAccount.Status,
                x.FinancialAccount.SettledBalance,
                x.FinancialAccount.AvailableBalance,
                x.FinancialAccount.HeldBalance,
                x.ProviderMappings.Count(m => !m.IsDeleted),
                x.CreatedAt,
                x.LastUpdatedAt))
            .ToListAsync(ct);

        return Page(rows, total, page, pageSize);
    }


    public async Task<IReadOnlyList<EmbeddedFinanceAdminProviderMappingDto>> GetProviderMappingsAsync(
        Guid collectionAccountId,
        CancellationToken ct = default)
    {
        var exists = await _db.CollectionAccounts
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == collectionAccountId && !x.IsDeleted,
                ct);

        if (!exists)
            throw new InvalidOperationException(
                "Collection account not found.");

        return await _db.ProviderAccountMappings
            .AsNoTracking()
            .Where(x =>
                x.CollectionAccountId == collectionAccountId &&
                !x.IsDeleted)
            .OrderBy(x => x.ProviderCode)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new EmbeddedFinanceAdminProviderMappingDto(
                x.Id,
                x.CollectionAccountId,
                x.ProviderCode,
                x.ProviderCustomerId,
                x.ProviderAccountId,
                x.ProviderReference,
                x.AccountNumber,
                x.AccountName,
                x.BankName,
                x.Status,
                x.FailureReason,
                x.CreatedAt,
                x.LastUpdatedAt,
                BlaaizVirtualAccountState.BankDetails(x.MetadataJson)))
            .ToListAsync(ct);
    }


    public async Task<PagedResult<EmbeddedFinanceAdminActivityDto>> GetActivityAsync(
        Guid? businessProfileId,
        Guid? businessCustomerId,
        Guid? collectionAccountId,
        string? activityType,
        string? assetCode,
        string? providerCode,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken ct = default,
        Guid? activityId = null)
    {
        (page, pageSize) = NormalizePage(page, pageSize);

        var requestedType = Clean(activityType);
        var asset = Code(assetCode);
        var provider = Clean(providerCode);
        var value = Clean(search);

        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Collection",
            "Payout",
            "Transfer",
            "DigitalAssetDeposit",
            "DigitalAssetWithdrawal",
            "MarketplaceTradeReservation",
            "InstantTradeReservation"
        };

        if (requestedType is not null && !supportedTypes.Contains(requestedType))
            throw new InvalidOperationException("Unsupported Embedded Finance activity type.");

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
            throw new InvalidOperationException("Activity start date cannot be after the end date.");

        var take = (int)Math.Min((long)page * pageSize, int.MaxValue);
        var total = 0;
        var rows = new List<ActivitySourceRow>();

        bool Include(string candidate) =>
            requestedType is null ||
            string.Equals(requestedType, candidate, StringComparison.OrdinalIgnoreCase);

        if (Include("Collection"))
        {
            var source =
                from collection in _db.Collections.AsNoTracking()
                join customer in _db.BusinessCustomers.AsNoTracking()
                    on collection.ContextEntityId equals (Guid?)customer.Id
                join business in _db.BusinessProfiles.AsNoTracking()
                    on customer.BusinessProfileId equals business.Id
                join accountSource in _db.CollectionAccounts.AsNoTracking().Where(x => !x.IsDeleted)
                    on collection.RelatedEntityId equals (Guid?)accountSource.Id into accountGroup
                from account in accountGroup.DefaultIfEmpty()
                where !collection.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
                      collection.Purpose == PaymentOperationPurpose.AccountFunding &&
                      collection.ContextEntityType == "BusinessCustomer"
                select new { Collection = collection, Customer = customer, Business = business, Account = account };

            if (activityId.HasValue) source = source.Where(x => x.Collection.Id == activityId.Value);
            if (businessProfileId.HasValue) source = source.Where(x => x.Customer.BusinessProfileId == businessProfileId.Value);
            if (businessCustomerId.HasValue) source = source.Where(x => x.Customer.Id == businessCustomerId.Value);
            if (collectionAccountId.HasValue) source = source.Where(x => x.Account != null && x.Account.Id == collectionAccountId.Value);
            if (asset is not null) source = source.Where(x => x.Collection.CurrencyCode == asset);
            if (provider is not null) source = source.Where(x => x.Collection.ProviderCode.Contains(provider));
            if (fromUtc.HasValue) source = source.Where(x => x.Collection.CreatedAt >= fromUtc.Value);
            if (toUtc.HasValue) source = source.Where(x => x.Collection.CreatedAt <= toUtc.Value);
            if (value is not null)
            {
                source = source.Where(x =>
                    x.Collection.Reference.Contains(value) ||
                    (x.Collection.ProviderCollectionId != null && x.Collection.ProviderCollectionId.Contains(value)) ||
                    (x.Collection.ProviderReference != null && x.Collection.ProviderReference.Contains(value)) ||
                    x.Customer.DisplayName.Contains(value) ||
                    x.Business.BusinessName.Contains(value) ||
                    (x.Account != null && x.Account.ExternalReference.Contains(value)));
            }

            total += await source.CountAsync(ct);
            rows.AddRange(await source
                .OrderByDescending(x => x.Collection.CreatedAt)
                .Take(take)
                .Select(x => new ActivitySourceRow(
                    x.Collection.Id, "Collection", "Inbound",
                    x.Customer.BusinessProfileId, x.Business.BusinessName,
                    x.Customer.Id, x.Customer.DisplayName,
                    x.Account == null ? null : (Guid?)x.Account.Id,
                    x.Account == null ? null : x.Account.ExternalReference,
                    x.Collection.FinancialAccountId,
                    x.Collection.Reference,
                    x.Collection.ProviderCollectionId,
                    x.Collection.CurrencyCode,
                    null,
                    x.Collection.Amount,
                    null,
                    null,
                    (int)x.Collection.Status,
                    x.Collection.ProviderCode,
                    x.Collection.ProviderReference,
                    _db.ProviderRequestLogs
                        .Where(log => log.RelatedCollectionId == x.Collection.Id)
                        .OrderByDescending(log => log.RequestedAt)
                        .Select(log => (Guid?)log.Id)
                        .FirstOrDefault(),
                    x.Collection.RelatedEntityType,
                    x.Collection.RelatedEntityId,
                    x.Collection.CreatedAt,
                    x.Collection.ConfirmedAt,
                    null))
                .ToListAsync(ct));
        }

        if (Include("Payout"))
        {
            var source =
                from payout in _db.Payouts.AsNoTracking()
                join customer in _db.BusinessCustomers.AsNoTracking()
                    on payout.ContextEntityId equals (Guid?)customer.Id
                join business in _db.BusinessProfiles.AsNoTracking()
                    on customer.BusinessProfileId equals business.Id
                join accountSource in _db.CollectionAccounts.AsNoTracking().Where(x => !x.IsDeleted)
                    on payout.FinancialAccountId equals (Guid?)accountSource.FinancialAccountId into accountGroup
                from account in accountGroup.DefaultIfEmpty()
                where !payout.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
                      payout.Purpose == PaymentOperationPurpose.Withdrawal &&
                      payout.ContextEntityType == "BusinessCustomer"
                select new { Payout = payout, Customer = customer, Business = business, Account = account };

            if (activityId.HasValue) source = source.Where(x => x.Payout.Id == activityId.Value);
            if (businessProfileId.HasValue) source = source.Where(x => x.Customer.BusinessProfileId == businessProfileId.Value);
            if (businessCustomerId.HasValue) source = source.Where(x => x.Customer.Id == businessCustomerId.Value);
            if (collectionAccountId.HasValue) source = source.Where(x => x.Account != null && x.Account.Id == collectionAccountId.Value);
            if (asset is not null) source = source.Where(x => x.Payout.CurrencyCode == asset);
            if (provider is not null) source = source.Where(x => x.Payout.ProviderCode.Contains(provider));
            if (fromUtc.HasValue) source = source.Where(x => x.Payout.CreatedAt >= fromUtc.Value);
            if (toUtc.HasValue) source = source.Where(x => x.Payout.CreatedAt <= toUtc.Value);
            if (value is not null)
            {
                source = source.Where(x =>
                    x.Payout.Reference.Contains(value) ||
                    (x.Payout.ProviderPayoutId != null && x.Payout.ProviderPayoutId.Contains(value)) ||
                    (x.Payout.ProviderReference != null && x.Payout.ProviderReference.Contains(value)) ||
                    x.Customer.DisplayName.Contains(value) ||
                    x.Business.BusinessName.Contains(value) ||
                    (x.Account != null && x.Account.ExternalReference.Contains(value)));
            }

            total += await source.CountAsync(ct);
            rows.AddRange(await source
                .OrderByDescending(x => x.Payout.CreatedAt)
                .Take(take)
                .Select(x => new ActivitySourceRow(
                    x.Payout.Id, "Payout", "Outbound",
                    x.Customer.BusinessProfileId, x.Business.BusinessName,
                    x.Customer.Id, x.Customer.DisplayName,
                    x.Account == null ? null : (Guid?)x.Account.Id,
                    x.Account == null ? null : x.Account.ExternalReference,
                    x.Payout.FinancialAccountId,
                    x.Payout.Reference,
                    null,
                    x.Payout.CurrencyCode,
                    null,
                    x.Payout.Amount,
                    null,
                    null,
                    (int)x.Payout.Status,
                    x.Payout.ProviderCode,
                    x.Payout.ProviderReference,
                    _db.ProviderRequestLogs
                        .Where(log => log.RelatedPayoutId == x.Payout.Id)
                        .OrderByDescending(log => log.RequestedAt)
                        .Select(log => (Guid?)log.Id)
                        .FirstOrDefault(),
                    x.Payout.RelatedEntityType,
                    x.Payout.RelatedEntityId,
                    x.Payout.CreatedAt,
                    x.Payout.CompletedAt ?? x.Payout.FailedAt ?? x.Payout.ReversedAt,
                    x.Payout.FailureReason))
                .ToListAsync(ct));
        }

        if (Include("Transfer"))
        {
            var source =
                from transfer in _db.Transfers.AsNoTracking()
                join customer in _db.BusinessCustomers.AsNoTracking()
                    on transfer.BusinessCustomerId equals (Guid?)customer.Id
                join business in _db.BusinessProfiles.AsNoTracking()
                    on customer.BusinessProfileId equals business.Id
                join accountSource in _db.CollectionAccounts.AsNoTracking().Where(x => !x.IsDeleted)
                    on transfer.SourceFinancialAccountId equals (Guid?)accountSource.FinancialAccountId into accountGroup
                from account in accountGroup.DefaultIfEmpty()
                where !transfer.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
                      transfer.SourceFinancialAccountId != null
                select new { Transfer = transfer, Customer = customer, Business = business, Account = account };

            if (activityId.HasValue) source = source.Where(x => x.Transfer.Id == activityId.Value);
            if (businessProfileId.HasValue) source = source.Where(x => x.Customer.BusinessProfileId == businessProfileId.Value);
            if (businessCustomerId.HasValue) source = source.Where(x => x.Customer.Id == businessCustomerId.Value);
            if (collectionAccountId.HasValue) source = source.Where(x => x.Account != null && x.Account.Id == collectionAccountId.Value);
            if (asset is not null) source = source.Where(x => x.Transfer.SourceCurrencyCode == asset || x.Transfer.DestinationCurrencyCode == asset);
            if (provider is not null) source = source.Where(x => x.Transfer.ProviderCode.Contains(provider));
            if (fromUtc.HasValue) source = source.Where(x => x.Transfer.CreatedAt >= fromUtc.Value);
            if (toUtc.HasValue) source = source.Where(x => x.Transfer.CreatedAt <= toUtc.Value);
            if (value is not null)
            {
                source = source.Where(x =>
                    x.Transfer.Reference.Contains(value) ||
                    (x.Transfer.ExternalReference != null && x.Transfer.ExternalReference.Contains(value)) ||
                    (x.Transfer.ProviderTransferId != null && x.Transfer.ProviderTransferId.Contains(value)) ||
                    (x.Transfer.ProviderReference != null && x.Transfer.ProviderReference.Contains(value)) ||
                    x.Customer.DisplayName.Contains(value) ||
                    x.Business.BusinessName.Contains(value) ||
                    (x.Account != null && x.Account.ExternalReference.Contains(value)));
            }

            total += await source.CountAsync(ct);
            rows.AddRange(await source
                .OrderByDescending(x => x.Transfer.CreatedAt)
                .Take(take)
                .Select(x => new ActivitySourceRow(
                    x.Transfer.Id, "Transfer", "Outbound",
                    x.Customer.BusinessProfileId, x.Business.BusinessName,
                    x.Customer.Id, x.Customer.DisplayName,
                    x.Account == null ? null : (Guid?)x.Account.Id,
                    x.Account == null ? null : x.Account.ExternalReference,
                    x.Transfer.SourceFinancialAccountId,
                    x.Transfer.Reference,
                    x.Transfer.ExternalReference,
                    x.Transfer.SourceCurrencyCode,
                    x.Transfer.DestinationCurrencyCode,
                    x.Transfer.SourceAmount,
                    x.Transfer.DestinationAmount,
                    x.Transfer.FeeAmount,
                    (int)x.Transfer.Status,
                    x.Transfer.ProviderCode,
                    x.Transfer.ProviderReference,
                    _db.ProviderRequestLogs
                        .Where(log => log.RelatedTransferId == x.Transfer.Id)
                        .OrderByDescending(log => log.RequestedAt)
                        .Select(log => (Guid?)log.Id)
                        .FirstOrDefault(),
                    "BusinessBeneficiary",
                    x.Transfer.BusinessBeneficiaryId,
                    x.Transfer.CreatedAt,
                    x.Transfer.CompletedAt ?? x.Transfer.FailedAt,
                    x.Transfer.FailureReason))
                .ToListAsync(ct));
        }

        if (Include("DigitalAssetDeposit"))
        {
            var source =
                from intent in _db.DigitalAssetDepositIntents.AsNoTracking()
                join customer in _db.BusinessCustomers.AsNoTracking()
                    on intent.BusinessCustomerId equals customer.Id
                join business in _db.BusinessProfiles.AsNoTracking()
                    on customer.BusinessProfileId equals business.Id
                join accountSource in _db.CollectionAccounts.AsNoTracking().Where(x => !x.IsDeleted)
                    on intent.FinancialAccountId equals accountSource.FinancialAccountId into accountGroup
                from account in accountGroup.DefaultIfEmpty()
                where !intent.IsDeleted && !customer.IsDeleted && !business.IsDeleted
                select new { Intent = intent, Customer = customer, Business = business, Account = account };

            if (activityId.HasValue) source = source.Where(x => x.Intent.Id == activityId.Value);
            if (businessProfileId.HasValue) source = source.Where(x => x.Intent.BusinessProfileId == businessProfileId.Value);
            if (businessCustomerId.HasValue) source = source.Where(x => x.Intent.BusinessCustomerId == businessCustomerId.Value);
            if (collectionAccountId.HasValue) source = source.Where(x => x.Account != null && x.Account.Id == collectionAccountId.Value);
            if (asset is not null) source = source.Where(x => x.Intent.AssetCode == asset);
            if (provider is not null) source = source.Where(x => x.Intent.ProviderCode.Contains(provider));
            if (fromUtc.HasValue) source = source.Where(x => x.Intent.CreatedAt >= fromUtc.Value);
            if (toUtc.HasValue) source = source.Where(x => x.Intent.CreatedAt <= toUtc.Value);
            if (value is not null)
            {
                source = source.Where(x =>
                    (x.Intent.ProviderCollectionId != null && x.Intent.ProviderCollectionId.Contains(value)) ||
                    (x.Intent.ProviderReference != null && x.Intent.ProviderReference.Contains(value)) ||
                    x.Customer.DisplayName.Contains(value) ||
                    x.Business.BusinessName.Contains(value) ||
                    (x.Account != null && x.Account.ExternalReference.Contains(value)));
            }

            total += await source.CountAsync(ct);
            rows.AddRange(await source
                .OrderByDescending(x => x.Intent.CreatedAt)
                .Take(take)
                .Select(x => new ActivitySourceRow(
                    x.Intent.Id, "DigitalAssetDeposit", "Inbound",
                    x.Intent.BusinessProfileId, x.Business.BusinessName,
                    x.Intent.BusinessCustomerId.GetValueOrDefault(), x.Customer.DisplayName,
                    x.Account == null ? null : (Guid?)x.Account.Id,
                    x.Account == null ? null : x.Account.ExternalReference,
                    x.Intent.FinancialAccountId,
                    x.Intent.ProviderCollectionId,
                    null,
                    x.Intent.AssetCode,
                    null,
                    x.Intent.Amount,
                    null,
                    null,
                    (int)x.Intent.Status,
                    x.Intent.ProviderCode,
                    x.Intent.ProviderReference,
                    x.Intent.CollectionId == null
                        ? null
                        : _db.ProviderRequestLogs
                            .Where(log => log.RelatedCollectionId == x.Intent.CollectionId)
                            .OrderByDescending(log => log.RequestedAt)
                            .Select(log => (Guid?)log.Id)
                            .FirstOrDefault(),
                    x.Intent.CollectionId == null ? null : "Collection",
                    x.Intent.CollectionId,
                    x.Intent.CreatedAt,
                    x.Intent.CompletedAt ?? x.Intent.FailedAt,
                    x.Intent.FailureReason))
                .ToListAsync(ct));
        }

        if (Include("DigitalAssetWithdrawal"))
        {
            var source =
                from withdrawal in _db.DigitalAssetWithdrawals.AsNoTracking()
                join customer in _db.BusinessCustomers.AsNoTracking()
                    on withdrawal.BusinessCustomerId equals customer.Id
                join business in _db.BusinessProfiles.AsNoTracking()
                    on customer.BusinessProfileId equals business.Id
                join payout in _db.Payouts.AsNoTracking()
                    on withdrawal.PayoutId equals payout.Id
                join accountSource in _db.CollectionAccounts.AsNoTracking().Where(x => !x.IsDeleted)
                    on withdrawal.FinancialAccountId equals accountSource.FinancialAccountId into accountGroup
                from account in accountGroup.DefaultIfEmpty()
                where !withdrawal.IsDeleted && !payout.IsDeleted && !customer.IsDeleted && !business.IsDeleted
                select new { Withdrawal = withdrawal, Payout = payout, Customer = customer, Business = business, Account = account };

            if (activityId.HasValue) source = source.Where(x => x.Withdrawal.Id == activityId.Value);
            if (businessProfileId.HasValue) source = source.Where(x => x.Withdrawal.BusinessProfileId == businessProfileId.Value);
            if (businessCustomerId.HasValue) source = source.Where(x => x.Withdrawal.BusinessCustomerId == businessCustomerId.Value);
            if (collectionAccountId.HasValue) source = source.Where(x => x.Account != null && x.Account.Id == collectionAccountId.Value);
            if (asset is not null) source = source.Where(x => x.Withdrawal.AssetCode == asset);
            if (provider is not null) source = source.Where(x => x.Withdrawal.ProviderCode.Contains(provider));
            if (fromUtc.HasValue) source = source.Where(x => x.Withdrawal.CreatedAt >= fromUtc.Value);
            if (toUtc.HasValue) source = source.Where(x => x.Withdrawal.CreatedAt <= toUtc.Value);
            if (value is not null)
            {
                source = source.Where(x =>
                    x.Payout.Reference.Contains(value) ||
                    (x.Payout.ProviderReference != null && x.Payout.ProviderReference.Contains(value)) ||
                    x.Customer.DisplayName.Contains(value) ||
                    x.Business.BusinessName.Contains(value) ||
                    (x.Account != null && x.Account.ExternalReference.Contains(value)));
            }

            total += await source.CountAsync(ct);
            rows.AddRange(await source
                .OrderByDescending(x => x.Withdrawal.CreatedAt)
                .Take(take)
                .Select(x => new ActivitySourceRow(
                    x.Withdrawal.Id, "DigitalAssetWithdrawal", "Outbound",
                    x.Withdrawal.BusinessProfileId, x.Business.BusinessName,
                    x.Withdrawal.BusinessCustomerId.GetValueOrDefault(), x.Customer.DisplayName,
                    x.Account == null ? null : (Guid?)x.Account.Id,
                    x.Account == null ? null : x.Account.ExternalReference,
                    x.Withdrawal.FinancialAccountId,
                    x.Payout.Reference,
                    null,
                    x.Withdrawal.AssetCode,
                    null,
                    x.Withdrawal.Amount,
                    null,
                    x.Withdrawal.NetworkFee,
                    (int)x.Withdrawal.Status,
                    x.Withdrawal.ProviderCode,
                    x.Payout.ProviderReference,
                    _db.ProviderRequestLogs
                        .Where(log => log.RelatedPayoutId == x.Withdrawal.PayoutId)
                        .OrderByDescending(log => log.RequestedAt)
                        .Select(log => (Guid?)log.Id)
                        .FirstOrDefault(),
                    "DigitalAssetWithdrawalDestination",
                    x.Withdrawal.DestinationId,
                    x.Withdrawal.CreatedAt,
                    x.Withdrawal.CompletedAt ?? x.Withdrawal.FailedAt,
                    x.Withdrawal.FailureReason))
                .ToListAsync(ct));
        }

        if ((Include("MarketplaceTradeReservation") || Include("InstantTradeReservation")) && provider is null)
        {
            var source =
                from reservation in _db.FinancialReservations.AsNoTracking()
                join financialAccount in _db.FinancialAccounts.AsNoTracking()
                    on reservation.FinancialAccountId equals financialAccount.Id
                join customer in _db.BusinessCustomers.AsNoTracking()
                    on financialAccount.OwnerId equals customer.Id
                join business in _db.BusinessProfiles.AsNoTracking()
                    on customer.BusinessProfileId equals business.Id
                join accountSource in _db.CollectionAccounts.AsNoTracking().Where(x => !x.IsDeleted)
                    on financialAccount.Id equals accountSource.FinancialAccountId into accountGroup
                from account in accountGroup.DefaultIfEmpty()
                where !reservation.IsDeleted && !financialAccount.IsDeleted && !customer.IsDeleted && !business.IsDeleted &&
                      financialAccount.OwnerType == FinancialAccountOwnerType.BusinessCustomer &&
                      (reservation.Type == FinancialReservationType.MarketplaceTrade ||
                       reservation.Type == FinancialReservationType.InstantTrade)
                select new { Reservation = reservation, FinancialAccount = financialAccount, Customer = customer, Business = business, Account = account };

            if (requestedType is not null)
            {
                if (requestedType.Equals("MarketplaceTradeReservation", StringComparison.OrdinalIgnoreCase))
                    source = source.Where(x => x.Reservation.Type == FinancialReservationType.MarketplaceTrade);
                else if (requestedType.Equals("InstantTradeReservation", StringComparison.OrdinalIgnoreCase))
                    source = source.Where(x => x.Reservation.Type == FinancialReservationType.InstantTrade);
            }

            if (activityId.HasValue) source = source.Where(x => x.Reservation.Id == activityId.Value);
            if (businessProfileId.HasValue) source = source.Where(x => x.Customer.BusinessProfileId == businessProfileId.Value);
            if (businessCustomerId.HasValue) source = source.Where(x => x.Customer.Id == businessCustomerId.Value);
            if (collectionAccountId.HasValue) source = source.Where(x => x.Account != null && x.Account.Id == collectionAccountId.Value);
            if (asset is not null) source = source.Where(x => x.FinancialAccount.AssetCode == asset);
            if (fromUtc.HasValue) source = source.Where(x => x.Reservation.CreatedAt >= fromUtc.Value);
            if (toUtc.HasValue) source = source.Where(x => x.Reservation.CreatedAt <= toUtc.Value);
            if (value is not null)
            {
                source = source.Where(x =>
                    x.Reservation.Reference.Contains(value) ||
                    x.Reservation.RelatedEntityType.Contains(value) ||
                    x.Customer.DisplayName.Contains(value) ||
                    x.Business.BusinessName.Contains(value) ||
                    (x.Account != null && x.Account.ExternalReference.Contains(value)));
            }

            total += await source.CountAsync(ct);
            rows.AddRange(await source
                .OrderByDescending(x => x.Reservation.CreatedAt)
                .Take(take)
                .Select(x => new ActivitySourceRow(
                    x.Reservation.Id,
                    x.Reservation.Type == FinancialReservationType.MarketplaceTrade
                        ? "MarketplaceTradeReservation"
                        : "InstantTradeReservation",
                    "Trading",
                    x.Customer.BusinessProfileId, x.Business.BusinessName,
                    x.Customer.Id, x.Customer.DisplayName,
                    x.Account == null ? null : (Guid?)x.Account.Id,
                    x.Account == null ? null : x.Account.ExternalReference,
                    x.Reservation.FinancialAccountId,
                    x.Reservation.Reference,
                    null,
                    x.FinancialAccount.AssetCode,
                    null,
                    x.Reservation.Amount,
                    null,
                    null,
                    (int)x.Reservation.Status,
                    null,
                    null,
                    null,
                    x.Reservation.RelatedEntityType,
                    x.Reservation.RelatedEntityId,
                    x.Reservation.CreatedAt,
                    x.Reservation.CapturedAt ?? x.Reservation.ReleasedAt,
                    x.Reservation.ReleaseReason))
                .ToListAsync(ct));
        }

        var items = rows
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToActivityDto)
            .ToList();

        return Page(items, total, page, pageSize);
    }


    public async Task<EmbeddedFinanceAdminActivityDetailDto> GetActivityDetailAsync(
        string activityType,
        Guid activityId,
        CancellationToken ct = default)
    {
        var normalizedType = Clean(activityType)
            ?? throw new InvalidOperationException(
                "Embedded Finance activity type is required.");

        var page = await GetActivityAsync(
            null, null, null, normalizedType, null, null, null,
            null, null, 1, 1, ct, activityId);

        var activity = page.Items.SingleOrDefault()
            ?? throw new InvalidOperationException(
                "Embedded Finance activity was not found.");

        var traceIds = new HashSet<Guid> { activity.Id };
        if (activity.RelatedEntityId.HasValue)
            traceIds.Add(activity.RelatedEntityId.Value);

        if (normalizedType.Equals("DigitalAssetDeposit", StringComparison.OrdinalIgnoreCase))
        {
            var collectionId = await _db.DigitalAssetDepositIntents
                .AsNoTracking()
                .Where(x => x.Id == activityId && !x.IsDeleted)
                .Select(x => x.CollectionId)
                .FirstOrDefaultAsync(ct);
            if (collectionId.HasValue)
                traceIds.Add(collectionId.Value);
        }

        if (normalizedType.Equals("DigitalAssetWithdrawal", StringComparison.OrdinalIgnoreCase))
        {
            var payoutId = await _db.DigitalAssetWithdrawals
                .AsNoTracking()
                .Where(x => x.Id == activityId && !x.IsDeleted)
                .Select(x => (Guid?)x.PayoutId)
                .FirstOrDefaultAsync(ct);
            if (payoutId.HasValue)
                traceIds.Add(payoutId.Value);
        }

        var apiContext = await _db.EmbeddedApiIdempotencyRecords
            .AsNoTracking()
            .Where(x => !x.IsDeleted && traceIds.Contains(x.ResourceId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new EmbeddedFinanceAdminApiContextDto(
                x.ApiApplicationId,
                x.ApiApplication.Name,
                x.ResourceType,
                x.ResourceId,
                x.CreatedAt))
            .FirstOrDefaultAsync(ct);

        var providerTransactions = await _db.ProviderTransactions
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                ((x.TransferId.HasValue && traceIds.Contains(x.TransferId.Value)) ||
                 (x.CollectionId.HasValue && traceIds.Contains(x.CollectionId.Value)) ||
                 (x.PayoutId.HasValue && traceIds.Contains(x.PayoutId.Value))))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .Select(x => new EmbeddedFinanceAdminProviderTransactionTraceDto(
                x.Id,
                x.ProviderTransactionId,
                x.ProviderReference,
                x.TransactionType,
                x.ProviderStatus,
                x.CurrencyCode,
                x.Amount,
                x.LastSyncedAt,
                x.CreatedAt))
            .ToListAsync(ct);

        var providerRows = await _db.ProviderRequestLogs
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                ((x.RelatedTransferId.HasValue && traceIds.Contains(x.RelatedTransferId.Value)) ||
                 (x.RelatedCollectionId.HasValue && traceIds.Contains(x.RelatedCollectionId.Value)) ||
                 (x.RelatedPayoutId.HasValue && traceIds.Contains(x.RelatedPayoutId.Value))))
            .OrderByDescending(x => x.RequestedAt)
            .Take(50)
            .ToListAsync(ct);

        var providerRequests = providerRows
            .Select(x => new EmbeddedFinanceAdminProviderRequestTraceDto(
                x.Id,
                x.ProviderCode.ToString(),
                x.Status.ToString(),
                x.ResponseStatusCode,
                !string.IsNullOrWhiteSpace(x.ErrorMessage),
                x.RequestedAt,
                x.RespondedAt,
                x.DurationMs))
            .ToList();

        var reservationRows = await _db.FinancialReservations
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.Id == activity.Id || traceIds.Contains(x.RelatedEntityId)))
            .OrderByDescending(x => x.ReservedAt)
            .Take(50)
            .ToListAsync(ct);

        var reservations = reservationRows
            .Select(x => new EmbeddedFinanceAdminReservationTraceDto(
                x.Id,
                x.Type.ToString(),
                x.RelatedEntityType,
                x.RelatedEntityId,
                x.ContextEntityType,
                x.ContextEntityId,
                x.Reference,
                x.Amount,
                x.CapturedAmount,
                x.ReleasedAmount,
                x.Amount - x.CapturedAmount - x.ReleasedAmount,
                x.Status.ToString(),
                x.ReservedAt,
                x.CapturedAt,
                x.ReleasedAt,
                x.ReleaseReason))
            .ToList();

        var ledgerRows = await _db.LedgerTransactions
            .AsNoTracking()
            .Include(x => x.Postings)
            .Where(x =>
                !x.IsDeleted &&
                x.RelatedEntityId.HasValue &&
                traceIds.Contains(x.RelatedEntityId.Value))
            .OrderByDescending(x => x.PostedAt)
            .Take(50)
            .ToListAsync(ct);

        var ledgers = ledgerRows
            .Select(x => new EmbeddedFinanceAdminLedgerTransactionTraceDto(
                x.Id,
                x.Reference,
                x.AssetCode,
                x.Type.ToString(),
                x.Status.ToString(),
                x.Amount,
                x.RelatedEntityType,
                x.RelatedEntityId,
                x.ContextEntityType,
                x.ContextEntityId,
                x.PostedAt,
                x.ReversedAt,
                x.ReversalReason,
                x.Postings
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => new EmbeddedFinanceAdminLedgerPostingTraceDto(
                        p.Id,
                        p.FinancialAccountId,
                        p.BalanceBucket.ToString(),
                        p.Side.ToString(),
                        p.Amount,
                        p.AccountBalanceAfter))
                    .ToList()))
            .ToList();

        var accountingSourceIds = new HashSet<Guid>(traceIds);
        foreach (var reservation in reservationRows)
            accountingSourceIds.Add(reservation.Id);
        foreach (var ledger in ledgerRows)
            accountingSourceIds.Add(ledger.Id);

        var journalRows = await _db.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x =>
                !x.IsDeleted &&
                x.SourceId.HasValue &&
                accountingSourceIds.Contains(x.SourceId.Value))
            .OrderByDescending(x => x.PostedAt)
            .Take(50)
            .ToListAsync(ct);

        var journals = journalRows
            .Select(x => new EmbeddedFinanceAdminJournalEntryTraceDto(
                x.Id,
                x.Reference,
                x.SourceType.ToString(),
                x.SourceId,
                x.EntryDate,
                x.CurrencyCode,
                x.Status.ToString(),
                x.PostedAt,
                x.ReversedAt,
                x.ReversalReason,
                x.Lines
                    .OrderBy(line => line.CreatedAt)
                    .Select(line => new EmbeddedFinanceAdminJournalLineTraceDto(
                        line.Id,
                        line.AccountingAccountId,
                        line.DebitAmount,
                        line.CreditAmount))
                    .ToList()))
            .ToList();

        var activityEntityName = normalizedType switch
        {
            "Collection" => "Collection",
            "Payout" => "Payout",
            "Transfer" => "Transfer",
            "DigitalAssetDeposit" => "DigitalAssetDepositIntent",
            "DigitalAssetWithdrawal" => "DigitalAssetWithdrawal",
            "MarketplaceTradeReservation" or "InstantTradeReservation" => "FinancialReservation",
            _ => normalizedType
        };

        var activityIdText = activity.Id.ToString();
        var customerIdText = activity.BusinessCustomerId.ToString();
        var accountIdText = activity.CollectionAccountId?.ToString();
        var applicationIdText = apiContext?.ApiApplicationId.ToString();

        var mappingIds = activity.CollectionAccountId.HasValue
            ? (await _db.ProviderAccountMappings
                .AsNoTracking()
                .Where(x =>
                    x.CollectionAccountId == activity.CollectionAccountId.Value &&
                    !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync(ct))
                .Select(x => x.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var auditRows = await _db.AuditLogs
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Category == "EmbeddedFinance" &&
                ((x.EntityName == activityEntityName && x.EntityId == activityIdText) ||
                 (x.EntityName == "BusinessCustomer" && x.EntityId == customerIdText) ||
                 (accountIdText != null && x.EntityName == "CollectionAccount" && x.EntityId == accountIdText) ||
                 (applicationIdText != null && x.EntityName == "ApiApplication" && x.EntityId == applicationIdText) ||
                 (x.EntityName == "ProviderAccountMapping" && x.EntityId != null && mappingIds.Contains(x.EntityId))))
            .OrderByDescending(x => x.OccurredAt)
            .Take(50)
            .Select(x => new
            {
                x.Id,
                x.UserId,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.NewValuesJson,
                x.CorrelationId,
                x.OccurredAt
            })
            .ToListAsync(ct);

        var timeline = auditRows
            .Select(x => new EmbeddedFinanceAdminAuditTimelineDto(
                x.Id,
                x.UserId,
                x.Action,
                x.EntityName,
                x.EntityId,
                ExtractAuditReason(x.NewValuesJson),
                x.CorrelationId,
                x.OccurredAt))
            .ToList();

        return new EmbeddedFinanceAdminActivityDetailDto(
            activity,
            apiContext,
            providerTransactions,
            providerRequests,
            reservations,
            ledgers,
            journals,
            timeline);
    }

    public async Task<PagedResult<EmbeddedFinanceAdminExceptionDto>> GetExceptionsAsync(
        Guid? businessProfileId,
        string? sourceType,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = NormalizePage(page, pageSize);

        var sourceFilter = Clean(sourceType);
        var value = Clean(search);
        var candidates = new List<EmbeddedFinanceAdminExceptionDto>();
        var now = DateTime.UtcNow;

        const int activityScanPageSize = 100;
        const int maxActivityPages = 5;

        for (var scanPage = 1; scanPage <= maxActivityPages; scanPage++)
        {
            var activityPage = await GetActivityAsync(
                businessProfileId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                scanPage,
                activityScanPageSize,
                ct);

            foreach (var item in activityPage.Items.Where(x => NeedsAttention(x, now)))
            {
                var stale = IsStaleActivity(item, now);

                candidates.Add(new EmbeddedFinanceAdminExceptionDto(
                    "Activity",
                    ExceptionSeverity(item, stale),
                    item.Id,
                    item.BusinessProfileId,
                    item.BusinessName,
                    item.BusinessCustomerId,
                    item.BusinessCustomerName,
                    item.CollectionAccountId,
                    item.Reference ?? item.ExternalReference,
                    item.Status,
                    item.ProviderCode,
                    item.FailureReason ??
                        (stale
                            ? "Activity has remained unresolved beyond the review threshold."
                            : null),
                    item.ActivityType,
                    item.Id,
                    item.OccurredAt));
            }

            if (scanPage * activityScanPageSize >= activityPage.Meta.TotalItems)
                break;
        }

        var webhookQuery =
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
                !business.IsDeleted &&
                (delivery.Status == BusinessWebhookDeliveryStatus.Retry ||
                 delivery.Status == BusinessWebhookDeliveryStatus.DeadLetter)
            select new
            {
                Delivery = delivery,
                Endpoint = endpoint,
                BusinessName = business.BusinessName,
                Event = webhookEvent
            };

        if (businessProfileId.HasValue)
        {
            webhookQuery = webhookQuery.Where(x =>
                x.Endpoint.BusinessProfileId == businessProfileId.Value);
        }

        candidates.AddRange(await webhookQuery
            .OrderByDescending(x => x.Delivery.CreatedAt)
            .Take(250)
            .Select(x => new EmbeddedFinanceAdminExceptionDto(
                "WebhookDelivery",
                x.Delivery.Status == BusinessWebhookDeliveryStatus.DeadLetter
                    ? "Critical"
                    : "High",
                x.Delivery.Id,
                x.Endpoint.BusinessProfileId,
                x.BusinessName,
                null,
                null,
                null,
                x.Event.EventId,
                x.Delivery.Status.ToString(),
                null,
                x.Delivery.ErrorMessage,
                null,
                null,
                x.Delivery.DeadLetteredAt ??
                    x.Delivery.LastAttemptAt ??
                    x.Delivery.CreatedAt))
            .ToListAsync(ct));

        var mappingQuery =
            from mapping in _db.ProviderAccountMappings.AsNoTracking()
            join account in _db.CollectionAccounts.AsNoTracking()
                on mapping.CollectionAccountId equals account.Id
            join customer in _db.BusinessCustomers.AsNoTracking()
                on account.BusinessCustomerId equals customer.Id
            join business in _db.BusinessProfiles.AsNoTracking()
                on account.BusinessProfileId equals business.Id
            where
                !mapping.IsDeleted &&
                !account.IsDeleted &&
                !customer.IsDeleted &&
                !business.IsDeleted &&
                mapping.Status == ProviderAccountMappingStatus.Failed
            select new
            {
                Mapping = mapping,
                Account = account,
                Customer = customer,
                Business = business
            };

        if (businessProfileId.HasValue)
        {
            mappingQuery = mappingQuery.Where(x =>
                x.Account.BusinessProfileId == businessProfileId.Value);
        }

        candidates.AddRange(await mappingQuery
            .OrderByDescending(x => x.Mapping.LastUpdatedAt ?? x.Mapping.CreatedAt)
            .Take(250)
            .Select(x => new EmbeddedFinanceAdminExceptionDto(
                "ProviderProvisioning",
                "High",
                x.Mapping.Id,
                x.Account.BusinessProfileId,
                x.Business.BusinessName,
                x.Account.BusinessCustomerId,
                x.Customer.DisplayName,
                x.Account.Id,
                x.Account.ExternalReference,
                x.Mapping.Status.ToString(),
                x.Mapping.ProviderCode,
                x.Mapping.FailureReason,
                null,
                null,
                x.Mapping.LastUpdatedAt ?? x.Mapping.CreatedAt))
            .ToListAsync(ct));

        if (sourceFilter is not null)
        {
            candidates = candidates
                .Where(x => x.SourceType.Equals(
                    sourceFilter,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (value is not null)
        {
            candidates = candidates
                .Where(x =>
                    Contains(x.Reference, value) ||
                    Contains(x.BusinessName, value) ||
                    Contains(x.BusinessCustomerName, value) ||
                    Contains(x.Status, value) ||
                    Contains(x.ProviderCode, value) ||
                    Contains(x.Reason, value) ||
                    Contains(x.ActivityType, value))
                .ToList();
        }

        var ordered = candidates
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenByDescending(x => x.OccurredAt)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Page(items, total, page, pageSize);
    }

    public async Task<byte[]> ExportActivityCsvAsync(
        Guid? businessProfileId,
        Guid? businessCustomerId,
        Guid? collectionAccountId,
        string? activityType,
        string? assetCode,
        string? providerCode,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        const int pageSize = 100;
        const int maxRows = 5000;

        var first = await GetActivityAsync(
            businessProfileId,
            businessCustomerId,
            collectionAccountId,
            activityType,
            assetCode,
            providerCode,
            search,
            fromUtc,
            toUtc,
            1,
            pageSize,
            ct);

        if (first.Meta.TotalItems > maxRows)
        {
            throw new InvalidOperationException(
                $"The filtered export contains {first.Meta.TotalItems} rows. " +
                $"Narrow the filters to {maxRows} rows or fewer.");
        }

        var rows = new List<EmbeddedFinanceAdminActivityDto>(first.Items);
        var totalPages = (int)Math.Ceiling(
            first.Meta.TotalItems / (double)pageSize);

        for (var exportPage = 2; exportPage <= totalPages; exportPage++)
        {
            var next = await GetActivityAsync(
                businessProfileId,
                businessCustomerId,
                collectionAccountId,
                activityType,
                assetCode,
                providerCode,
                search,
                fromUtc,
                toUtc,
                exportPage,
                pageSize,
                ct);

            rows.AddRange(next.Items);
        }

        var csv = new StringBuilder();

        csv.AppendLine(
            "activityId,activityType,direction,businessProfileId,businessName," +
            "businessCustomerId,businessCustomerName,collectionAccountId," +
            "collectionAccountReference,financialAccountId,reference,externalReference," +
            "assetCode,destinationAssetCode,amount,secondaryAmount,feeAmount,status," +
            "providerCode,providerReference,occurredAt,finalizedAt,failureReason");

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(
                ",",
                Csv(row.Id.ToString()),
                Csv(row.ActivityType),
                Csv(row.Direction),
                Csv(row.BusinessProfileId.ToString()),
                Csv(row.BusinessName),
                Csv(row.BusinessCustomerId.ToString()),
                Csv(row.BusinessCustomerName),
                Csv(row.CollectionAccountId?.ToString()),
                Csv(row.CollectionAccountReference),
                Csv(row.FinancialAccountId?.ToString()),
                Csv(row.Reference),
                Csv(row.ExternalReference),
                Csv(row.AssetCode),
                Csv(row.DestinationAssetCode),
                Csv(row.Amount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
                Csv(row.SecondaryAmount?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
                Csv(row.FeeAmount?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
                Csv(row.Status),
                Csv(row.ProviderCode),
                Csv(row.ProviderReference),
                Csv(row.OccurredAt.ToString("O")),
                Csv(row.FinalizedAt?.ToString("O")),
                Csv(row.FailureReason)));
        }

        return Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString()))
            .ToArray();
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


    private static EmbeddedFinanceAdminActivityDto ToActivityDto(ActivitySourceRow row) =>
        new(
            row.Id,
            row.ActivityType,
            row.Direction,
            row.BusinessProfileId,
            row.BusinessName,
            row.BusinessCustomerId,
            row.BusinessCustomerName,
            row.CollectionAccountId,
            row.CollectionAccountReference,
            row.FinancialAccountId,
            row.Reference,
            row.ExternalReference,
            row.AssetCode,
            row.DestinationAssetCode,
            row.Amount,
            row.SecondaryAmount,
            row.FeeAmount,
            ActivityStatus(row.ActivityType, row.StatusValue),
            row.ProviderCode,
            row.ProviderReference,
            row.ProviderRequestLogId,
            row.RelatedEntityType,
            row.RelatedEntityId,
            row.OccurredAt,
            row.FinalizedAt,
            row.FailureReason);

    private static string ActivityStatus(string activityType, int value) =>
        activityType switch
        {
            "Collection" or "DigitalAssetDeposit" =>
                Enum.GetName(typeof(CollectionStatus), value) ?? value.ToString(),
            "Payout" =>
                Enum.GetName(typeof(PayoutStatus), value) ?? value.ToString(),
            "Transfer" =>
                Enum.GetName(typeof(TransferStatus), value) ?? value.ToString(),
            "DigitalAssetWithdrawal" =>
                Enum.GetName(typeof(DigitalAssetWithdrawalStatus), value) ?? value.ToString(),
            "MarketplaceTradeReservation" or "InstantTradeReservation" =>
                Enum.GetName(typeof(FinancialReservationStatus), value) ?? value.ToString(),
            _ => value.ToString()
        };

    private sealed record ActivitySourceRow(
        Guid Id,
        string ActivityType,
        string Direction,
        Guid BusinessProfileId,
        string BusinessName,
        Guid BusinessCustomerId,
        string BusinessCustomerName,
        Guid? CollectionAccountId,
        string? CollectionAccountReference,
        Guid? FinancialAccountId,
        string? Reference,
        string? ExternalReference,
        string AssetCode,
        string? DestinationAssetCode,
        decimal Amount,
        decimal? SecondaryAmount,
        decimal? FeeAmount,
        int StatusValue,
        string? ProviderCode,
        string? ProviderReference,
        Guid? ProviderRequestLogId,
        string? RelatedEntityType,
        Guid? RelatedEntityId,
        DateTime OccurredAt,
        DateTime? FinalizedAt,
        string? FailureReason);

    private static bool NeedsAttention(
        EmbeddedFinanceAdminActivityDto activity,
        DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(activity.FailureReason))
            return true;

        var status = activity.Status.Trim();

        if (
            status.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Expired", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Reversed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsStaleActivity(activity, now);
    }

    private static bool IsStaleActivity(
        EmbeddedFinanceAdminActivityDto activity,
        DateTime now)
    {
        if (activity.FinalizedAt.HasValue)
            return false;

        if (activity.OccurredAt > now.AddMinutes(-30))
            return false;

        var status = activity.Status.Trim();

        return status.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Initiated", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Processing", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Active", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("Submitted", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExceptionSeverity(
        EmbeddedFinanceAdminActivityDto activity,
        bool stale)
    {
        if (stale)
            return "Medium";

        if (
            activity.Status.Contains("Failed", StringComparison.OrdinalIgnoreCase) ||
            activity.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return "High";
        }

        return "Medium";
    }

    private static int SeverityRank(string value) =>
        value switch
        {
            "Critical" => 3,
            "High" => 2,
            _ => 1
        };

    private static bool Contains(string? source, string value) =>
        source?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;

        if (
            text.StartsWith('=') ||
            text.StartsWith('+') ||
            text.StartsWith('-') ||
            text.StartsWith('@'))
        {
            text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
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


    private static string? ExtractAuditReason(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("Reason", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (property.Value.ValueKind != JsonValueKind.String)
                    return null;

                var value = property.Value.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                    return null;

                return value.Length <= 1000 ? value : value[..1000];
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static ApiCredentialStatus EffectiveCredentialStatus(
        ApiCredentialStatus status,
        DateTime? expiresAt,
        DateTime now) =>
        status == ApiCredentialStatus.Active &&
        expiresAt.HasValue &&
        expiresAt.Value <= now
            ? ApiCredentialStatus.Expired
            : status;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Code(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
}
