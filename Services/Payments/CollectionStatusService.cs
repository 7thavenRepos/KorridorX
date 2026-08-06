using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Transfers;
using KorridorX.Services.Transfers;

namespace KorridorX.Services.Payments;

public class CollectionStatusService : ICollectionStatusService
{
    private static readonly IReadOnlyDictionary<CollectionStatus, HashSet<CollectionStatus>> AllowedTransitions =
        new Dictionary<CollectionStatus, HashSet<CollectionStatus>>
        {
            [CollectionStatus.Pending] =
            [
                CollectionStatus.Initiated,
                CollectionStatus.Failed,
                CollectionStatus.Cancelled
            ],

            [CollectionStatus.Initiated] =
            [
                CollectionStatus.Processing,
                CollectionStatus.Successful,
                CollectionStatus.Failed,
                CollectionStatus.Cancelled
            ],

            [CollectionStatus.Processing] =
            [
                CollectionStatus.Successful,
                CollectionStatus.Failed,
                CollectionStatus.Cancelled
            ],

            [CollectionStatus.Failed] =
            [
                CollectionStatus.Initiated,
                CollectionStatus.Cancelled
            ],

            [CollectionStatus.Cancelled] =
            [
                CollectionStatus.Initiated
            ],

            [CollectionStatus.Successful] =
            [
                CollectionStatus.Refunded
            ],

            [CollectionStatus.Refunded] = []
        };

    private readonly AppDbContext _db;
    private readonly ITransferStatusService _transferStatusService;

    public CollectionStatusService(
        AppDbContext db,
        ITransferStatusService transferStatusService)
    {
        _db = db;
        _transferStatusService = transferStatusService;
    }

    public bool CanTransition(
        CollectionStatus currentStatus,
        CollectionStatus newStatus)
    {
        if (currentStatus == newStatus)
        {
            return true;
        }

        return AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
               allowed.Contains(newStatus);
    }

    public bool ApplyTransition(
        Collection collection,
        CollectionStatus newStatus,
        CollectionStatusTransitionContext context,
        CollectionAttempt? attempt = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(context);

        if (collection.Status == newStatus)
        {
            ApplyProviderDetails(collection, context);
            ApplyAttemptDetails(attempt, newStatus, context);
            return false;
        }

        if (!CanTransition(collection.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Collection cannot move from '{collection.Status}' to '{newStatus}'.");
        }

        ValidateTransferState(collection, newStatus);
        ApplyProviderDetails(collection, context);
        ApplyAttemptDetails(attempt, newStatus, context);

        var source = NormalizeRequired(context.Source, "Transition source", 100);
        var reason = NormalizeOptional(context.Reason, "Transition reason", 1000);
        var now = context.OccurredAt ?? DateTime.UtcNow;

        collection.Status = newStatus;
        collection.LastUpdatedAt = now;
        collection.LastUpdatedByUserId = context.ChangedByUserId;

        ApplyCollectionTimestamp(collection, newStatus, reason, now);
        ApplyTransferEffect(collection, newStatus, source, reason, context, now);

        return true;
    }


    private static void ValidateTransferState(
        Collection collection,
        CollectionStatus newStatus)
    {
        if ((newStatus is CollectionStatus.Initiated or CollectionStatus.Processing) &&
            collection.Transfer.Status != TransferStatus.PendingPayment)
        {
            throw new InvalidOperationException(
                $"Collection cannot move to '{newStatus}' while the transfer status is '{collection.Transfer.Status}'.");
        }
    }

    private void ApplyTransferEffect(
        Collection collection,
        CollectionStatus newStatus,
        string source,
        string? reason,
        CollectionStatusTransitionContext context,
        DateTime occurredAt)
    {
        var transfer = collection.Transfer;

        switch (newStatus)
        {
            case CollectionStatus.Successful:
                if (transfer.Status == TransferStatus.PendingPayment)
                {
                    _transferStatusService.ApplyTransition(
                        transfer,
                        TransferStatus.PaymentReceived,
                        new TransferStatusTransitionContext(
                            Source: source,
                            Reason: reason ?? "Transfer funding was confirmed.",
                            ChangedByUserId: context.ChangedByUserId,
                            EventType: "COLLECTION_SUCCESSFUL",
                            Title: "Payment received",
                            Description: "Your payment has been received and the transfer can now be processed.",
                            MetadataJson: context.MetadataJson,
                            OccurredAt: occurredAt));
                }
                else if (transfer.Status is TransferStatus.Cancelled or TransferStatus.Failed)
                {
                    _transferStatusService.ApplyTransition(
                        transfer,
                        TransferStatus.RefundPending,
                        new TransferStatusTransitionContext(
                            Source: source,
                            Reason: "Payment was received after the transfer could no longer proceed.",
                            ChangedByUserId: context.ChangedByUserId,
                            EventType: "LATE_COLLECTION_RECEIVED",
                            Title: "Payment received after transfer closure",
                            Description: "The payment requires a refund because the transfer can no longer proceed.",
                            MetadataJson: context.MetadataJson,
                            OccurredAt: occurredAt));
                }
                break;

            case CollectionStatus.Refunded:
                if (transfer.Status != TransferStatus.RefundPending &&
                    _transferStatusService.CanTransition(transfer.Status, TransferStatus.RefundPending))
                {
                    _transferStatusService.ApplyTransition(
                        transfer,
                        TransferStatus.RefundPending,
                        new TransferStatusTransitionContext(
                            Source: source,
                            Reason: reason ?? "A collection refund was initiated.",
                            ChangedByUserId: context.ChangedByUserId,
                            MetadataJson: context.MetadataJson,
                            OccurredAt: occurredAt));
                }

                if (_transferStatusService.CanTransition(transfer.Status, TransferStatus.Refunded))
                {
                    _transferStatusService.ApplyTransition(
                        transfer,
                        TransferStatus.Refunded,
                        new TransferStatusTransitionContext(
                            Source: source,
                            Reason: reason ?? "The transfer payment was refunded.",
                            ChangedByUserId: context.ChangedByUserId,
                            EventType: "COLLECTION_REFUNDED",
                            Title: "Payment refunded",
                            Description: "The transfer payment has been refunded.",
                            MetadataJson: context.MetadataJson,
                            OccurredAt: occurredAt));
                }
                break;

            default:
                AddCollectionTimelineEvent(
                    transfer.Id,
                    newStatus,
                    reason,
                    context.MetadataJson,
                    occurredAt);
                break;
        }
    }

    private void AddCollectionTimelineEvent(
        Guid transferId,
        CollectionStatus status,
        string? reason,
        string? metadataJson,
        DateTime occurredAt)
    {
        var (eventType, title, description) = status switch
        {
            CollectionStatus.Pending =>
                ("COLLECTION_PENDING", "Payment method selected", "Funding instructions are being prepared."),

            CollectionStatus.Initiated =>
                ("COLLECTION_INITIATED", "Payment initiated", "The payment request has been initiated."),

            CollectionStatus.Processing =>
                ("COLLECTION_PROCESSING", "Payment processing", "The payment is being processed."),

            CollectionStatus.Failed =>
                ("COLLECTION_FAILED", "Payment failed", reason ?? "The payment could not be completed."),

            CollectionStatus.Cancelled =>
                ("COLLECTION_CANCELLED", "Payment cancelled", reason ?? "The payment request was cancelled."),

            _ =>
                ("COLLECTION_STATUS_CHANGED", "Payment status updated", reason ?? "The payment status has been updated.")
        };

        _db.TransferTimelineEvents.Add(new TransferTimelineEvent
        {
            TransferId = transferId,
            EventType = eventType,
            Title = title,
            Description = description,
            MetadataJson = metadataJson,
            OccurredAt = occurredAt
        });
    }

    private static void ApplyCollectionTimestamp(
        Collection collection,
        CollectionStatus newStatus,
        string? reason,
        DateTime occurredAt)
    {
        switch (newStatus)
        {
            case CollectionStatus.Initiated:
                collection.InitiatedAt ??= occurredAt;
                collection.FailedAt = null;
                collection.FailureReason = null;
                break;

            case CollectionStatus.Successful:
                collection.ConfirmedAt ??= occurredAt;
                collection.FailedAt = null;
                collection.FailureReason = null;
                break;

            case CollectionStatus.Failed:
                collection.FailedAt = occurredAt;
                collection.FailureReason = reason ?? "Payment collection failed.";
                break;
        }
    }

    private static void ApplyProviderDetails(
        Collection collection,
        CollectionStatusTransitionContext context)
    {
        collection.ProviderCollectionId = NormalizeOptional(
            context.ProviderCollectionId ?? collection.ProviderCollectionId,
            "Provider collection ID",
            150);

        collection.ProviderReference = NormalizeOptional(
            context.ProviderReference ?? collection.ProviderReference,
            "Provider reference",
            150);

        collection.CheckoutUrl = NormalizeOptional(
            context.CheckoutUrl ?? collection.CheckoutUrl,
            "Checkout URL",
            1000);

        collection.ProviderExpiresAt = context.ProviderExpiresAt ?? collection.ProviderExpiresAt;

        collection.VirtualAccountNumber = NormalizeOptional(
            context.VirtualAccountNumber ?? collection.VirtualAccountNumber,
            "Virtual account number",
            100);

        collection.VirtualAccountBankName = NormalizeOptional(
            context.VirtualAccountBankName ?? collection.VirtualAccountBankName,
            "Virtual account bank name",
            150);

        collection.VirtualAccountName = NormalizeOptional(
            context.VirtualAccountName ?? collection.VirtualAccountName,
            "Virtual account name",
            200);
    }

    private static void ApplyAttemptDetails(
        CollectionAttempt? attempt,
        CollectionStatus newStatus,
        CollectionStatusTransitionContext context)
    {
        if (attempt is null)
        {
            return;
        }

        attempt.ProviderRequestId = NormalizeOptional(
            context.ProviderRequestId ?? attempt.ProviderRequestId,
            "Provider request ID",
            150);

        attempt.ProviderResponseId = NormalizeOptional(
            context.ProviderResponseId ?? attempt.ProviderResponseId,
            "Provider response ID",
            150);

        attempt.RequestPayloadJson = context.RequestPayloadJson ?? attempt.RequestPayloadJson;
        attempt.ResponsePayloadJson = context.ResponsePayloadJson ?? attempt.ResponsePayloadJson;
        attempt.LastUpdatedAt = context.OccurredAt ?? DateTime.UtcNow;

        attempt.Status = newStatus switch
        {
            CollectionStatus.Successful or CollectionStatus.Refunded => ProviderRequestStatus.Successful,
            CollectionStatus.Failed or CollectionStatus.Cancelled => ProviderRequestStatus.Failed,
            _ => ProviderRequestStatus.Pending
        };

        attempt.ErrorMessage = attempt.Status == ProviderRequestStatus.Failed
            ? NormalizeOptional(context.Reason, "Attempt error", 1000)
            : null;
    }

    private static string NormalizeRequired(
        string value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException(
                $"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        string fieldName,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException(
                $"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
