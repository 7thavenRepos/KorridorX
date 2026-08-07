using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Models.Webhooks;
using KorridorX.Providers.Remittance;
using KorridorX.Services.Payments;
using KorridorX.Services.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Webhooks;

public class BlaaizWebhookService : IBlaaizWebhookService
{
    private readonly AppDbContext _db;
    private readonly BlaaizOptions _options;
    private readonly ICollectionStatusService _collectionStatusService;
    private readonly IPayoutStatusService _payoutStatusService;
    private readonly IRemittanceProvider _provider;
    private readonly IComplianceScreeningService _screeningService;

    public BlaaizWebhookService(
        AppDbContext db,
        IOptions<BlaaizOptions> options,
        ICollectionStatusService collectionStatusService,
        IPayoutStatusService payoutStatusService,
        IRemittanceProvider provider,
        IComplianceScreeningService screeningService)
    {
        _db = db;
        _options = options.Value;
        _collectionStatusService = collectionStatusService;
        _payoutStatusService = payoutStatusService;
        _provider = provider;
        _screeningService = screeningService;
    }

    public Task<BlaaizWebhookResult> ProcessCollectionWebhookAsync(
        string rawPayload,
        string? signature,
        string? timestamp,
        CancellationToken ct = default) =>
        ProcessWebhookAsync(rawPayload, signature, timestamp, "collection", ct);

    public Task<BlaaizWebhookResult> ProcessPayoutWebhookAsync(
        string rawPayload,
        string? signature,
        string? timestamp,
        CancellationToken ct = default) =>
        ProcessWebhookAsync(rawPayload, signature, timestamp, "payout", ct);

    private async Task<BlaaizWebhookResult> ProcessWebhookAsync(
        string rawPayload,
        string? signature,
        string? timestamp,
        string expectedChannel,
        CancellationToken ct)
    {
        if (!_options.IsEnabled)
        {
            throw new InvalidOperationException("Blaaiz integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw new InvalidOperationException("Blaaiz webhook payload is empty.");
        }

        ValidateSignature(rawPayload, signature, timestamp);

        using var document = JsonDocument.Parse(rawPayload);
        var root = document.RootElement;
        var data = GetDataNode(root);

        var providerStatus = ReadOptionalString(data, root, "transaction_status", "status", "new_status");
        var type = ReadOptionalString(data, root, "type", "transaction_type");
        var eventType = ReadOptionalString(root, data, "event_type", "event")
            ?? InferEventType(type, providerStatus, expectedChannel);
        var eventId = ReadOptionalString(root, data, "event_id")
            ?? CreateDeterministicEventId(eventType, rawPayload);

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
            var processed = await RouteEventAsync(eventType, root, data, rawPayload, ct);
            webhook.ProcessingStatus = processed
                ? WebhookProcessingStatus.Processed
                : WebhookProcessingStatus.Ignored;

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

    private async Task<bool> RouteEventAsync(
        string eventType,
        JsonElement root,
        JsonElement data,
        string rawPayload,
        CancellationToken ct)
    {
        if (eventType.Equals("customer.status_changed", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessCustomerStatusChangedAsync(root, data, ct);
        }

        if (eventType.StartsWith("collection.", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("refund", StringComparison.OrdinalIgnoreCase) ||
            eventType.Equals("refund_initiated", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessCollectionEventAsync(eventType, root, data, rawPayload, ct);
        }

        if (eventType.StartsWith("payout.", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessPayoutEventAsync(eventType, root, data, rawPayload, ct);
        }

        return false;
    }

    private async Task<bool> ProcessCollectionEventAsync(
        string eventType,
        JsonElement root,
        JsonElement data,
        string rawPayload,
        CancellationToken ct)
    {
        var isRefund = eventType.Equals("refund", StringComparison.OrdinalIgnoreCase) ||
                       eventType.Equals("refund_initiated", StringComparison.OrdinalIgnoreCase);

        // For refund webhooks transaction_id identifies the original collection,
        // while refund_id identifies the provider refund transaction itself.
        var collectionTransactionId = ReadTransactionId(
            data,
            root,
            "transaction_id",
            "provider_transaction_id",
            "collection_id");

        var refundId = isRefund
            ? ReadOptionalString(data, root, "refund_id")
            : null;

        var collectionReference = isRefund
            ? ReadOptionalString(data, root, "collection_reference")
            : ReadOptionalString(data, root, "transaction_reference", "reference");

        var providerReference = isRefund
            ? ReadOptionalString(data, root, "refund_reference", "reference")
            : collectionReference;

        if (string.IsNullOrWhiteSpace(collectionTransactionId) &&
            string.IsNullOrWhiteSpace(collectionReference) &&
            string.IsNullOrWhiteSpace(refundId))
        {
            throw new InvalidOperationException(
                isRefund
                    ? "Refund webhook is missing collection or refund identification."
                    : "Collection webhook is missing transaction identification.");
        }

        var collection = await _db.Collections
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                ((!string.IsNullOrWhiteSpace(collectionTransactionId) &&
                  x.ProviderCollectionId == collectionTransactionId) ||
                 (!string.IsNullOrWhiteSpace(collectionReference) &&
                  x.ProviderReference == collectionReference) ||
                 (!string.IsNullOrWhiteSpace(refundId) &&
                  x.ProviderRefundId == refundId)),
                ct);

        if (collection is null)
        {
            return false;
        }

        var providerStatus = ReadOptionalString(data, root, "transaction_status", "status")
            ?? EventStatus(eventType);
        var mappedStatus = MapCollectionStatus(eventType, providerStatus);
        var amount = ReadOptionalDecimal(data, root, "transaction_amount", "amount");
        var currency = ReadOptionalString(data, root, "transaction_currency", "currency");
        var failureReason = ReadOptionalString(data, root, "failure_reason", "reason", "message");
        var occurredAt = ReadOptionalDateTime(data, root, "updated_at", "date", "timestamp")
            ?? DateTime.UtcNow;

        if (mappedStatus is CollectionStatus.Successful or
            CollectionStatus.Refunded or
            CollectionStatus.RefundFailed)
        {
            ValidateMoney(collection.Amount, collection.CurrencyCode, amount, currency);
        }

        if (isRefund)
        {
            collection.ProviderRefundId = refundId ?? collection.ProviderRefundId;
            collection.ProviderRefundReference = providerReference ?? collection.ProviderRefundReference;
            collection.RefundFailureReason = mappedStatus == CollectionStatus.RefundFailed
                ? failureReason ?? "The provider refund failed."
                : null;
            collection.LastRefundSyncedAt = DateTime.UtcNow;
        }

        var latestAttempt = collection.Attempts.OrderByDescending(x => x.AttemptedAt).FirstOrDefault();
        var metadata = JsonSerializer.Serialize(new
        {
            webhook = true,
            eventType,
            providerStatus,
            collectionTransactionId,
            collectionReference,
            refundId,
            providerReference
        });

        if (collection.Status == mappedStatus ||
            _collectionStatusService.CanTransition(collection.Status, mappedStatus))
        {
            _collectionStatusService.ApplyTransition(
                collection,
                mappedStatus,
                new CollectionStatusTransitionContext(
                    Source: "Webhook",
                    Reason: failureReason ?? $"Blaaiz reported {providerStatus}.",
                    ProviderCollectionId: isRefund
                        ? collection.ProviderCollectionId
                        : collectionTransactionId,
                    ProviderReference: isRefund
                        ? collection.ProviderReference
                        : providerReference,
                    ProviderResponseId: isRefund ? refundId : collectionTransactionId,
                    ResponsePayloadJson: rawPayload,
                    MetadataJson: metadata,
                    OccurredAt: occurredAt),
                isRefund ? null : latestAttempt);
        }

        var providerTransactionId = isRefund ? refundId : collectionTransactionId;
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            await UpsertProviderTransactionAsync(
                collection.TransferId,
                collection.Id,
                null,
                providerTransactionId,
                providerReference,
                isRefund ? "refund" : "collection",
                providerStatus,
                currency ?? collection.CurrencyCode,
                amount ?? collection.Amount,
                rawPayload,
                occurredAt,
                ct);
        }

        return true;
    }

    private async Task<bool> ProcessPayoutEventAsync(
        string eventType,
        JsonElement root,
        JsonElement data,
        string rawPayload,
        CancellationToken ct)
    {
        var transactionId = ReadTransactionId(data, root,
            "transaction_id", "provider_transaction_id", "payout_id");
        var reference = ReadOptionalString(data, root,
            "transaction_reference", "reference");

        if (string.IsNullOrWhiteSpace(transactionId) && string.IsNullOrWhiteSpace(reference))
        {
            throw new InvalidOperationException("Payout webhook is missing transaction identification.");
        }

        var payout = await _db.Payouts
            .Include(x => x.Transfer)
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x =>
                !x.IsDeleted &&
                ((!string.IsNullOrWhiteSpace(transactionId) && x.ProviderPayoutId == transactionId) ||
                 (!string.IsNullOrWhiteSpace(reference) && x.ProviderReference == reference)),
                ct);

        if (payout is null)
        {
            return false;
        }

        var providerStatus = ReadOptionalString(data, root, "transaction_status", "status")
            ?? EventStatus(eventType);
        var mappedStatus = MapPayoutStatus(eventType, providerStatus);
        var amount = ReadRecipientAmount(data) ?? ReadOptionalDecimal(data, root, "transaction_amount", "amount");
        var currency = ReadRecipientString(data, "currency")
            ?? ReadOptionalString(data, root, "transaction_currency", "currency");
        var failureReason = ReadOptionalString(data, root, "failure_reason", "reason", "message");
        var occurredAt = ReadOptionalDateTime(data, root, "updated_at", "date", "timestamp")
            ?? DateTime.UtcNow;

        if (mappedStatus == PayoutStatus.Successful)
        {
            ValidateMoney(payout.Amount, payout.CurrencyCode, amount, currency);
        }

        var latestAttempt = payout.Attempts.OrderByDescending(x => x.AttemptedAt).FirstOrDefault();
        var metadata = JsonSerializer.Serialize(new
        {
            webhook = true,
            eventType,
            providerStatus,
            transactionId,
            reference
        });

        if (payout.Status == mappedStatus ||
            _payoutStatusService.CanTransition(payout.Status, mappedStatus))
        {
            _payoutStatusService.ApplyTransition(
                payout,
                mappedStatus,
                new PayoutStatusTransitionContext(
                    Source: "Webhook",
                    Reason: failureReason ?? $"Blaaiz reported {providerStatus}.",
                    ProviderPayoutId: transactionId,
                    ProviderReference: reference,
                    ProviderResponseId: transactionId,
                    ResponsePayloadJson: rawPayload,
                    MetadataJson: metadata,
                    OccurredAt: occurredAt),
                latestAttempt);
        }

        payout.Transfer.ProviderTransferId = transactionId ?? payout.Transfer.ProviderTransferId;
        payout.Transfer.ProviderReference = reference ?? payout.Transfer.ProviderReference;

        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            await UpsertProviderTransactionAsync(
                payout.TransferId,
                null,
                payout.Id,
                transactionId,
                reference,
                "payout",
                providerStatus,
                currency ?? payout.CurrencyCode,
                amount ?? payout.Amount,
                rawPayload,
                occurredAt,
                ct);
        }

        return true;
    }

    private async Task<bool> ProcessCustomerStatusChangedAsync(
        JsonElement root,
        JsonElement data,
        CancellationToken ct)
    {
        var providerCustomerId = ReadRequiredString(data, root, "customer_id", "id");
        var providerStatus = ReadRequiredString(data, root, "new_status", "verification_status", "status")
            .ToUpperInvariant();
        var comment = ReadOptionalString(data, root, "comment", "rejection_reason");
        var updatedAt = ReadOptionalDateTime(data, root, "updated_at", "timestamp") ?? DateTime.UtcNow;

        var providerCustomer = await _db.ProviderCustomers
            .Include(x => x.CustomerProfile)
            .Include(x => x.BusinessProfile)
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == ProviderCode.Blaaiz &&
                x.ProviderCustomerId == providerCustomerId &&
                !x.IsDeleted,
                ct);

        if (providerCustomer is null)
        {
            return false;
        }

        if (providerCustomer.BusinessProfileId.HasValue && providerCustomer.BusinessProfile is not null)
        {
            return await ProcessBusinessCustomerStatusAsync(
                providerCustomer,
                providerStatus,
                comment,
                updatedAt,
                ct);
        }

        if (providerCustomer.CustomerProfileId.HasValue && providerCustomer.CustomerProfile is not null)
        {
            return await ProcessIndividualCustomerStatusAsync(
                providerCustomer,
                providerStatus,
                comment,
                updatedAt,
                ct);
        }

        return false;
    }

    private async Task<bool> ProcessIndividualCustomerStatusAsync(
        ProviderCustomer providerCustomer,
        string providerStatus,
        string? comment,
        DateTime updatedAt,
        CancellationToken ct)
    {
        var customerProfileId = providerCustomer.CustomerProfileId!.Value;
        var customerProfile = providerCustomer.CustomerProfile!;

        var kycProfile = await _db.KycProfiles
            .Include(x => x.Applications)
            .ThenInclude(x => x.Documents)
            .FirstOrDefaultAsync(x =>
                x.CustomerProfileId == customerProfileId &&
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

        var status = MapProviderKycStatus(providerStatus);
        var now = DateTime.UtcNow;

        providerCustomer.ProviderStatus = providerStatus;
        providerCustomer.LastSyncedAt = now;
        providerCustomer.LastUpdatedAt = now;

        kycProfile.Status = status;
        kycProfile.RejectionReason = status == KycStatus.Rejected ? comment : null;
        kycProfile.LastUpdatedAt = now;

        customerProfile.KycStatus = status;
        customerProfile.KycApprovedAt = status == KycStatus.Approved ? updatedAt : null;
        customerProfile.LastUpdatedAt = now;

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
            application.ReviewedAt = status is KycStatus.Approved or KycStatus.Rejected ? updatedAt : null;
            application.ReviewNote = status == KycStatus.Rejected ? comment : null;
            application.LastUpdatedAt = now;

            foreach (var kycDocument in application.Documents.Where(x => !x.IsDeleted))
            {
                if (status == KycStatus.Rejected)
                {
                    kycDocument.IsAttachedToProvider = false;
                    kycDocument.RejectionReason = comment;
                }
                else if (status == KycStatus.Approved)
                {
                    kycDocument.RejectionReason = null;
                }

                kycDocument.LastUpdatedAt = now;
            }
        }

        if (status == KycStatus.Approved)
        {
            await _screeningService.ScreenCustomerAsync(
                customerProfileId,
                ScreeningReason.Onboarding,
                null,
                null,
                ct);
        }

        return true;
    }

    private async Task<bool> ProcessBusinessCustomerStatusAsync(
        ProviderCustomer providerCustomer,
        string providerStatus,
        string? comment,
        DateTime updatedAt,
        CancellationToken ct)
    {
        var businessProfileId = providerCustomer.BusinessProfileId!.Value;
        var businessProfile = providerCustomer.BusinessProfile!;
        var status = MapProviderKybStatus(providerStatus);
        var now = DateTime.UtcNow;

        var application = await _db.BusinessKybApplications
            .Include(x => x.Owners)
            .Include(x => x.Documents)
            .Where(x => x.BusinessProfileId == businessProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        RemittanceBusinessCustomerResult? providerSnapshot = null;
        if (application is not null &&
            providerStatus is "VERIFIED" or "REJECTED" or "PROCESSING")
        {
            providerSnapshot = await _provider.GetBusinessCustomerAsync(
                businessProfileId,
                providerCustomer.ProviderCustomerId,
                ct);
        }

        providerCustomer.ProviderStatus = providerSnapshot?.ProviderStatus ?? providerStatus;
        providerCustomer.MetadataJson = providerSnapshot?.RawResponseJson ?? providerCustomer.MetadataJson;
        providerCustomer.LastSyncedAt = now;
        providerCustomer.LastUpdatedAt = now;

        businessProfile.KybStatus = status;
        businessProfile.KybApprovedAt = status == KybStatus.Approved ? updatedAt : null;
        businessProfile.KybRejectedAt = status == KybStatus.Rejected ? updatedAt : null;
        businessProfile.KybRejectionReason = status == KybStatus.Rejected ? comment : null;
        businessProfile.LastUpdatedAt = now;

        if (application is null)
        {
            return true;
        }

        application.Status = status;
        application.ReviewedAt = status is KybStatus.Approved or KybStatus.Rejected ? updatedAt : null;
        application.ReviewNote = status == KybStatus.Rejected ? comment : null;
        application.ProviderResponseJson = providerSnapshot?.RawResponseJson ?? application.ProviderResponseJson;
        application.LastUpdatedAt = now;

        if (providerSnapshot is { } resolvedProviderSnapshot)
        {
            ApplyBusinessProviderSnapshot(application, resolvedProviderSnapshot);
        }

        if (status == KybStatus.Rejected)
        {
            var rejectionMessages = new List<string>();
            if (!string.IsNullOrWhiteSpace(comment)) rejectionMessages.Add(comment);

            rejectionMessages.AddRange(application.Owners
                .Where(x => !x.IsDeleted && string.Equals(x.ProviderStatus, "REJECTED", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => ReadAdminComments(x.ProviderAdminCommentsJson)));

            rejectionMessages.AddRange(application.Documents
                .Where(x => !x.IsDeleted && string.Equals(x.ProviderStatus, "REJECTED", StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => ReadAdminComments(x.ProviderAdminCommentsJson)));

            var distinctMessages = rejectionMessages
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctMessages.Count > 0)
            {
                var combined = string.Join(" | ", distinctMessages);
                application.ReviewNote = combined;
                businessProfile.KybRejectionReason = combined;
            }
        }
        else if (status == KybStatus.Approved)
        {
            businessProfile.KybRejectionReason = null;
            application.ReviewNote = null;
            await _screeningService.ScreenBusinessAsync(
                businessProfileId,
                ScreeningReason.Onboarding,
                null,
                null,
                ct);
        }

        return true;
    }

    private static void ApplyBusinessProviderSnapshot(
        BusinessKybApplication application,
        RemittanceBusinessCustomerResult snapshot)
    {
        application.ProviderApplicationId = snapshot.ProviderCustomerId;

        foreach (var providerOwner in snapshot.Owners)
        {
            var owner = application.Owners.FirstOrDefault(x =>
                !x.IsDeleted &&
                !string.IsNullOrWhiteSpace(x.ProviderOwnerId) &&
                x.ProviderOwnerId == providerOwner.ProviderOwnerId);

            if (owner is null) continue;

            owner.ProviderStatus = providerOwner.ProviderStatus;
            owner.ProviderAdminCommentsJson = providerOwner.AdminCommentsJson;
            owner.RejectionReason = string.Equals(providerOwner.ProviderStatus, "REJECTED", StringComparison.OrdinalIgnoreCase)
                ? FirstAdminComment(providerOwner.AdminCommentsJson)
                : null;
            owner.LastUpdatedAt = DateTime.UtcNow;
        }

        foreach (var providerDocument in snapshot.Documents)
        {
            var document = application.Documents.FirstOrDefault(x =>
                !x.IsDeleted &&
                ((!string.IsNullOrWhiteSpace(x.ProviderDocumentId) &&
                  x.ProviderDocumentId == providerDocument.ProviderDocumentId) ||
                 (x.DocumentType == MapBusinessDocumentType(providerDocument.DocumentType) &&
                  string.Equals(x.Name, providerDocument.Name, StringComparison.OrdinalIgnoreCase))));

            if (document is null) continue;

            document.ProviderDocumentId = providerDocument.ProviderDocumentId;
            document.ProviderStatus = providerDocument.ProviderStatus;
            document.ProviderAdminCommentsJson = providerDocument.AdminCommentsJson;
            document.RejectionReason = string.Equals(providerDocument.ProviderStatus, "REJECTED", StringComparison.OrdinalIgnoreCase)
                ? FirstAdminComment(providerDocument.AdminCommentsJson)
                : null;
            document.LastUpdatedAt = DateTime.UtcNow;
        }
    }

    private static BusinessKybDocumentType MapBusinessDocumentType(string providerType) =>
        providerType.Trim().ToUpperInvariant() switch
        {
            "CERTIFICATE_OF_INCORPORATION" => BusinessKybDocumentType.CertificateOfIncorporation,
            "ARTICLES_OF_INCORPORATION" => BusinessKybDocumentType.ArticlesOfIncorporation,
            "BENEFICIAL_OWNERSHIP_CERTIFICATE" => BusinessKybDocumentType.BeneficialOwnershipCertificate,
            "INCORPORATION_DOCUMENTS" => BusinessKybDocumentType.IncorporationDocuments,
            "CAC_STATUS_REPORT" => BusinessKybDocumentType.CacStatusReport,
            "SHARE_REGISTER" => BusinessKybDocumentType.ShareRegister,
            "BANK_STATEMENT" => BusinessKybDocumentType.BankStatement,
            "PROOF_OF_ADDRESS" => BusinessKybDocumentType.ProofOfBusinessAddress,
            "TAX_DOCUMENT" => BusinessKybDocumentType.TaxDocument,
            _ => BusinessKybDocumentType.Other
        };

    private static string? FirstAdminComment(string? commentsJson) =>
        ReadAdminComments(commentsJson).FirstOrDefault();

    private static IReadOnlyList<string> ReadAdminComments(string? commentsJson)
    {
        if (string.IsNullOrWhiteSpace(commentsJson)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(commentsJson) ?? [];
        }
        catch (JsonException)
        {
            return [commentsJson];
        }
    }

    private async Task UpsertProviderTransactionAsync(
        Guid transferId,
        Guid? collectionId,
        Guid? payoutId,
        string providerTransactionId,
        string? providerReference,
        string transactionType,
        string providerStatus,
        string currencyCode,
        decimal amount,
        string rawPayload,
        DateTime providerCreatedAt,
        CancellationToken ct)
    {
        var transaction = await _db.ProviderTransactions
            .FirstOrDefaultAsync(x =>
                x.ProviderCode == ProviderCode.Blaaiz &&
                x.ProviderTransactionId == providerTransactionId,
                ct);

        if (transaction is null)
        {
            transaction = new ProviderTransaction
            {
                ProviderCode = ProviderCode.Blaaiz,
                ProviderTransactionId = providerTransactionId
            };
            _db.ProviderTransactions.Add(transaction);
        }

        transaction.TransferId = transferId;
        transaction.CollectionId = collectionId ?? transaction.CollectionId;
        transaction.PayoutId = payoutId ?? transaction.PayoutId;
        transaction.ProviderReference = providerReference ?? transaction.ProviderReference;
        transaction.TransactionType = transactionType;
        transaction.ProviderStatus = providerStatus;
        transaction.CurrencyCode = currencyCode;
        transaction.Amount = amount;
        transaction.RawPayloadJson = rawPayload;
        transaction.ProviderCreatedAt = providerCreatedAt;
        transaction.LastSyncedAt = DateTime.UtcNow;
        transaction.LastUpdatedAt = DateTime.UtcNow;
    }

    private void ValidateSignature(string rawPayload, string? receivedSignature, string? timestamp)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSigningSecret) ||
            string.IsNullOrWhiteSpace(receivedSignature) ||
            string.IsNullOrWhiteSpace(timestamp))
        {
            throw new UnauthorizedAccessException("Missing Blaaiz webhook signature information.");
        }

        var isValid = BlaaizWebhookSignature.IsValid(
            rawPayload,
            receivedSignature,
            timestamp,
            _options.WebhookSigningSecret,
            TimeSpan.FromMinutes(_options.WebhookTimestampToleranceMinutes),
            DateTimeOffset.UtcNow);

        if (!isValid)
            throw new UnauthorizedAccessException("Invalid or stale Blaaiz webhook signature.");
    }

    private static JsonElement GetDataNode(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object)
        {
            return data;
        }

        return root;
    }

    private static string CreateDeterministicEventId(string eventType, string rawPayload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{eventType}:{rawPayload}"));
        return $"derived-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static string InferEventType(string? type, string? status, string expectedChannel)
    {
        var channel = string.IsNullOrWhiteSpace(type) ? expectedChannel : type.Trim().ToLowerInvariant();
        var suffix = (status ?? "pending").Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => "completed",
            "PROCESSING" => "processing",
            "FAILED" or "REJECTED" => "failed",
            "EXPIRED" => "expired",
            _ => "pending"
        };

        return $"{channel}.{suffix}";
    }

    private static string EventStatus(string eventType) => eventType.ToLowerInvariant() switch
    {
        "collection.completed" or "payout.completed" => "SUCCESSFUL",
        "collection.processing" or "payout.processing" => "PROCESSING",
        "collection.failed" or "payout.failed" => "FAILED",
        "collection.expired" => "EXPIRED",
        "refund_initiated" => "REFUND_PENDING",
        "refund" => "REFUNDED",
        _ => "PENDING"
    };

    private static CollectionStatus MapCollectionStatus(string eventType, string providerStatus)
    {
        if (eventType.Equals("refund_initiated", StringComparison.OrdinalIgnoreCase))
        {
            return CollectionStatus.RefundPending;
        }

        if (eventType.Equals("refund", StringComparison.OrdinalIgnoreCase))
        {
            return providerStatus.Trim().ToUpperInvariant() switch
            {
                "FAILED" or "REJECTED" => CollectionStatus.RefundFailed,
                "SUCCESSFUL" or "COMPLETED" => CollectionStatus.Refunded,
                _ => CollectionStatus.RefundPending
            };
        }

        return providerStatus.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => CollectionStatus.Successful,
            "PROCESSING" => CollectionStatus.Processing,
            "FAILED" or "REJECTED" => CollectionStatus.Failed,
            "EXPIRED" => CollectionStatus.Expired,
            "REFUND_PENDING" or "REFUND_INITIATED" => CollectionStatus.RefundPending,
            "REFUNDED" => CollectionStatus.Refunded,
            _ => CollectionStatus.Initiated
        };
    }

    private static PayoutStatus MapPayoutStatus(string eventType, string providerStatus)
    {
        if (eventType.Equals("payout.completed", StringComparison.OrdinalIgnoreCase))
        {
            return PayoutStatus.Successful;
        }

        if (eventType.Equals("payout.failed", StringComparison.OrdinalIgnoreCase))
        {
            return PayoutStatus.Failed;
        }

        return providerStatus.Trim().ToUpperInvariant() switch
        {
            "SUCCESSFUL" or "COMPLETED" => PayoutStatus.Successful,
            "PROCESSING" => PayoutStatus.Processing,
            "FAILED" or "REJECTED" => PayoutStatus.Failed,
            "REVERSED" => PayoutStatus.Reversed,
            _ => PayoutStatus.Initiated
        };
    }

    private static KybStatus MapProviderKybStatus(string providerStatus) =>
        providerStatus.Trim().ToUpperInvariant() switch
        {
            "VERIFIED" => KybStatus.Approved,
            "REJECTED" => KybStatus.Rejected,
            "PROCESSING" => KybStatus.UnderReview,
            "PENDING" => KybStatus.Pending,
            _ => throw new InvalidOperationException(
                $"Unsupported Blaaiz business customer status '{providerStatus}'.")
        };

    private static KycStatus MapProviderKycStatus(string providerStatus) => providerStatus switch
    {
        "VERIFIED" => KycStatus.Approved,
        "REJECTED" => KycStatus.Rejected,
        "PROCESSING" => KycStatus.UnderReview,
        "PENDING" => KycStatus.Pending,
        _ => throw new InvalidOperationException($"Unsupported Blaaiz customer status '{providerStatus}'.")
    };

    private static string ReadRequiredString(
        JsonElement primary,
        JsonElement fallback,
        params string[] names) =>
        ReadOptionalString(primary, fallback, names)
        ?? throw new InvalidOperationException(
            $"Blaaiz webhook payload is missing '{string.Join("' or '", names)}'.");

    private static string? ReadOptionalString(
        JsonElement primary,
        JsonElement fallback,
        params string[] names)
    {
        foreach (var element in EnumerateObjectCandidates(primary, fallback))
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString();
                }

                if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return value.GetRawText();
                }
            }
        }

        return null;
    }

    private static string? ReadTransactionId(
        JsonElement primary,
        JsonElement fallback,
        params string[] names)
    {
        var explicitId = ReadOptionalString(primary, fallback, names);
        if (!string.IsNullOrWhiteSpace(explicitId))
        {
            return explicitId;
        }

        if (primary.ValueKind == JsonValueKind.Object &&
            primary.TryGetProperty("id", out var primaryId))
        {
            return primaryId.ValueKind == JsonValueKind.String
                ? primaryId.GetString()
                : primaryId.GetRawText();
        }

        foreach (var element in EnumerateObjectCandidates(primary, fallback))
        {
            if (element.TryGetProperty("transaction", out var transaction) &&
                transaction.ValueKind == JsonValueKind.Object &&
                transaction.TryGetProperty("id", out var id))
            {
                return id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
            }
        }

        return null;
    }

    private static decimal? ReadOptionalDecimal(
        JsonElement primary,
        JsonElement fallback,
        params string[] names)
    {
        foreach (var element in EnumerateObjectCandidates(primary, fallback))
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
                {
                    return number;
                }
            }
        }

        return null;
    }

    private static decimal? ReadRecipientAmount(JsonElement data)
    {
        foreach (var element in EnumerateObjectCandidates(data, data))
        {
            if (element.TryGetProperty("recipient", out var recipient) &&
                recipient.ValueKind == JsonValueKind.Object)
            {
                return ReadOptionalDecimal(recipient, recipient, "amount");
            }
        }

        return null;
    }

    private static string? ReadRecipientString(JsonElement data, params string[] names)
    {
        foreach (var element in EnumerateObjectCandidates(data, data))
        {
            if (element.TryGetProperty("recipient", out var recipient) &&
                recipient.ValueKind == JsonValueKind.Object)
            {
                return ReadOptionalString(recipient, recipient, names);
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateObjectCandidates(
        JsonElement primary,
        JsonElement fallback)
    {
        foreach (var element in new[] { primary, fallback })
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            yield return element;

            if (element.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                yield return data;

                if (data.TryGetProperty("transaction", out var dataTransaction) &&
                    dataTransaction.ValueKind == JsonValueKind.Object)
                {
                    yield return dataTransaction;
                }
            }

            if (element.TryGetProperty("transaction", out var transaction) &&
                transaction.ValueKind == JsonValueKind.Object)
            {
                yield return transaction;
            }
        }
    }

    private static DateTime? ReadOptionalDateTime(
        JsonElement primary,
        JsonElement fallback,
        params string[] names)
    {
        var value = ReadOptionalString(primary, fallback, names);
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static void ValidateMoney(
        decimal expectedAmount,
        string expectedCurrency,
        decimal? actualAmount,
        string? actualCurrency)
    {
        if (actualAmount is null || string.IsNullOrWhiteSpace(actualCurrency))
        {
            throw new InvalidOperationException(
                "Successful provider webhook is missing transaction amount or currency.");
        }

        if (!string.Equals(expectedCurrency, actualCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provider transaction currency mismatch. Expected {expectedCurrency}, received {actualCurrency}.");
        }

        if (Math.Abs(expectedAmount - actualAmount.Value) > 0.01m)
        {
            throw new InvalidOperationException(
                $"Provider transaction amount mismatch. Expected {expectedAmount}, received {actualAmount.Value}.");
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
