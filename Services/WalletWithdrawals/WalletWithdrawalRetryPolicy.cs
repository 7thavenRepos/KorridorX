using System.Text.Json;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Models.Providers;
using KorridorX.Models.WalletWithdrawals;

namespace KorridorX.Services.WalletWithdrawals;

// This pure policy only classifies existing evidence; it does not retry or settle money.
public static class WalletWithdrawalRetryPolicy
{
    public static bool IsLatestKnownNoteRejection(int? status, string? body) =>
        status == 422 && WalletWithdrawalRecoveryPolicy.ReadKnownNoteValidationError(body) is not null;

    public static bool IsSoleNoteValidationError(int? status, string? body)
    {
        if (status != 422 || string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.TryGetProperty("transaction", out _) ||
                root.TryGetProperty("data", out _) ||
                !root.TryGetProperty("errors", out var errors) ||
                errors.ValueKind != JsonValueKind.Object)
                return false;
            var fields = errors.EnumerateObject().ToArray();
            if (fields.Length != 1 || fields[0].Name != "note" ||
                fields[0].Value.ValueKind != JsonValueKind.Array ||
                fields[0].Value.GetArrayLength() != 1)
                return false;
            var note = fields[0].Value[0];
            return note.ValueKind == JsonValueKind.String &&
                string.Equals(note.GetString(),
                    WalletWithdrawalRecoveryPolicy.BlaaizNoteLimitMessage, StringComparison.Ordinal);
        }
        catch (JsonException) { return false; }
    }

    public static bool CanRetryOnce(FiatWalletWithdrawal withdrawal, Payout payout,
        ProviderRequestLog? originalPost, int postCount, int get404Count,
        int otherGetCount, DateTime? last404Utc, DateTime nowUtc)
    {
        if (withdrawal is null || payout is null || originalPost is null ||
            withdrawal.IsDeleted || payout.IsDeleted ||
            withdrawal.Status != FiatWalletWithdrawalStatus.RequiresReconciliation ||
            withdrawal.PayoutId != payout.Id || withdrawal.FinancialReservationId is null ||
            withdrawal.SubmittedToProviderAtUtc is null ||
            payout.Status != PayoutStatus.Processing ||
            !string.IsNullOrWhiteSpace(payout.ProviderPayoutId) ||
            !string.IsNullOrWhiteSpace(payout.ProviderReference) ||
            payout.Purpose != PaymentOperationPurpose.Withdrawal ||
            payout.RelatedEntityType != nameof(FiatWalletWithdrawal) ||
            payout.RelatedEntityId != withdrawal.Id ||
            payout.FinancialAccountId != withdrawal.FinancialAccountId ||
            payout.PaymentChannelId != withdrawal.PaymentChannelId ||
            payout.PaymentMethod != withdrawal.PaymentMethod ||
            !string.Equals(payout.ProviderCode, "Blaaiz", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(payout.Reference, withdrawal.Reference, StringComparison.Ordinal) ||
            !string.Equals(payout.CurrencyCode, withdrawal.CurrencyCode, StringComparison.Ordinal) ||
            payout.Amount != withdrawal.ExpectedRecipientAmount ||
            postCount != 1 || get404Count < 1 || otherGetCount != 0 ||
            last404Utc is null || last404Utc > nowUtc ||
            last404Utc < nowUtc.AddHours(-24) ||
            originalPost.RelatedPayoutId != payout.Id ||
            originalPost.HttpMethod != "POST" ||
            originalPost.Status != ProviderRequestStatus.Failed ||
            payout.Attempts.Count != 1 ||
            payout.Attempts.Single().Status != ProviderRequestStatus.Pending)
            return false;
        return IsSoleNoteValidationError(originalPost.ResponseStatusCode, originalPost.ResponseBodyJson);
    }
}