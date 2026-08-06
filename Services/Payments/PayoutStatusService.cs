using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Services.Transfers;

namespace KorridorX.Services.Payments;

public class PayoutStatusService : IPayoutStatusService
{
    private static readonly IReadOnlyDictionary<PayoutStatus, HashSet<PayoutStatus>> AllowedTransitions =
        new Dictionary<PayoutStatus, HashSet<PayoutStatus>>
        {
            [PayoutStatus.Pending] = [PayoutStatus.Initiated, PayoutStatus.Processing, PayoutStatus.Successful, PayoutStatus.Failed],
            [PayoutStatus.Initiated] = [PayoutStatus.Processing, PayoutStatus.Successful, PayoutStatus.Failed, PayoutStatus.Reversed],
            [PayoutStatus.Processing] = [PayoutStatus.Successful, PayoutStatus.Failed, PayoutStatus.Reversed],
            [PayoutStatus.Failed] = [PayoutStatus.Initiated, PayoutStatus.Processing, PayoutStatus.Successful],
            [PayoutStatus.Successful] = [PayoutStatus.Reversed],
            [PayoutStatus.Reversed] = []
        };

    private readonly ITransferStatusService _transferStatusService;

    public PayoutStatusService(ITransferStatusService transferStatusService)
    {
        _transferStatusService = transferStatusService;
    }

    public bool CanTransition(PayoutStatus currentStatus, PayoutStatus newStatus)
    {
        if (currentStatus == newStatus)
        {
            return true;
        }

        return AllowedTransitions.TryGetValue(currentStatus, out var allowed) &&
               allowed.Contains(newStatus);
    }

    public bool ApplyTransition(
        Payout payout,
        PayoutStatus newStatus,
        PayoutStatusTransitionContext context,
        PayoutAttempt? attempt = null)
    {
        ArgumentNullException.ThrowIfNull(payout);
        ArgumentNullException.ThrowIfNull(context);

        ApplyProviderDetails(payout, context);
        ApplyAttemptDetails(attempt, newStatus, context);

        if (payout.Status == newStatus)
        {
            return false;
        }

        if (!CanTransition(payout.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Payout cannot move from '{payout.Status}' to '{newStatus}'.");
        }

        var now = context.OccurredAt ?? DateTime.UtcNow;
        var reason = NormalizeOptional(context.Reason, 1000);

        payout.Status = newStatus;
        payout.LastUpdatedAt = now;
        payout.LastUpdatedByUserId = context.ChangedByUserId;

        ApplyPayoutTimestamp(payout, newStatus, reason, now);
        ApplyTransferEffect(payout, newStatus, context, reason, now);

        return true;
    }

    private void ApplyTransferEffect(
        Payout payout,
        PayoutStatus newStatus,
        PayoutStatusTransitionContext context,
        string? reason,
        DateTime occurredAt)
    {
        var transfer = payout.Transfer;
        var source = string.IsNullOrWhiteSpace(context.Source) ? "System" : context.Source.Trim();

        if (newStatus is PayoutStatus.Initiated or PayoutStatus.Processing)
        {
            if (transfer.Status == TransferStatus.PaymentReceived)
            {
                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.Processing,
                    new TransferStatusTransitionContext(
                        source,
                        "Transfer funding has been confirmed and payout processing has started.",
                        context.ChangedByUserId,
                        MetadataJson: context.MetadataJson,
                        OccurredAt: occurredAt));
            }

            if (transfer.Status == TransferStatus.Processing)
            {
                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.PayoutInitiated,
                    new TransferStatusTransitionContext(
                        source,
                        reason ?? "Recipient payout has been initiated.",
                        context.ChangedByUserId,
                        EventType: "PAYOUT_INITIATED",
                        Title: "Payout initiated",
                        Description: "The payout to your recipient has been initiated.",
                        MetadataJson: context.MetadataJson,
                        OccurredAt: occurredAt));
            }

            return;
        }

        if (newStatus == PayoutStatus.Successful)
        {
            if (transfer.Status == TransferStatus.PaymentReceived)
            {
                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.Processing,
                    new TransferStatusTransitionContext(
                        source,
                        "Transfer processing started.",
                        context.ChangedByUserId,
                        MetadataJson: context.MetadataJson,
                        OccurredAt: occurredAt));
            }

            if (transfer.Status == TransferStatus.Processing)
            {
                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.PayoutInitiated,
                    new TransferStatusTransitionContext(
                        source,
                        "Recipient payout was initiated.",
                        context.ChangedByUserId,
                        MetadataJson: context.MetadataJson,
                        OccurredAt: occurredAt));
            }

            if (transfer.Status == TransferStatus.PayoutInitiated)
            {
                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.PayoutCompleted,
                    new TransferStatusTransitionContext(
                        source,
                        "Recipient payout completed successfully.",
                        context.ChangedByUserId,
                        EventType: "PAYOUT_COMPLETED",
                        Title: "Recipient paid",
                        Description: "The recipient payout has been completed successfully.",
                        MetadataJson: context.MetadataJson,
                        OccurredAt: occurredAt));
            }

            if (transfer.Status == TransferStatus.PayoutCompleted)
            {
                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.Completed,
                    new TransferStatusTransitionContext(
                        source,
                        "Transfer completed successfully.",
                        context.ChangedByUserId,
                        EventType: "TRANSFER_COMPLETED",
                        Title: "Transfer completed",
                        Description: "The recipient has received the transfer.",
                        MetadataJson: context.MetadataJson,
                        OccurredAt: occurredAt));
            }

            return;
        }

        if (newStatus is PayoutStatus.Failed or PayoutStatus.Reversed)
        {
            if (_transferStatusService.CanTransition(transfer.Status, TransferStatus.RefundPending))
            {
                _transferStatusService.ApplyTransition(
                    transfer,
                    TransferStatus.RefundPending,
                    new TransferStatusTransitionContext(
                        source,
                        reason ?? "Recipient payout could not be completed and the transfer requires a refund.",
                        context.ChangedByUserId,
                        EventType: newStatus == PayoutStatus.Reversed ? "PAYOUT_REVERSED" : "PAYOUT_FAILED",
                        Title: newStatus == PayoutStatus.Reversed ? "Payout reversed" : "Payout failed",
                        Description: "The payout could not be completed. A refund review is required.",
                        MetadataJson: context.MetadataJson,
                        OccurredAt: occurredAt));
            }
        }
    }

    private static void ApplyPayoutTimestamp(
        Payout payout,
        PayoutStatus status,
        string? reason,
        DateTime occurredAt)
    {
        switch (status)
        {
            case PayoutStatus.Initiated:
            case PayoutStatus.Processing:
                payout.InitiatedAt ??= occurredAt;
                payout.FailedAt = null;
                payout.FailureReason = null;
                break;

            case PayoutStatus.Successful:
                payout.InitiatedAt ??= occurredAt;
                payout.CompletedAt ??= occurredAt;
                payout.FailedAt = null;
                payout.FailureReason = null;
                break;

            case PayoutStatus.Failed:
                payout.FailedAt = occurredAt;
                payout.FailureReason = reason ?? "Payout failed.";
                break;

            case PayoutStatus.Reversed:
                payout.ReversedAt = occurredAt;
                payout.FailureReason = reason ?? "Payout was reversed.";
                break;
        }
    }

    private static void ApplyProviderDetails(Payout payout, PayoutStatusTransitionContext context)
    {
        payout.ProviderPayoutId = NormalizeOptional(context.ProviderPayoutId ?? payout.ProviderPayoutId, 150);
        payout.ProviderReference = NormalizeOptional(context.ProviderReference ?? payout.ProviderReference, 150);
        payout.InteracQuestion = NormalizeOptional(context.InteracQuestion ?? payout.InteracQuestion, 500);
        payout.InteracAnswer = NormalizeOptional(context.InteracAnswer ?? payout.InteracAnswer, 500);
    }

    private static void ApplyAttemptDetails(
        PayoutAttempt? attempt,
        PayoutStatus newStatus,
        PayoutStatusTransitionContext context)
    {
        if (attempt is null)
        {
            return;
        }

        attempt.ProviderRequestId = NormalizeOptional(context.ProviderRequestId ?? attempt.ProviderRequestId, 150);
        attempt.ProviderResponseId = NormalizeOptional(context.ProviderResponseId ?? attempt.ProviderResponseId, 150);
        attempt.RequestPayloadJson = context.RequestPayloadJson ?? attempt.RequestPayloadJson;
        attempt.ResponsePayloadJson = context.ResponsePayloadJson ?? attempt.ResponsePayloadJson;
        attempt.LastUpdatedAt = context.OccurredAt ?? DateTime.UtcNow;

        attempt.Status = newStatus switch
        {
            PayoutStatus.Successful => ProviderRequestStatus.Successful,
            PayoutStatus.Failed or PayoutStatus.Reversed => ProviderRequestStatus.Failed,
            _ => ProviderRequestStatus.Pending
        };

        attempt.ErrorMessage = attempt.Status == ProviderRequestStatus.Failed
            ? NormalizeOptional(context.Reason, 1000)
            : null;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
