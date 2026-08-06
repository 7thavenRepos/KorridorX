using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Transfers;

public class TransferStatusService : ITransferStatusService
{
    private static readonly IReadOnlyDictionary<TransferStatus, HashSet<TransferStatus>> AllowedTransitions =
        new Dictionary<TransferStatus, HashSet<TransferStatus>>
        {
            [TransferStatus.Draft] =
            [
                TransferStatus.Quoted,
                TransferStatus.PendingApproval,
                TransferStatus.PendingPayment,
                TransferStatus.PaymentReceived,
                TransferStatus.Cancelled
            ],

            [TransferStatus.Quoted] =
            [
                TransferStatus.PendingApproval,
                TransferStatus.PendingPayment,
                TransferStatus.Cancelled,
                TransferStatus.Failed
            ],

            [TransferStatus.PendingApproval] =
            [
                TransferStatus.PendingPayment,
                TransferStatus.PaymentReceived,
                TransferStatus.Rejected,
                TransferStatus.Cancelled
            ],

            [TransferStatus.PendingPayment] =
            [
                TransferStatus.PaymentReceived,
                TransferStatus.Cancelled,
                TransferStatus.Failed
            ],

            [TransferStatus.PaymentReceived] =
            [
                TransferStatus.Processing,
                TransferStatus.RefundPending,
                TransferStatus.Failed
            ],

            [TransferStatus.Processing] =
            [
                TransferStatus.PayoutInitiated,
                TransferStatus.RefundPending,
                TransferStatus.Failed
            ],

            [TransferStatus.PayoutInitiated] =
            [
                TransferStatus.PayoutCompleted,
                TransferStatus.RefundPending,
                TransferStatus.Failed
            ],

            [TransferStatus.PayoutCompleted] =
            [
                TransferStatus.Completed,
                TransferStatus.RefundPending,
                TransferStatus.Failed
            ],

            [TransferStatus.Completed] =
            [
                TransferStatus.RefundPending
            ],

            [TransferStatus.Failed] =
            [
                TransferStatus.RefundPending
            ],

            [TransferStatus.RefundPending] =
            [
                TransferStatus.Processing,
                TransferStatus.Refunded,
                TransferStatus.Failed
            ],

            [TransferStatus.Cancelled] =
            [
                TransferStatus.RefundPending
            ],

            [TransferStatus.Rejected] =
            [
                TransferStatus.PendingApproval
            ],

            [TransferStatus.Refunded] = []
        };

    private readonly AppDbContext _db;

    public TransferStatusService(AppDbContext db)
    {
        _db = db;
    }

    public bool CanTransition(
        TransferStatus currentStatus,
        TransferStatus newStatus)
    {
        if (currentStatus == newStatus)
        {
            return true;
        }

        return AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
               allowed.Contains(newStatus);
    }

    public bool ApplyTransition(
        Transfer transfer,
        TransferStatus newStatus,
        TransferStatusTransitionContext context)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        ArgumentNullException.ThrowIfNull(context);

        if (transfer.Status == newStatus)
        {
            return false;
        }

        if (!CanTransition(transfer.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Transfer cannot move from '{transfer.Status}' to '{newStatus}'.");
        }

        var source = NormalizeRequired(context.Source, "Transition source", 100);
        var reason = NormalizeOptional(context.Reason, "Transition reason", 1000);
        var eventType = NormalizeRequired(
            context.EventType ?? GetDefaultEventType(newStatus),
            "Timeline event type",
            100);
        var title = NormalizeRequired(
            context.Title ?? GetDefaultTitle(newStatus),
            "Timeline title",
            200);
        var description = NormalizeOptional(
            context.Description ?? reason ?? GetDefaultDescription(newStatus),
            "Timeline description",
            1000);

        var now = context.OccurredAt ?? DateTime.UtcNow;
        var oldStatus = transfer.Status;

        transfer.Status = newStatus;
        transfer.LastUpdatedAt = now;
        transfer.LastUpdatedByUserId = context.ChangedByUserId;

        ApplyStatusTimestamp(transfer, newStatus, reason, now);

        _db.TransferStatusHistories.Add(new TransferStatusHistory
        {
            TransferId = transfer.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Reason = reason,
            Source = source,
            ChangedByUserId = context.ChangedByUserId,
            ChangedAt = now
        });

        _db.TransferTimelineEvents.Add(new TransferTimelineEvent
        {
            TransferId = transfer.Id,
            EventType = eventType,
            Title = title,
            Description = description,
            MetadataJson = context.MetadataJson,
            OccurredAt = now
        });

        return true;
    }

    private static void ApplyStatusTimestamp(
        Transfer transfer,
        TransferStatus newStatus,
        string? reason,
        DateTime occurredAt)
    {
        switch (newStatus)
        {
            case TransferStatus.PaymentReceived:
                transfer.PaymentReceivedAt ??= occurredAt;
                break;

            case TransferStatus.PayoutInitiated:
                transfer.PayoutInitiatedAt ??= occurredAt;
                break;

            case TransferStatus.Completed:
                transfer.CompletedAt ??= occurredAt;
                break;

            case TransferStatus.Failed:
                transfer.FailedAt = occurredAt;
                transfer.FailureReason = reason ?? "Transfer processing failed.";
                break;

            case TransferStatus.Cancelled:
                transfer.CancelledAt ??= occurredAt;
                break;
        }
    }

    private static string GetDefaultEventType(TransferStatus status)
    {
        return status switch
        {
            TransferStatus.Draft => "TRANSFER_DRAFTED",
            TransferStatus.Quoted => "TRANSFER_QUOTED",
            TransferStatus.PendingPayment => "TRANSFER_PENDING_PAYMENT",
            TransferStatus.PaymentReceived => "TRANSFER_PAYMENT_RECEIVED",
            TransferStatus.Processing => "TRANSFER_PROCESSING",
            TransferStatus.PayoutInitiated => "TRANSFER_PAYOUT_INITIATED",
            TransferStatus.PayoutCompleted => "TRANSFER_PAYOUT_COMPLETED",
            TransferStatus.Completed => "TRANSFER_COMPLETED",
            TransferStatus.Failed => "TRANSFER_FAILED",
            TransferStatus.Cancelled => "TRANSFER_CANCELLED",
            TransferStatus.RefundPending => "TRANSFER_REFUND_PENDING",
            TransferStatus.Refunded => "TRANSFER_REFUNDED",
            TransferStatus.PendingApproval => "TRANSFER_PENDING_APPROVAL",
            TransferStatus.Rejected => "TRANSFER_REJECTED",
            _ => "TRANSFER_STATUS_CHANGED"
        };
    }

    private static string GetDefaultTitle(TransferStatus status)
    {
        return status switch
        {
            TransferStatus.Draft => "Transfer saved as draft",
            TransferStatus.Quoted => "Quote accepted",
            TransferStatus.PendingPayment => "Payment pending",
            TransferStatus.PaymentReceived => "Payment received",
            TransferStatus.Processing => "Transfer processing",
            TransferStatus.PayoutInitiated => "Payout initiated",
            TransferStatus.PayoutCompleted => "Payout completed",
            TransferStatus.Completed => "Transfer completed",
            TransferStatus.Failed => "Transfer failed",
            TransferStatus.Cancelled => "Transfer cancelled",
            TransferStatus.RefundPending => "Refund pending",
            TransferStatus.Refunded => "Transfer refunded",
            TransferStatus.PendingApproval => "Transfer pending approval",
            TransferStatus.Rejected => "Transfer rejected",
            _ => "Transfer status updated"
        };
    }

    private static string GetDefaultDescription(TransferStatus status)
    {
        return status switch
        {
            TransferStatus.Draft => "The transfer has been saved as a draft.",
            TransferStatus.Quoted => "The transfer quote has been accepted.",
            TransferStatus.PendingPayment => "The transfer is waiting for payment.",
            TransferStatus.PaymentReceived => "Payment for the transfer has been received.",
            TransferStatus.Processing => "The transfer is being processed.",
            TransferStatus.PayoutInitiated => "The recipient payout has been initiated.",
            TransferStatus.PayoutCompleted => "The recipient payout has been completed.",
            TransferStatus.Completed => "The transfer has been completed successfully.",
            TransferStatus.Failed => "The transfer could not be completed.",
            TransferStatus.Cancelled => "The transfer has been cancelled.",
            TransferStatus.RefundPending => "A refund is being processed.",
            TransferStatus.Refunded => "The transfer payment has been refunded.",
            TransferStatus.PendingApproval => "The transfer is waiting for business approval.",
            TransferStatus.Rejected => "The transfer was rejected during business approval.",
            _ => "The transfer status has been updated."
        };
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
