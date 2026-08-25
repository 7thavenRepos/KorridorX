using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Exceptions;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Compliance;
using KorridorX.Services.FinancialCore;
using KorridorX.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinancePayoutService : IEmbeddedFinancePayoutService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddedFinanceContextAccessor _context;
    private readonly IFinancialReservationService _reservations;
    private readonly IOutboundFundsRestrictionService? _outboundFundsRestrictions;
    private readonly IRemittanceProvider _provider;
    private readonly IPayoutStatusService _payoutStatus;
    private readonly IEmbeddedPayoutSettlementService _settlement;
    private readonly IEmbeddedWebhookPublisher _webhooks;
    private readonly IReadOnlyDictionary<string, string> _payoutWalletIds;

    public EmbeddedFinancePayoutService(
        AppDbContext db,
        IEmbeddedFinanceContextAccessor context,
        IFinancialReservationService reservations,
        IRemittanceProvider provider,
        IPayoutStatusService payoutStatus,
        IEmbeddedPayoutSettlementService settlement,
        IEmbeddedWebhookPublisher webhooks,
        IOptions<BlaaizOptions> blaaizOptions,
        IOutboundFundsRestrictionService? outboundFundsRestrictions = null)
    {
        _db = db;
        _context = context;
        _reservations = reservations;
        _outboundFundsRestrictions = outboundFundsRestrictions;
        _provider = provider;
        _payoutStatus = payoutStatus;
        _settlement = settlement;
        _webhooks = webhooks;
        _payoutWalletIds = blaaizOptions.Value.PayoutWalletIds;
    }

    public async Task<PagedResult<EmbeddedPayoutDto>> GetPayoutsAsync(
        Guid businessCustomerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.PayoutsRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);

        var result = await _db.Payouts
            .AsNoTracking()
            .Where(x =>
                x.Purpose == PaymentOperationPurpose.Withdrawal &&
                x.ContextEntityType == nameof(BusinessCustomer) &&
                x.ContextEntityId == businessCustomerId &&
                !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);

        var items = new List<EmbeddedPayoutDto>();
        foreach (var payout in result.Items)
            items.Add(await ToDtoAsync(payout, ct));

        return new PagedResult<EmbeddedPayoutDto>
        {
            Items = items,
            Meta = result.Meta
        };
    }

    public async Task<EmbeddedPayoutDto> GetPayoutAsync(
        Guid businessCustomerId,
        Guid payoutId,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.PayoutsRead);
        await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);

        var payout = await _db.Payouts
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == payoutId &&
                x.Purpose == PaymentOperationPurpose.Withdrawal &&
                x.ContextEntityType == nameof(BusinessCustomer) &&
                x.ContextEntityId == businessCustomerId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Embedded payout not found.");

        return await ToDtoAsync(payout, ct);
    }

    public async Task<EmbeddedPayoutDto> CreatePayoutAsync(
        Guid businessCustomerId,
        Guid collectionAccountId,
        CreateEmbeddedPayoutRequestDto request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var principal = RequireScope(EmbeddedFinanceScope.PayoutsWrite);
        var key = NormalizeKey(idempotencyKey);
        var externalReference = Required(request.ExternalReference, 50, "External reference");

        if (request.Amount <= 0m)
            throw new InvalidOperationException("Payout amount must be greater than zero.");

        var customer = await EnsureCustomerAsync(principal.BusinessProfileId, businessCustomerId, ct);
        if (customer.Status != BusinessCustomerStatus.Active)
            throw new InvalidOperationException("Business customer must be active.");

        var collectionAccount = await _db.CollectionAccounts
            .Include(x => x.FinancialAccount)
            .FirstOrDefaultAsync(x =>
                x.Id == collectionAccountId &&
                x.BusinessProfileId == principal.BusinessProfileId &&
                x.BusinessCustomerId == businessCustomerId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Collection account not found.");

        if (collectionAccount.Status != CollectionAccountStatus.Active)
            throw new InvalidOperationException("Collection account must be active.");

        var account = collectionAccount.FinancialAccount;
        if (account.OwnerType != FinancialAccountOwnerType.BusinessCustomer || account.OwnerId != businessCustomerId)
            throw new InvalidOperationException("Collection account ownership is invalid.");
        if (!string.Equals(collectionAccount.AssetCode, account.AssetCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Collection account asset does not match its Financial Account asset.");
        if (account.Status != FinancialAccountStatus.Active)
            throw new InvalidOperationException("Financial account must be active.");

        var beneficiaryAccount = await _db.BusinessBeneficiaryBankAccounts
            .Include(x => x.BusinessBeneficiary)
            .FirstOrDefaultAsync(x =>
                x.Id == request.BusinessBeneficiaryBankAccountId &&
                x.BusinessBeneficiary.BusinessProfileId == principal.BusinessProfileId &&
                !x.IsDeleted &&
                !x.BusinessBeneficiary.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business beneficiary bank account not found.");

        if (!beneficiaryAccount.IsActive || !beneficiaryAccount.BusinessBeneficiary.IsActive)
            throw new InvalidOperationException("Business beneficiary bank account must be active.");

        if (!string.Equals(account.AssetCode, beneficiaryAccount.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Payout source asset {account.AssetCode} does not match beneficiary account currency {beneficiaryAccount.CurrencyCode}.");

        var currency = account.AssetCode.ToUpperInvariant();
        var paymentMethod = ResolvePayoutMethod(currency);

        var providerCustomer = await _db.ProviderCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == _provider.ProviderCode &&
                x.BusinessCustomerId == businessCustomerId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Business customer has not completed provider onboarding for payouts.");

        if (string.IsNullOrWhiteSpace(providerCustomer.ProviderCustomerId) ||
            IsBlockedProviderCustomerStatus(providerCustomer.ProviderStatus))
            throw new InvalidOperationException("Business customer is not ready for payout with the selected provider.");

        var providerDestination = await _db.PayoutDestinationProviderMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.DestinationType == PayoutDestinationType.BusinessBeneficiaryBankAccount &&
                x.DestinationId == beneficiaryAccount.Id &&
                x.ProviderCode == _provider.ProviderCode &&
                x.IsActive &&
                !x.IsDeleted,
                ct);

        ValidateDestination(paymentMethod, beneficiaryAccount, providerDestination);

        var note = Optional(request.Note, 500);
        var normalized = new
        {
            BusinessCustomerId = businessCustomerId,
            CollectionAccountId = collectionAccountId,
            ExternalReference = externalReference,
            request.BusinessBeneficiaryBankAccountId,
            request.Amount,
            CurrencyCode = currency,
            Note = note
        };

        var hash = Hash(normalized);
        var existingIdem = await _db.EmbeddedApiIdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApiApplicationId == principal.ApiApplicationId &&
                x.IdempotencyKey == key &&
                !x.IsDeleted,
                ct);

        if (existingIdem is not null)
        {
            EnsureSame(existingIdem, hash, nameof(Payout));
            var existing = await _db.Payouts.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == existingIdem.ResourceId &&
                x.Purpose == PaymentOperationPurpose.Withdrawal &&
                x.ContextEntityId == businessCustomerId &&
                !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("Idempotent payout resource was not found.");

            return await ToDtoAsync(existing, ct);
        }

        if (await _db.Payouts.AnyAsync(x =>
                x.Purpose == PaymentOperationPurpose.Withdrawal &&
                x.ContextEntityType == nameof(BusinessCustomer) &&
                x.ContextEntityId == businessCustomerId &&
                x.Reference == externalReference &&
                !x.IsDeleted,
                ct))
            throw new InvalidOperationException(
                "A payout with this external reference already exists for the business customer.");

        var payout = new Payout
        {
            Purpose = PaymentOperationPurpose.Withdrawal,
            FinancialAccountId = account.Id,
            FinancialAccount = account,
            RelatedEntityType = nameof(BusinessBeneficiaryBankAccount),
            RelatedEntityId = beneficiaryAccount.Id,
            ContextEntityType = nameof(BusinessCustomer),
            ContextEntityId = businessCustomerId,
            Reference = externalReference,
            CurrencyCode = currency,
            Amount = request.Amount,
            PaymentMethod = paymentMethod,
            Status = PayoutStatus.Pending,
            ProviderCode = _provider.ProviderName
        };

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _db.Payouts.Add(payout);
            _db.EmbeddedApiIdempotencyRecords.Add(new EmbeddedApiIdempotencyRecord
            {
                ApiApplicationId = principal.ApiApplicationId,
                IdempotencyKey = key,
                RequestHash = hash,
                ResourceType = nameof(Payout),
                ResourceId = payout.Id
            });

            await _reservations.ReserveAsync(
                account.Id,
                FinancialReservationType.Withdrawal,
                nameof(Payout),
                payout.Id,
                request.Amount,
                null,
                nameof(BusinessCustomer),
                businessCustomerId,
                ct);

            await _webhooks.PublishAsync(
                principal.BusinessProfileId,
                "payout.created",
                new
                {
                    id = payout.Id,
                    businessCustomerId,
                    collectionAccountId,
                    financialAccountId = account.Id,
                    externalReference,
                    amount = payout.Amount,
                    currencyCode = payout.CurrencyCode,
                    status = payout.Status.ToString()
                },
                ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            var recovered = await RecoverConcurrentIdempotentPayoutAsync(
                principal.ApiApplicationId, businessCustomerId, key, hash, ct);
            if (recovered is not null) return recovered;
            throw;
        }

        return await DispatchAsync(
            payout.Id,
            customer,
            providerCustomer.ProviderCustomerId,
            beneficiaryAccount,
            providerDestination,
            note,
            ct);
    }

    private async Task<EmbeddedPayoutDto?> RecoverConcurrentIdempotentPayoutAsync(
        Guid apiApplicationId,
        Guid businessCustomerId,
        string idempotencyKey,
        string requestHash,
        CancellationToken ct)
    {
        var idem = await _db.EmbeddedApiIdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ApiApplicationId == apiApplicationId &&
                x.IdempotencyKey == idempotencyKey &&
                !x.IsDeleted,
                ct);

        if (idem is null) return null;

        EnsureSame(idem, requestHash, nameof(Payout));
        var existing = await _db.Payouts.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == idem.ResourceId &&
            x.Purpose == PaymentOperationPurpose.Withdrawal &&
            x.ContextEntityType == nameof(BusinessCustomer) &&
            x.ContextEntityId == businessCustomerId &&
            !x.IsDeleted,
            ct)
            ?? throw new InvalidOperationException(
                "Idempotent payout resource was not found after concurrent creation.");

        return await ToDtoAsync(existing, ct);
    }

    private async Task<EmbeddedPayoutDto> DispatchAsync(
        Guid payoutId,
        BusinessCustomer customer,
        string providerCustomerId,
        BusinessBeneficiaryBankAccount beneficiaryAccount,
        PayoutDestinationProviderMapping? destination,
        string? note,
        CancellationToken ct)
    {
        var payout = await _db.Payouts.Include(x => x.Attempts)
            .FirstAsync(x => x.Id == payoutId && !x.IsDeleted, ct);

        var beneficiary = beneficiaryAccount.BusinessBeneficiary;
        var names = SplitName(beneficiary.Name);
        var firstName = string.IsNullOrWhiteSpace(beneficiary.ContactFirstName)
            ? names.FirstName : beneficiary.ContactFirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(beneficiary.ContactLastName)
            ? names.LastName : beneficiary.ContactLastName.Trim();

        var walletId = ResolveWalletId(payout.CurrencyCode);
        var sanitized = JsonSerializer.Serialize(new
        {
            payoutId = payout.Id,
            payout.Reference,
            businessCustomerId = customer.Id,
            financialAccountId = payout.FinancialAccountId,
            amount = payout.Amount,
            currency = payout.CurrencyCode,
            payout.PaymentMethod,
            beneficiaryId = beneficiary.Id,
            beneficiaryBankAccountId = beneficiaryAccount.Id,
            beneficiaryName = beneficiary.Name,
            bankName = beneficiaryAccount.BankName,
            accountNumber = Mask(beneficiaryAccount.AccountNumber)
        });

        var attempt = new PayoutAttempt
        {
            Payout = payout,
            PayoutId = payout.Id,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = sanitized,
            AttemptedAt = DateTime.UtcNow
        };
        payout.Attempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        try
        {
            if (_outboundFundsRestrictions is not null)
            {
                await _outboundFundsRestrictions.EnsureBusinessCustomerOutboundAllowedAsync(
                    customer.Id,
                    "embedded_finance_payout_provider_dispatch",
                    ct);
            }

            var result = await _provider.InitiatePayoutAsync(
                new RemittancePayoutRequest(
                    null,
                    payout.Id,
                    payout.PaymentMethod,
                    payout.Amount,
                    payout.CurrencyCode,
                    payout.CurrencyCode,
                    providerCustomerId,
                    walletId,
                    firstName,
                    lastName,
                    beneficiary.Email,
                    beneficiary.PhoneNumber,
                    destination?.ProviderBankId ?? beneficiaryAccount.ProviderBankId,
                    beneficiaryAccount.BankName,
                    destination?.ProviderVerifiedAccountName
                        ?? beneficiaryAccount.ProviderVerifiedAccountName
                        ?? beneficiaryAccount.AccountName,
                    beneficiaryAccount.AccountNumber,
                    beneficiaryAccount.RoutingNumber,
                    beneficiaryAccount.SortCode,
                    beneficiaryAccount.Iban,
                    beneficiaryAccount.SwiftBic,
                    note ?? $"KorridorX embedded payout {payout.Reference}"),
                ct);

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var mappedStatus = MapProviderStatus(result.ProviderStatus);
            var changed = _payoutStatus.ApplyTransition(
                payout,
                mappedStatus,
                new PayoutStatusTransitionContext(
                    Source: "EmbeddedApi",
                    Reason: mappedStatus == PayoutStatus.Failed
                        ? $"{_provider.ProviderName} rejected the payout."
                        : $"Payout submitted to {_provider.ProviderName}.",
                    ProviderPayoutId: result.ProviderTransactionId,
                    ProviderReference: result.ProviderReference,
                    InteracQuestion: result.InteracQuestion,
                    InteracAnswer: result.InteracAnswer,
                    ProviderRequestId: result.ProviderRequestLogId.ToString(),
                    ProviderResponseId: result.ProviderTransactionId,
                    RequestPayloadJson: sanitized,
                    ResponsePayloadJson: result.RawResponseJson,
                    OccurredAt: result.ProviderCreatedAt),
                attempt);

            await UpsertProviderTransactionAsync(
                payout,
                result.ProviderTransactionId,
                result.ProviderReference,
                result.ProviderStatus,
                result.CurrencyCode,
                result.Amount,
                result.RawResponseJson,
                result.ProviderCreatedAt,
                ct);

            await _settlement.ApplyStatusEffectAsync(payout, changed, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return await ToDtoAsync(payout, ct);
        }
        catch (ProviderIntegrationException ex)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var changed = _payoutStatus.ApplyTransition(
                payout,
                PayoutStatus.Failed,
                new PayoutStatusTransitionContext(
                    Source: "EmbeddedApi",
                    Reason: ex.Message,
                    ProviderRequestId: ex.RequestLogId?.ToString(),
                    RequestPayloadJson: sanitized,
                    ResponsePayloadJson: ex.ProviderResponse),
                attempt);

            await _settlement.ApplyStatusEffectAsync(payout, changed, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw;
        }
    }

    private async Task UpsertProviderTransactionAsync(
        Payout payout,
        string providerTransactionId,
        string? providerReference,
        string providerStatus,
        string currencyCode,
        decimal amount,
        string rawPayload,
        DateTime? providerCreatedAt,
        CancellationToken ct)
    {
        var row = await _db.ProviderTransactions.FirstOrDefaultAsync(x =>
            x.ProviderCode == _provider.ProviderCode &&
            x.ProviderTransactionId == providerTransactionId, ct);

        if (row is null)
        {
            row = new ProviderTransaction
            {
                ProviderCode = _provider.ProviderCode,
                ProviderTransactionId = providerTransactionId,
                TransactionType = "payout"
            };
            _db.ProviderTransactions.Add(row);
        }

        row.PayoutId = payout.Id;
        row.ProviderReference = providerReference;
        row.ProviderStatus = providerStatus;
        row.CurrencyCode = currencyCode;
        row.Amount = amount;
        row.RawPayloadJson = rawPayload;
        row.ProviderCreatedAt = providerCreatedAt;
        row.LastSyncedAt = DateTime.UtcNow;
        row.LastUpdatedAt = DateTime.UtcNow;
    }

    private async Task<EmbeddedPayoutDto> ToDtoAsync(Payout payout, CancellationToken ct)
    {
        var collectionAccountId = await _db.CollectionAccounts.AsNoTracking()
            .Where(x => x.FinancialAccountId == payout.FinancialAccountId && !x.IsDeleted)
            .Select(x => x.Id)
            .SingleOrDefaultAsync(ct);

        return new EmbeddedPayoutDto(
            payout.Id,
            payout.ContextEntityId ?? Guid.Empty,
            collectionAccountId,
            payout.FinancialAccountId ?? Guid.Empty,
            payout.Reference,
            payout.RelatedEntityId ?? Guid.Empty,
            payout.CurrencyCode,
            payout.Amount,
            payout.PaymentMethod,
            payout.Status,
            payout.ProviderCode,
            payout.ProviderPayoutId,
            payout.ProviderReference,
            payout.FailureReason,
            payout.CreatedAt,
            payout.InitiatedAt,
            payout.CompletedAt,
            payout.FailedAt,
            payout.ReversedAt);
    }

    private EmbeddedFinancePrincipal RequireScope(EmbeddedFinanceScope scope)
    {
        var principal = _context.GetRequiredPrincipal();
        if (!principal.HasScope(scope))
            throw new UnauthorizedAccessException(
                $"API application does not have required scope '{scope}'.");
        return principal;
    }

    private async Task<BusinessCustomer> EnsureCustomerAsync(
        Guid businessProfileId,
        Guid businessCustomerId,
        CancellationToken ct) =>
        await _db.BusinessCustomers.FirstOrDefaultAsync(x =>
            x.Id == businessCustomerId &&
            x.BusinessProfileId == businessProfileId &&
            !x.IsDeleted,
            ct)
        ?? throw new InvalidOperationException("Business customer not found.");

    private string ResolveWalletId(string currencyCode)
    {
        if (!_payoutWalletIds.TryGetValue(currencyCode, out var walletId) ||
            string.IsNullOrWhiteSpace(walletId))
            throw new InvalidOperationException(
                $"No payout wallet is configured for {currencyCode} on {_provider.ProviderName}.");

        return walletId.Trim();
    }

    private static PaymentMethod ResolvePayoutMethod(string currencyCode) =>
        currencyCode.ToUpperInvariant() switch
        {
            "NGN" => PaymentMethod.BankTransfer,
            "CAD" => PaymentMethod.Interac,
            _ => throw new InvalidOperationException(
                $"Embedded payout is not configured for {currencyCode}.")
        };

    private static void ValidateDestination(
        PaymentMethod method,
        BusinessBeneficiaryBankAccount account,
        PayoutDestinationProviderMapping? mapping)
    {
        if (method == PaymentMethod.BankTransfer)
        {
            var providerBankId = mapping?.ProviderBankId ?? account.ProviderBankId;
            var verifiedName = mapping?.ProviderVerifiedAccountName ?? account.ProviderVerifiedAccountName;
            var verified = mapping is not null
                ? mapping.IsActive && mapping.IsVerified
                : account.IsVerified;

            if (string.IsNullOrWhiteSpace(providerBankId) ||
                string.IsNullOrWhiteSpace(account.AccountNumber))
                throw new InvalidOperationException(
                    "A provider bank selection and account number are required for bank payout.");

            if (!verified || string.IsNullOrWhiteSpace(verifiedName))
                throw new InvalidOperationException(
                    "The beneficiary bank account must be verified with the provider before payout.");
        }

        if (method == PaymentMethod.Interac &&
            string.IsNullOrWhiteSpace(account.BusinessBeneficiary.Email))
            throw new InvalidOperationException(
                "Beneficiary email is required for an Interac payout.");
    }

    private static bool IsBlockedProviderCustomerStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Trim().ToUpperInvariant() is "FAILED" or "REJECTED" or "SUSPENDED" or "DISABLED" or "CLOSED" or "BLOCKED";
    }

    private static PayoutStatus MapProviderStatus(string status) =>
        status.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => PayoutStatus.Successful,
            "PROCESSING" => PayoutStatus.Processing,
            "FAILED" or "REJECTED" => PayoutStatus.Failed,
            "REVERSED" => PayoutStatus.Reversed,
            _ => PayoutStatus.Initiated
        };

    private static string NormalizeKey(string value)
    {
        var key = (value ?? "").Trim();
        if (key.Length == 0)
            throw new InvalidOperationException("Idempotency-Key header is required.");
        if (key.Length > 200)
            throw new InvalidOperationException("Idempotency-Key cannot exceed 200 characters.");
        return key;
    }

    private static void EnsureSame(
        EmbeddedApiIdempotencyRecord record,
        string hash,
        string resourceType)
    {
        if (!string.Equals(record.ResourceType, resourceType, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The Idempotency-Key has already been used for a different operation.");

        if (!string.Equals(record.RequestHash, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The Idempotency-Key has already been used with a different request.");
    }

    private static string Hash<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    private static string Required(string value, int max, string label)
    {
        var result = (value ?? "").Trim();
        if (result.Length == 0)
            throw new InvalidOperationException($"{label} is required.");
        if (result.Length > max)
            throw new InvalidOperationException($"{label} cannot exceed {max} characters.");
        return result;
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var result = value.Trim();
        if (result.Length > max)
            throw new InvalidOperationException($"Value cannot exceed {max} characters.");
        return result;
    }

    private static (string FirstName, string LastName) SplitName(string name)
    {
        var parts = name.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return ("Business", "Beneficiary");
        if (parts.Length == 1) return (parts[0], "Beneficiary");
        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Length <= 4 ? "****" : $"***{value[^4..]}";
    }
}
