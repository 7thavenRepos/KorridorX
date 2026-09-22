using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinanceAdminCommandService
    : IEmbeddedFinanceAdminCommandService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IEmbeddedWebhookPublisher _webhooks;
    private readonly IReadOnlyDictionary<string, ICollectionAccountProvisioner> _provisioners;

    public EmbeddedFinanceAdminCommandService(
        AppDbContext db,
        IAuditService audit,
        IEmbeddedWebhookPublisher webhooks,
        IEnumerable<ICollectionAccountProvisioner> provisioners)
    {
        _db = db;
        _audit = audit;
        _webhooks = webhooks;
        _provisioners = provisioners.ToDictionary(
            x => x.ProviderCode,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<EmbeddedFinanceAdminActionResultDto> SetApplicationStatusAsync(
        Guid actorUserId,
        Guid apiApplicationId,
        ApiApplicationStatus status,
        string reason,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(ApiApplicationStatus), status))
            throw new InvalidOperationException("Invalid API application status.");

        var normalizedReason = RequiredReason(reason);

        var entity = await _db.ApiApplications
            .FirstOrDefaultAsync(
                x => x.Id == apiApplicationId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("API application not found.");

        EnsureApplicationStatusTransition(entity.Status, status);

        var previous = entity.Status;

        if (previous == status)
        {
            return new EmbeddedFinanceAdminActionResultDto(
                entity.Id,
                nameof(ApiApplication),
                entity.Status.ToString(),
                entity.LastUpdatedAt ?? entity.CreatedAt);
        }

        entity.Status = status;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = actorUserId;

        _audit.Stage(new AuditRecordRequest(
            "EmbeddedFinanceApiApplicationStatusChanged",
            "EmbeddedFinance",
            nameof(ApiApplication),
            entity.Id.ToString(),
            OldValues: new
            {
                Status = previous
            },
            NewValues: new
            {
                Status = entity.Status,
                entity.BusinessProfileId,
                Reason = normalizedReason
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        return new EmbeddedFinanceAdminActionResultDto(
            entity.Id,
            nameof(ApiApplication),
            entity.Status.ToString(),
            entity.LastUpdatedAt.Value);
    }

    public async Task<EmbeddedFinanceAdminActionResultDto> RevokeCredentialAsync(
        Guid actorUserId,
        Guid apiApplicationId,
        Guid credentialId,
        string reason,
        CancellationToken ct = default)
    {
        var normalizedReason = RequiredReason(reason);

        var entity = await _db.ApiCredentials
            .Include(x => x.ApiApplication)
            .FirstOrDefaultAsync(
                x =>
                    x.Id == credentialId &&
                    x.ApiApplicationId == apiApplicationId &&
                    !x.IsDeleted &&
                    !x.ApiApplication.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("API credential not found.");

        var previous = entity.Status;

        if (previous == ApiCredentialStatus.Revoked)
        {
            return new EmbeddedFinanceAdminActionResultDto(
                entity.Id,
                nameof(ApiCredential),
                entity.Status.ToString(),
                entity.LastUpdatedAt ?? entity.CreatedAt);
        }

        entity.Status = ApiCredentialStatus.Revoked;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = actorUserId;

        _audit.Stage(new AuditRecordRequest(
            "EmbeddedFinanceApiCredentialRevoked",
            "EmbeddedFinance",
            nameof(ApiCredential),
            entity.Id.ToString(),
            OldValues: new
            {
                Status = previous
            },
            NewValues: new
            {
                Status = entity.Status,
                entity.ApiApplicationId,
                Reason = normalizedReason
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        return new EmbeddedFinanceAdminActionResultDto(
            entity.Id,
            nameof(ApiCredential),
            entity.Status.ToString(),
            entity.LastUpdatedAt.Value);
    }


    public async Task<EmbeddedFinanceAdminActionResultDto> SetCustomerStatusAsync(
        Guid actorUserId,
        Guid businessCustomerId,
        BusinessCustomerStatus status,
        string reason,
        CancellationToken ct = default)
    {
        if (status is not (
            BusinessCustomerStatus.Active or
            BusinessCustomerStatus.Suspended))
        {
            throw new InvalidOperationException(
                "Admin customer lifecycle controls currently support only Active and Suspended. " +
                "Terminal Close remains provider-orchestration controlled.");
        }

        var normalizedReason = RequiredReason(reason);

        var entity = await _db.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.Id == businessCustomerId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Business customer not found.");

        if (entity.Status == BusinessCustomerStatus.Closed)
        {
            throw new InvalidOperationException(
                "A closed business customer cannot be reactivated.");
        }

        if (entity.Status == status)
        {
            return new EmbeddedFinanceAdminActionResultDto(
                entity.Id,
                nameof(BusinessCustomer),
                entity.Status.ToString(),
                entity.LastUpdatedAt ?? entity.CreatedAt);
        }

        var previous = entity.Status;
        var changedAt = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        entity.Status = status;
        entity.LastUpdatedAt = changedAt;
        entity.LastUpdatedByUserId = actorUserId;

        _audit.Stage(new AuditRecordRequest(
            "EmbeddedFinanceBusinessCustomerStatusChanged",
            "EmbeddedFinance",
            nameof(BusinessCustomer),
            entity.Id.ToString(),
            OldValues: new
            {
                Status = previous
            },
            NewValues: new
            {
                Status = entity.Status,
                entity.BusinessProfileId,
                Reason = normalizedReason
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        await _webhooks.PublishAsync(
            entity.BusinessProfileId,
            "customer.status.changed",
            new
            {
                id = entity.Id,
                previousStatus = previous.ToString(),
                status = entity.Status.ToString(),
                changedAt
            },
            ct);

        await tx.CommitAsync(ct);

        return new EmbeddedFinanceAdminActionResultDto(
            entity.Id,
            nameof(BusinessCustomer),
            entity.Status.ToString(),
            changedAt);
    }

    public async Task<EmbeddedFinanceAdminCollectionAccountActionResultDto> SetCollectionAccountStatusAsync(
        Guid actorUserId,
        Guid collectionAccountId,
        CollectionAccountStatus status,
        string reason,
        CancellationToken ct = default)
    {
        if (status is not (
            CollectionAccountStatus.Active or
            CollectionAccountStatus.Suspended))
        {
            throw new InvalidOperationException(
                "Admin collection-account lifecycle controls currently support only Active and Suspended. " +
                "Pending activation remains provider-provisioning controlled and terminal Close remains deferred.");
        }

        var normalizedReason = RequiredReason(reason);

        var entity = await _db.CollectionAccounts
            .Include(x => x.FinancialAccount)
            .Include(x => x.ProviderMappings)
            .FirstOrDefaultAsync(
                x => x.Id == collectionAccountId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Collection account not found.");

        if (entity.Status == CollectionAccountStatus.Closed)
        {
            throw new InvalidOperationException(
                "A closed collection account cannot be reactivated.");
        }

        if (entity.Status == CollectionAccountStatus.Pending)
        {
            throw new InvalidOperationException(
                "Pending collection accounts must be activated through provider provisioning.");
        }

        if (entity.FinancialAccount.Status == FinancialAccountStatus.Closed)
        {
            throw new InvalidOperationException(
                "The linked financial account is closed and cannot be reactivated.");
        }

        if (
            status == CollectionAccountStatus.Active &&
            !entity.ProviderMappings.Any(
                x =>
                    !x.IsDeleted &&
                    x.Status == ProviderAccountMappingStatus.Active))
        {
            throw new InvalidOperationException(
                "The collection account cannot be reactivated without an active provider mapping.");
        }

        var targetFinancialStatus =
            status == CollectionAccountStatus.Active
                ? FinancialAccountStatus.Active
                : FinancialAccountStatus.Frozen;

        if (
            entity.Status == status &&
            entity.FinancialAccount.Status == targetFinancialStatus)
        {
            return new EmbeddedFinanceAdminCollectionAccountActionResultDto(
                entity.Id,
                entity.Status.ToString(),
                entity.FinancialAccount.Status.ToString(),
                entity.LastUpdatedAt ?? entity.CreatedAt);
        }

        var previousCollectionStatus = entity.Status;
        var previousFinancialStatus = entity.FinancialAccount.Status;
        var changedAt = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        entity.Status = status;
        entity.LastUpdatedAt = changedAt;
        entity.LastUpdatedByUserId = actorUserId;

        entity.FinancialAccount.Status = targetFinancialStatus;
        entity.FinancialAccount.LastUpdatedAt = changedAt;
        entity.FinancialAccount.LastUpdatedByUserId = actorUserId;

        _audit.Stage(new AuditRecordRequest(
            "EmbeddedFinanceCollectionAccountStatusChanged",
            "EmbeddedFinance",
            nameof(CollectionAccount),
            entity.Id.ToString(),
            OldValues: new
            {
                Status = previousCollectionStatus,
                FinancialAccountStatus = previousFinancialStatus
            },
            NewValues: new
            {
                Status = entity.Status,
                FinancialAccountStatus = entity.FinancialAccount.Status,
                entity.BusinessProfileId,
                entity.BusinessCustomerId,
                entity.FinancialAccountId,
                Reason = normalizedReason
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        await _webhooks.PublishAsync(
            entity.BusinessProfileId,
            "account.status.changed",
            new
            {
                id = entity.Id,
                businessCustomerId = entity.BusinessCustomerId,
                assetCode = entity.AssetCode,
                previousStatus = previousCollectionStatus.ToString(),
                status = entity.Status.ToString(),
                financialAccountStatus =
                    entity.FinancialAccount.Status.ToString(),
                changedAt
            },
            ct);

        await tx.CommitAsync(ct);

        return new EmbeddedFinanceAdminCollectionAccountActionResultDto(
            entity.Id,
            entity.Status.ToString(),
            entity.FinancialAccount.Status.ToString(),
            changedAt);
    }


    public async Task<EmbeddedFinanceAdminProvisioningRetryResultDto> RetryProviderMappingAsync(
        Guid actorUserId,
        Guid collectionAccountId,
        Guid providerMappingId,
        string reason,
        CancellationToken ct = default)
    {
        var normalizedReason = RequiredReason(reason);

        var mapping = await _db.ProviderAccountMappings
            .Include(x => x.CollectionAccount)
                .ThenInclude(x => x.BusinessCustomer)
            .FirstOrDefaultAsync(
                x =>
                    x.Id == providerMappingId &&
                    x.CollectionAccountId == collectionAccountId &&
                    !x.IsDeleted &&
                    !x.CollectionAccount.IsDeleted &&
                    !x.CollectionAccount.BusinessCustomer.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Provider account mapping not found.");

        if (mapping.Status != ProviderAccountMappingStatus.Failed)
        {
            throw new InvalidOperationException(
                "Only failed provider-account mappings can be retried from the admin workspace.");
        }

        var account = mapping.CollectionAccount;
        var customer = account.BusinessCustomer;

        if (customer.Status != BusinessCustomerStatus.Active)
        {
            throw new InvalidOperationException(
                "The business customer must be active before provider provisioning can be retried.");
        }

        if (account.Status == CollectionAccountStatus.Suspended)
        {
            throw new InvalidOperationException(
                "Reactivate the collection account before retrying provider provisioning.");
        }

        if (account.Status == CollectionAccountStatus.Closed)
        {
            throw new InvalidOperationException(
                "A closed collection account cannot be provisioned.");
        }

        if (!_provisioners.TryGetValue(
                mapping.ProviderCode,
                out var provisioner))
        {
            throw new InvalidOperationException(
                $"Provider '{mapping.ProviderCode}' does not expose collection-account provisioning in this deployment.");
        }

        if (!provisioner.Supports(
                customer.CountryCode,
                account.AssetCode))
        {
            throw new InvalidOperationException(
                $"Provider '{mapping.ProviderCode}' does not support collection-account provisioning for {customer.CountryCode}/{account.AssetCode}.");
        }

        var retryStartedAt = DateTime.UtcNow;

        mapping.Status = ProviderAccountMappingStatus.Pending;
        mapping.FailureReason = null;
        mapping.LastUpdatedAt = retryStartedAt;
        mapping.LastUpdatedByUserId = actorUserId;

        _audit.Stage(new AuditRecordRequest(
            "EmbeddedFinanceProviderProvisioningRetryRequested",
            "EmbeddedFinance",
            nameof(ProviderAccountMapping),
            mapping.Id.ToString(),
            OldValues: new
            {
                Status = ProviderAccountMappingStatus.Failed
            },
            NewValues: new
            {
                Status = mapping.Status,
                mapping.CollectionAccountId,
                mapping.ProviderCode,
                Reason = normalizedReason
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        try
        {
            var dispatchState = await _db.ProviderAccountMappings
                .AsNoTracking()
                .Where(x =>
                    x.Id == mapping.Id &&
                    x.CollectionAccountId == account.Id &&
                    !x.IsDeleted &&
                    !x.CollectionAccount.IsDeleted &&
                    !x.CollectionAccount.BusinessCustomer.IsDeleted)
                .Select(x => new
                {
                    MappingStatus = x.Status,
                    CollectionAccountStatus = x.CollectionAccount.Status,
                    CustomerStatus = x.CollectionAccount.BusinessCustomer.Status
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException(
                    "Provider account mapping is no longer available for retry.");

            if (dispatchState.MappingStatus != ProviderAccountMappingStatus.Pending)
                throw new InvalidOperationException(
                    "Provider account mapping is no longer pending for retry.");

            if (dispatchState.CustomerStatus != BusinessCustomerStatus.Active)
                throw new InvalidOperationException(
                    "The business customer must remain active before provider provisioning is dispatched.");

            if (dispatchState.CollectionAccountStatus == CollectionAccountStatus.Suspended)
                throw new InvalidOperationException(
                    "Reactivate the collection account before provider provisioning is dispatched.");

            if (dispatchState.CollectionAccountStatus == CollectionAccountStatus.Closed)
                throw new InvalidOperationException(
                    "A closed collection account cannot be provisioned.");

            var result = await provisioner.ProvisionAsync(
                new CollectionAccountProvisioningRequest(
                    account.BusinessProfileId,
                    account.BusinessCustomerId,
                    account.Id,
                    account.ExternalReference,
                    customer.DisplayName,
                    customer.Email,
                    customer.PhoneNumber,
                    customer.CountryCode,
                    account.AssetCode),
                ct);

            await _db.Entry(mapping).ReloadAsync(ct);
            await _db.Entry(account).ReloadAsync(ct);
            if (mapping.Status != ProviderAccountMappingStatus.Pending)
                return new EmbeddedFinanceAdminProvisioningRetryResultDto(ToAdminProviderMapping(mapping), account.Status, DateTime.UtcNow);
            if (result.Status is not (ProviderAccountMappingStatus.Pending or ProviderAccountMappingStatus.Active or ProviderAccountMappingStatus.Failed))
                throw new InvalidOperationException("Provider returned an invalid provisioning state.");
            if (mapping.ProviderAccountId is not null && mapping.ProviderAccountId != result.ProviderAccountId)
                throw new InvalidOperationException("Provider response conflicts with the recorded virtual-account ID.");
            if (string.IsNullOrWhiteSpace(result.ProviderAccountId))
            {
                throw new InvalidOperationException(
                    "Provider did not return a provider account identifier.");
            }

            var previousAccountStatus = account.Status;
            var completedAt = DateTime.UtcNow;

            mapping.ProviderCustomerId =
                CleanProviderValue(result.ProviderCustomerId, 150);
            mapping.ProviderAccountId =
                RequiredProviderValue(
                    result.ProviderAccountId,
                    150,
                    "Provider account ID");
            mapping.ProviderReference =
                CleanProviderValue(result.ProviderReference, 150);
            mapping.AccountNumber =
                CleanProviderValue(result.AccountNumber, 150);
            mapping.AccountName =
                CleanProviderValue(result.AccountName, 200);
            mapping.BankName =
                CleanProviderValue(result.BankName, 200);
            mapping.MetadataJson = result.MetadataJson;
            mapping.Status = result.Status;
            mapping.FailureReason = result.FailureReason;
            mapping.LastUpdatedAt = completedAt;
            mapping.LastUpdatedByUserId = actorUserId;

            if (result.Status == ProviderAccountMappingStatus.Active && account.Status == CollectionAccountStatus.Pending &&
                await _db.BusinessCustomers.AsNoTracking().AnyAsync(x => x.Id == account.BusinessCustomerId &&
                    !x.IsDeleted && x.Status == BusinessCustomerStatus.Active, ct))
            {
                account.Status = CollectionAccountStatus.Active;
                account.LastUpdatedAt = completedAt;
                account.LastUpdatedByUserId = actorUserId;
            }

            _audit.Stage(new AuditRecordRequest(
                result.Status == ProviderAccountMappingStatus.Pending
                    ? "EmbeddedFinanceProviderProvisioningRetryPending" : "EmbeddedFinanceProviderProvisioningRetryCompleted",
                "EmbeddedFinance",
                nameof(ProviderAccountMapping),
                mapping.Id.ToString(),
                OldValues: new
                {
                    Status = ProviderAccountMappingStatus.Pending,
                    CollectionAccountStatus = previousAccountStatus
                },
                NewValues: new
                {
                    Status = mapping.Status,
                    CollectionAccountStatus = account.Status,
                    mapping.CollectionAccountId,
                    mapping.ProviderCode,
                    mapping.ProviderReference
                },
                UserId: actorUserId));

            await _db.SaveChangesAsync(ct);

            if (
                previousAccountStatus != account.Status &&
                account.Status == CollectionAccountStatus.Active)
            {
                await _webhooks.PublishAsync(
                    account.BusinessProfileId,
                    "account.status.changed",
                    new
                    {
                        id = account.Id,
                        businessCustomerId = account.BusinessCustomerId,
                        assetCode = account.AssetCode,
                        previousStatus =
                            previousAccountStatus.ToString(),
                        status = account.Status.ToString(),
                        changedAt = completedAt
                    },
                    ct);
            }

            return new EmbeddedFinanceAdminProvisioningRetryResultDto(
                ToAdminProviderMapping(mapping),
                account.Status,
                completedAt);
        }
        catch (Exception ex)
        {
            await _db.Entry(mapping).ReloadAsync(ct);
            await _db.Entry(account).ReloadAsync(ct);
            if (mapping.Status != ProviderAccountMappingStatus.Pending) throw;
            var failedAt = DateTime.UtcNow;

            mapping.Status = ProviderAccountMappingStatus.Failed;
            mapping.FailureReason =
                TruncateProviderFailure(ex.Message);
            mapping.LastUpdatedAt = failedAt;
            mapping.LastUpdatedByUserId = actorUserId;

            _audit.Stage(new AuditRecordRequest(
                "EmbeddedFinanceProviderProvisioningRetryFailed",
                "EmbeddedFinance",
                nameof(ProviderAccountMapping),
                mapping.Id.ToString(),
                OldValues: new
                {
                    Status = ProviderAccountMappingStatus.Pending
                },
                NewValues: new
                {
                    Status = mapping.Status,
                    mapping.CollectionAccountId,
                    mapping.ProviderCode,
                    ErrorType = ex.GetType().Name
                },
                UserId: actorUserId));

            await _db.SaveChangesAsync(ct);

            throw new InvalidOperationException(
                "Provider provisioning retry failed. Review the provider mapping failure reason.",
                ex);
        }
    }

    public async Task<EmbeddedFinanceAdminActionResultDto> SetWebhookEndpointStatusAsync(
        Guid actorUserId,
        Guid endpointId,
        bool enabled,
        string reason,
        CancellationToken ct = default)
    {
        var normalizedReason = RequiredReason(reason);

        var entity = await _db.BusinessWebhookEndpoints
            .Include(x => x.ApiApplication)
            .FirstOrDefaultAsync(
                x => x.Id == endpointId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Webhook endpoint not found.");

        if (enabled &&
            (entity.ApiApplication.IsDeleted ||
             entity.ApiApplication.Status != ApiApplicationStatus.Active))
        {
            throw new InvalidOperationException(
                "Webhook endpoint cannot be enabled while its API application is not active.");
        }

        var previous = entity.Status;
        var target = enabled
            ? BusinessWebhookEndpointStatus.Active
            : BusinessWebhookEndpointStatus.Disabled;

        if (previous == target)
        {
            return new EmbeddedFinanceAdminActionResultDto(
                entity.Id,
                nameof(BusinessWebhookEndpoint),
                entity.Status.ToString(),
                entity.LastUpdatedAt ?? entity.CreatedAt);
        }

        entity.Status = target;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = actorUserId;

        _audit.Stage(new AuditRecordRequest(
            "EmbeddedFinanceWebhookEndpointStatusChanged",
            "EmbeddedFinance",
            nameof(BusinessWebhookEndpoint),
            entity.Id.ToString(),
            OldValues: new
            {
                Status = previous
            },
            NewValues: new
            {
                Status = entity.Status,
                entity.BusinessProfileId,
                entity.ApiApplicationId,
                Reason = normalizedReason
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        return new EmbeddedFinanceAdminActionResultDto(
            entity.Id,
            nameof(BusinessWebhookEndpoint),
            entity.Status.ToString(),
            entity.LastUpdatedAt.Value);
    }

    public async Task<EmbeddedFinanceAdminActionResultDto> RetryWebhookDeliveryAsync(
        Guid actorUserId,
        Guid deliveryId,
        string reason,
        CancellationToken ct = default)
    {
        var normalizedReason = RequiredReason(reason);

        var entity = await _db.BusinessWebhookDeliveries
            .Include(x => x.BusinessWebhookEndpoint)
                .ThenInclude(x => x.ApiApplication)
            .FirstOrDefaultAsync(
                x =>
                    x.Id == deliveryId &&
                    !x.IsDeleted &&
                    !x.BusinessWebhookEndpoint.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Webhook delivery not found.");

        if (entity.Status == BusinessWebhookDeliveryStatus.Delivered)
            throw new InvalidOperationException(
                "Delivered webhook deliveries cannot be retried.");

        if (entity.BusinessWebhookEndpoint.Status !=
            BusinessWebhookEndpointStatus.Active)
        {
            throw new InvalidOperationException(
                "Webhook delivery cannot be retried while its endpoint is disabled.");
        }

        if (entity.BusinessWebhookEndpoint.ApiApplication.IsDeleted ||
            entity.BusinessWebhookEndpoint.ApiApplication.Status !=
            ApiApplicationStatus.Active)
        {
            throw new InvalidOperationException(
                "Webhook delivery cannot be retried while its API application is not active.");
        }

        var previous = entity.Status;

        entity.Status = BusinessWebhookDeliveryStatus.Pending;
        entity.AttemptCount = 0;
        entity.NextAttemptAt = DateTime.UtcNow;
        entity.LockId = null;
        entity.LockedAt = null;
        entity.DeadLetteredAt = null;
        entity.ErrorMessage = null;
        entity.LastUpdatedAt = DateTime.UtcNow;

        _audit.Stage(new AuditRecordRequest(
            "EmbeddedFinanceWebhookDeliveryRetried",
            "EmbeddedFinance",
            nameof(BusinessWebhookDelivery),
            entity.Id.ToString(),
            OldValues: new
            {
                Status = previous
            },
            NewValues: new
            {
                Status = entity.Status,
                entity.BusinessWebhookEndpointId,
                Reason = normalizedReason
            },
            UserId: actorUserId));

        await _db.SaveChangesAsync(ct);

        return new EmbeddedFinanceAdminActionResultDto(
            entity.Id,
            nameof(BusinessWebhookDelivery),
            entity.Status.ToString(),
            entity.LastUpdatedAt.Value);
    }


    private static void EnsureApplicationStatusTransition(
        ApiApplicationStatus current,
        ApiApplicationStatus target)
    {
        if (current == ApiApplicationStatus.Disabled &&
            target != ApiApplicationStatus.Disabled)
        {
            throw new InvalidOperationException(
                "A disabled API application is terminal and cannot be reactivated.");
        }
    }

    private static string? CleanProviderValue(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var cleaned = value.Trim();

        return cleaned.Length <= maxLength
            ? cleaned
            : cleaned[..maxLength];
    }

    private static string RequiredProviderValue(
        string? value,
        int maxLength,
        string label)
    {
        var cleaned = (value ?? string.Empty).Trim();

        if (cleaned.Length == 0)
            throw new InvalidOperationException(
                $"{label} is required.");

        if (cleaned.Length > maxLength)
            throw new InvalidOperationException(
                $"{label} cannot exceed {maxLength} characters.");

        return cleaned;
    }

    private static EmbeddedFinanceAdminProviderMappingDto ToAdminProviderMapping(
        ProviderAccountMapping value) =>
        new(
            value.Id,
            value.CollectionAccountId,
            value.ProviderCode,
            value.ProviderCustomerId,
            value.ProviderAccountId,
            value.ProviderReference,
            value.AccountNumber,
            value.AccountName,
            value.BankName,
            value.Status,
            value.FailureReason,
            value.CreatedAt,
            value.LastUpdatedAt,
            BlaaizVirtualAccountState.BankDetails(value.MetadataJson));

    private static string TruncateProviderFailure(string? value)
    {
        var cleaned = string.IsNullOrWhiteSpace(value)
            ? "Provider provisioning retry failed."
            : value.Trim();

        return cleaned.Length <= 1000
            ? cleaned
            : cleaned[..1000];
    }

    private static string RequiredReason(string value)
    {
        var reason = (value ?? string.Empty).Trim();

        if (reason.Length < 10)
            throw new InvalidOperationException(
                "An internal reason of at least 10 characters is required.");

        if (reason.Length > 1000)
            throw new InvalidOperationException(
                "Internal reason cannot exceed 1000 characters.");

        return reason;
    }
}
