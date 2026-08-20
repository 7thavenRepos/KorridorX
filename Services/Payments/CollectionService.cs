using System.Text.Json;
using KorridorX.Data;
using KorridorX.Configuration;
using Microsoft.Extensions.Options;
using KorridorX.Dtos.Payments;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Providers.Remittance;
using KorridorX.Exceptions;
using KorridorX.Models.Transfers;
using KorridorX.Services.References;
using KorridorX.Services.Compliance;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Payments;

public class CollectionService : ICollectionService
{
    private readonly AppDbContext _db;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ICollectionPaymentMethodPolicy _paymentMethodPolicy;
    private readonly ICollectionStatusService _collectionStatusService;
    private readonly IRemittanceProvider _remittanceProvider;
    private readonly IComplianceGateService _complianceGateService;
    private readonly IReadOnlyDictionary<string, string> collectionWalletIds;

    public CollectionService(
        AppDbContext db,
        IReferenceGenerator referenceGenerator,
        ICollectionPaymentMethodPolicy paymentMethodPolicy,
        ICollectionStatusService collectionStatusService,
        IRemittanceProvider remittanceProvider,
        IComplianceGateService complianceGateService,
        IOptions<BlaaizOptions> blaaizOptions)
    {
        _db = db;
        _referenceGenerator = referenceGenerator;
        _paymentMethodPolicy = paymentMethodPolicy;
        _collectionStatusService = collectionStatusService;
        _remittanceProvider = remittanceProvider;
        _complianceGateService = complianceGateService;
        collectionWalletIds = blaaizOptions.Value.CollectionWalletIds;
    }

    public async Task<IReadOnlyList<CollectionPaymentMethodDto>> GetAvailablePaymentMethodsAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var transfer = await GetOwnedTransferAsync(userId, transferId, false, ct);

        if (transfer.Status != TransferStatus.PendingPayment)
        {
            throw new InvalidOperationException(
                $"Payment methods are not available while the transfer status is '{transfer.Status}'.");
        }

        var methods = _paymentMethodPolicy.GetSupportedMethods(
            transfer.SourceCountryCode,
            transfer.SourceCurrencyCode);

        if (methods.Count == 0)
        {
            throw new InvalidOperationException(
                $"No collection payment methods are configured for {transfer.SourceCountryCode}/{transfer.SourceCurrencyCode}.");
        }

        return methods
            .Select(ToPaymentMethodDto)
            .ToList();
    }

    public async Task<CollectionDetailsDto> CreateCollectionAsync(
        Guid userId,
        Guid transferId,
        CreateCollectionRequestDto request,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var transfer = await GetOwnedTransferAsync(userId, transferId, true, ct);

        var existingCollection = await _db.Collections
            .Include(x => x.Transfer)
            .ThenInclude(x => x!.CustomerProfile)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                x.TransferId == transfer.Id &&
                !x.IsDeleted,
                ct);

        if (existingCollection is not null)
        {
            if (existingCollection.PaymentMethod != request.PaymentMethod)
            {
                throw new InvalidOperationException(
                    $"A collection already exists for this transfer using '{GetPaymentMethodDisplayName(existingCollection.PaymentMethod)}'.");
            }

            await tx.CommitAsync(ct);
            return ToDetailsDto(existingCollection);
        }

        if (transfer.Status != TransferStatus.PendingPayment)
        {
            throw new InvalidOperationException(
                $"A collection cannot be created while the transfer status is '{transfer.Status}'.");
        }

        if (!_paymentMethodPolicy.IsSupported(
                transfer.SourceCountryCode,
                transfer.SourceCurrencyCode,
                request.PaymentMethod))
        {
            throw new InvalidOperationException(
                $"Payment method '{GetPaymentMethodDisplayName(request.PaymentMethod)}' is not supported for {transfer.SourceCountryCode}/{transfer.SourceCurrencyCode}.");
        }

        var now = DateTime.UtcNow;
        var reference = await GenerateUniqueCollectionReferenceAsync(ct);

        var collection = new Collection
        {
            TransferId = transfer.Id,
            Transfer = transfer,
            Purpose = PaymentOperationPurpose.Remittance,
            RelatedEntityType = nameof(Transfer),
            RelatedEntityId = transfer.Id,
            Reference = reference,
            CurrencyCode = transfer.SourceCurrencyCode,
            Amount = transfer.TotalPayableAmount,
            PaymentMethod = request.PaymentMethod,
            Status = CollectionStatus.Pending,
            ProviderCode = transfer.ProviderCode,
            CreatedByUserId = userId
        };

        var attempt = new CollectionAttempt
        {
            CollectionId = collection.Id,
            Collection = collection,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = JsonSerializer.Serialize(new
            {
                transferId = transfer.Id,
                transferReference = transfer.Reference,
                collectionReference = reference,
                amount = transfer.TotalPayableAmount,
                currencyCode = transfer.SourceCurrencyCode,
                paymentMethod = request.PaymentMethod.ToString()
            }),
            AttemptedAt = now
        };

        collection.Attempts.Add(attempt);

        _db.Collections.Add(collection);
        _db.TransferTimelineEvents.Add(new TransferTimelineEvent
        {
            TransferId = transfer.Id,
            EventType = "COLLECTION_CREATED",
            Title = "Payment method selected",
            Description = $"{GetPaymentMethodDisplayName(request.PaymentMethod)} was selected to fund this transfer.",
            MetadataJson = JsonSerializer.Serialize(new
            {
                collectionId = collection.Id,
                collectionReference = collection.Reference,
                paymentMethod = request.PaymentMethod.ToString()
            }),
            OccurredAt = now
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDetailsDto(collection);
    }

    public async Task<CollectionDetailsDto> InitiateCollectionAsync(
        Guid userId,
        Guid collectionId,
        InitiateCollectionRequestDto request,
        CancellationToken ct = default)
    {
        var collection = await _db.Collections
            .Include(x => x.Transfer)
            .ThenInclude(x => x!.CustomerProfile)
            .ThenInclude(x => x!.User)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                x.Id == collectionId &&
                x.Purpose == PaymentOperationPurpose.Remittance &&
                x.TransferId != null &&
                x.Transfer != null &&
                x.Transfer.CustomerProfile != null &&
                x.Transfer.CustomerProfile.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile.IsDeleted,
                ct);

        if (collection is null)
        {
            throw new InvalidOperationException("Collection not found.");
        }

        if (collection.Status is CollectionStatus.Initiated or
            CollectionStatus.Processing or
            CollectionStatus.Successful)
        {
            return ToDetailsDto(collection);
        }

        var transfer = collection.Transfer
            ?? throw new InvalidOperationException("Remittance collection is missing its transfer.");

        if (collection.Status is not CollectionStatus.Pending and
            not CollectionStatus.Failed and
            not CollectionStatus.Expired)
        {
            throw new InvalidOperationException(
                $"Collection cannot be initiated while its status is '{collection.Status}'.");
        }

        if (transfer.Status != TransferStatus.PendingPayment)
        {
            throw new InvalidOperationException(
                $"Collection cannot be initiated while the transfer status is '{transfer.Status}'.");
        }

        if (collection.PaymentMethod is not PaymentMethod.Card and not PaymentMethod.Interac)
        {
            throw new InvalidOperationException(
                $"Live provider initiation is not yet supported for '{collection.PaymentMethod}'.");
        }

        var profile = transfer.CustomerProfile
            ?? throw new InvalidOperationException("Individual customer profile not found for this collection.");

        var complianceGate = await _complianceGateService.EnsureCanInitiateMoneyMovementAsync(
            profile.Id,
            _remittanceProvider.ProviderCode,
            ct);

        var email = request.PayerEmail ?? profile.Email ?? profile.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Payer email is required to initiate this collection.");
        }

        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? $"{profile.FirstName} {profile.LastName}".Trim()
            : request.CustomerName.Trim();

        var walletId = ResolveCollectionWalletId(collection.CurrencyCode);
        var cardRequest = request.Card;
        var card = cardRequest is null
            ? null
            : new RemittanceCardDetails(
                cardRequest.CardHolderName,
                cardRequest.CardNumber,
                cardRequest.Expiry,
                cardRequest.Cvc);

        var sanitizedRequestPayload = JsonSerializer.Serialize(new
        {
            collectionId = collection.Id,
            collectionReference = collection.Reference,
            transferId = collection.TransferId,
            transferReference = transfer.Reference,
            amount = collection.Amount,
            currencyCode = collection.CurrencyCode,
            paymentMethod = collection.PaymentMethod.ToString(),
            payerEmail = email,
            customerName,
            interacExpiryHours = request.InteracExpiryHours,
            redirectUrl = request.RedirectUrl,
            card = card is null ? null : new
            {
                cardHolderName = card.CardHolderName,
                cardNumber = MaskCardNumber(card.CardNumber),
                expiry = card.Expiry,
                cvc = "***REDACTED***"
            }
        });

        var attempt = new CollectionAttempt
        {
            CollectionId = collection.Id,
            Collection = collection,
            Status = ProviderRequestStatus.Pending,
            RequestPayloadJson = sanitizedRequestPayload,
            AttemptedAt = DateTime.UtcNow
        };

        collection.Attempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        try
        {
            var providerResult = await _remittanceProvider.InitiateCollectionAsync(
                new RemittanceCollectionRequest(
                    collection.TransferId ?? throw new InvalidOperationException("Remittance collection is missing its transfer."),
                    collection.Id,
                    collection.PaymentMethod,
                    collection.Amount,
                    collection.CurrencyCode,
                    complianceGate.ProviderCustomerId,
                    email,
                    customerName,
                    profile.PhoneNumber,
                    walletId,
                    request.RedirectUrl,
                    card,
                    request.InteracExpiryHours),
                ct);

            _collectionStatusService.ApplyTransition(
                collection,
                CollectionStatus.Initiated,
                new CollectionStatusTransitionContext(
                    Source: "Provider",
                    Reason: "Collection was initiated with Blaaiz.",
                    ChangedByUserId: userId,
                    ProviderCollectionId: providerResult.ProviderTransactionId,
                    ProviderReference: providerResult.ProviderReference,
                    CheckoutUrl: providerResult.CheckoutUrl,
                    ProviderExpiresAt: providerResult.ExpiresAt,
                    ProviderRequestId: providerResult.ProviderRequestLogId.ToString(),
                    ProviderResponseId: providerResult.ProviderTransactionId,
                    RequestPayloadJson: sanitizedRequestPayload,
                    ResponsePayloadJson: providerResult.RawResponseJson,
                    MetadataJson: JsonSerializer.Serialize(new
                    {
                        provider = _remittanceProvider.ProviderName,
                        providerStatus = providerResult.ProviderStatus,
                        providerExpiresAt = providerResult.ExpiresAt
                    })),
                attempt);

            await UpsertProviderTransactionAsync(
                collection,
                providerResult,
                ct);

            await _db.SaveChangesAsync(ct);
            return ToDetailsDto(collection);
        }
        catch (InvalidOperationException ex)
        {
            attempt.Status = ProviderRequestStatus.Failed;
            attempt.ErrorMessage = ex.Message.Length <= 1000
                ? ex.Message
                : ex.Message[..1000];
            attempt.LastUpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }
        catch (ProviderIntegrationException ex)
        {
            _collectionStatusService.ApplyTransition(
                collection,
                CollectionStatus.Failed,
                new CollectionStatusTransitionContext(
                    Source: "Provider",
                    Reason: ex.Message,
                    ChangedByUserId: userId,
                    ProviderRequestId: ex.RequestLogId?.ToString(),
                    RequestPayloadJson: sanitizedRequestPayload,
                    ResponsePayloadJson: ex.ProviderResponse),
                attempt);

            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<CollectionDetailsDto> GetCollectionByIdAsync(
        Guid userId,
        Guid collectionId,
        CancellationToken ct = default)
    {
        var collection = await BuildOwnedCollectionQuery(userId)
            .FirstOrDefaultAsync(x => x.Id == collectionId, ct);

        if (collection is null)
        {
            throw new InvalidOperationException("Collection not found.");
        }

        return ToDetailsDto(collection);
    }

    public async Task<CollectionDetailsDto> GetTransferCollectionAsync(
        Guid userId,
        Guid transferId,
        CancellationToken ct = default)
    {
        var collection = await BuildOwnedCollectionQuery(userId)
            .FirstOrDefaultAsync(x => x.TransferId == transferId, ct);

        if (collection is null)
        {
            throw new InvalidOperationException("No collection has been created for this transfer.");
        }

        return ToDetailsDto(collection);
    }

    public async Task<PagedResult<CollectionDto>> GetMyCollectionsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        return await _db.Collections
            .AsNoTracking()
            .Where(x =>
                x.Purpose == PaymentOperationPurpose.Remittance &&
                x.TransferId != null &&
                x.Transfer != null &&
                x.Transfer.CustomerProfileId != null &&
                x.Transfer.CustomerProfile!.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile!.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CollectionDto
            {
                Id = x.Id,
                TransferId = x.TransferId,
                Purpose = x.Purpose,
                FinancialAccountId = x.FinancialAccountId,
                RelatedEntityType = x.RelatedEntityType,
                RelatedEntityId = x.RelatedEntityId,
                ContextEntityType = x.ContextEntityType,
                ContextEntityId = x.ContextEntityId,
                TransferReference = x.Transfer != null ? x.Transfer.Reference : null,
                TransferStatus = x.Transfer != null ? x.Transfer.Status : null,
                Reference = x.Reference,
                SourceCountryCode = x.Transfer != null ? x.Transfer.SourceCountryCode : null,
                CurrencyCode = x.CurrencyCode,
                Amount = x.Amount,
                PaymentMethod = x.PaymentMethod,
                Status = x.Status,
                ProviderCode = x.ProviderCode,
                ProviderCollectionId = x.ProviderCollectionId,
                ProviderReference = x.ProviderReference,
                CheckoutUrl = x.CheckoutUrl,
                ProviderExpiresAt = x.ProviderExpiresAt,
                VirtualAccountNumber = x.VirtualAccountNumber,
                VirtualAccountBankName = x.VirtualAccountBankName,
                VirtualAccountName = x.VirtualAccountName,
                InitiatedAt = x.InitiatedAt,
                ConfirmedAt = x.ConfirmedAt,
                FailedAt = x.FailedAt,
                ExpiredAt = x.ExpiredAt,
                RefundInitiatedAt = x.RefundInitiatedAt,
                RefundedAt = x.RefundedAt,
                ProviderRefundId = x.ProviderRefundId,
                ProviderRefundReference = x.ProviderRefundReference,
                RefundReason = x.RefundReason,
                RefundFailureReason = x.RefundFailureReason,
                LastRefundSyncedAt = x.LastRefundSyncedAt,
                FailureReason = x.FailureReason,
                CreatedAt = x.CreatedAt,
                LastUpdatedAt = x.LastUpdatedAt
            })
            .PaginateAsync(page, pageSize, ct);
    }

    private async Task<Transfer> GetOwnedTransferAsync(
        Guid userId,
        Guid transferId,
        bool tracked,
        CancellationToken ct)
    {
        IQueryable<Transfer> query = _db.Transfers
            .Include(x => x.CustomerProfile);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        var transfer = await query.FirstOrDefaultAsync(x =>
            x.Id == transferId &&
            x.CustomerProfileId != null &&
            x.CustomerProfile!.UserId == userId &&
            !x.IsDeleted &&
            !x.CustomerProfile!.IsDeleted,
            ct);

        if (transfer is null)
        {
            throw new InvalidOperationException("Transfer not found.");
        }

        return transfer;
    }

    private IQueryable<Collection> BuildOwnedCollectionQuery(Guid userId)
    {
        return _db.Collections
            .AsNoTracking()
            .Include(x => x.Transfer)
            .ThenInclude(x => x!.CustomerProfile)
            .Include(x => x.Attempts)
            .Where(x =>
                x.Purpose == PaymentOperationPurpose.Remittance &&
                x.TransferId != null &&
                x.Transfer != null &&
                x.Transfer.CustomerProfileId != null &&
                x.Transfer.CustomerProfile!.UserId == userId &&
                !x.IsDeleted &&
                !x.Transfer.IsDeleted &&
                !x.Transfer.CustomerProfile!.IsDeleted);
    }

    private string? ResolveCollectionWalletId(string currencyCode)
    {
        if (collectionWalletIds.TryGetValue(currencyCode, out var walletId) &&
            !string.IsNullOrWhiteSpace(walletId))
        {
            return walletId.Trim();
        }

        return null;
    }

    private async Task UpsertProviderTransactionAsync(
        Collection collection,
        RemittanceCollectionResult providerResult,
        CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == _remittanceProvider.ProviderCode &&
                x.ProviderTransactionId == providerResult.ProviderTransactionId,
                ct);

        if (transaction is null)
        {
            transaction = new ProviderTransaction
            {
                ProviderCode = _remittanceProvider.ProviderCode,
                TransferId = collection.TransferId,
                CollectionId = collection.Id,
                ProviderTransactionId = providerResult.ProviderTransactionId,
                ProviderReference = providerResult.ProviderReference,
                TransactionType = "Collection",
                ProviderStatus = providerResult.ProviderStatus,
                CurrencyCode = collection.CurrencyCode,
                Amount = collection.Amount,
                RawPayloadJson = providerResult.RawResponseJson,
                LastSyncedAt = DateTime.UtcNow
            };

            _db.ProviderTransactions.Add(transaction);
            return;
        }

        transaction.ProviderReference = providerResult.ProviderReference;
        transaction.ProviderStatus = providerResult.ProviderStatus;
        transaction.RawPayloadJson = providerResult.RawResponseJson;
        transaction.LastSyncedAt = DateTime.UtcNow;
        transaction.LastUpdatedAt = DateTime.UtcNow;
    }

    private static string MaskCardNumber(string cardNumber)
    {
        var digits = new string(cardNumber.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? $"************{digits[^4..]}" : "****";
    }

    private async Task<string> GenerateUniqueCollectionReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var reference = _referenceGenerator.GenerateCollectionReference();

            var exists = await _db.Collections
                .AsNoTracking()
                .AnyAsync(x => x.Reference == reference, ct);

            if (!exists)
            {
                return reference;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique collection reference.");
    }

    private static CollectionDetailsDto ToDetailsDto(Collection collection)
    {
        return new CollectionDetailsDto(
            ToDto(collection),
            collection.Attempts
                .OrderByDescending(x => x.AttemptedAt)
                .Select(ToAttemptDto)
                .ToList());
    }

    private static CollectionDto ToDto(Collection collection)
    {
        return new CollectionDto
        {
            Id = collection.Id,
            TransferId = collection.TransferId,
            Purpose = collection.Purpose,
            FinancialAccountId = collection.FinancialAccountId,
            RelatedEntityType = collection.RelatedEntityType,
            RelatedEntityId = collection.RelatedEntityId,
            ContextEntityType = collection.ContextEntityType,
            ContextEntityId = collection.ContextEntityId,
            TransferReference = collection.Transfer?.Reference,
            TransferStatus = collection.Transfer?.Status,
            Reference = collection.Reference,
            SourceCountryCode = collection.Transfer?.SourceCountryCode,
            CurrencyCode = collection.CurrencyCode,
            Amount = collection.Amount,
            PaymentMethod = collection.PaymentMethod,
            Status = collection.Status,
            ProviderCode = collection.ProviderCode,
            ProviderCollectionId = collection.ProviderCollectionId,
            ProviderReference = collection.ProviderReference,
            CheckoutUrl = collection.CheckoutUrl,
            ProviderExpiresAt = collection.ProviderExpiresAt,
            VirtualAccountNumber = collection.VirtualAccountNumber,
            VirtualAccountBankName = collection.VirtualAccountBankName,
            VirtualAccountName = collection.VirtualAccountName,
            InitiatedAt = collection.InitiatedAt,
            ConfirmedAt = collection.ConfirmedAt,
            FailedAt = collection.FailedAt,
            ExpiredAt = collection.ExpiredAt,
            RefundInitiatedAt = collection.RefundInitiatedAt,
            RefundedAt = collection.RefundedAt,
            ProviderRefundId = collection.ProviderRefundId,
            ProviderRefundReference = collection.ProviderRefundReference,
            RefundReason = collection.RefundReason,
            RefundFailureReason = collection.RefundFailureReason,
            LastRefundSyncedAt = collection.LastRefundSyncedAt,
            FailureReason = collection.FailureReason,
            CreatedAt = collection.CreatedAt,
            LastUpdatedAt = collection.LastUpdatedAt
        };
    }

    private static CollectionAttemptDto ToAttemptDto(CollectionAttempt attempt)
    {
        return new CollectionAttemptDto(
            attempt.Id,
            attempt.CollectionId,
            attempt.Status,
            attempt.ProviderRequestId,
            attempt.ProviderResponseId,
            attempt.RequestPayloadJson,
            attempt.ResponsePayloadJson,
            attempt.AttemptedAt,
            attempt.ErrorMessage);
    }

    private static CollectionPaymentMethodDto ToPaymentMethodDto(PaymentMethod paymentMethod)
    {
        return new CollectionPaymentMethodDto(
            paymentMethod,
            paymentMethod.ToString(),
            GetPaymentMethodDisplayName(paymentMethod));
    }

    private static string GetPaymentMethodDisplayName(PaymentMethod paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethod.Ach => "ACH",
            PaymentMethod.Sepa => "SEPA",
            PaymentMethod.Interac => "Interac",
            PaymentMethod.BankTransfer => "Bank transfer",
            PaymentMethod.MobileMoney => "Mobile money",
            PaymentMethod.VirtualAccount => "Virtual account",
            _ => paymentMethod.ToString()
        };
    }
}
