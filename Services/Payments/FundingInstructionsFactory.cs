using KorridorX.Dtos.Payments;
using KorridorX.Models.Enums;
using KorridorX.Models.Payments;
using KorridorX.Services.EmbeddedFinance;

namespace KorridorX.Services.Payments;

public static class FundingInstructionsFactory
{
    public static FundingInstructionsDto? ForCollection(Collection collection, DateTime? utcNow = null)
    {
        var kind = collection.PaymentMethod switch
        {
            PaymentMethod.Interac when collection.CurrencyCode == "CAD" => "interac_money_request",
            PaymentMethod.Card => "card_checkout",
            _ => null
        };
        if (kind is null) return null;
        var title = kind == "interac_money_request" ? "Pay by Interac" : "Pay by card";
        var expired = collection.Status == CollectionStatus.Expired ||
            collection.ProviderExpiresAt <= (utcNow ?? DateTime.UtcNow);
        var ready = !expired && collection.Status is CollectionStatus.Initiated or CollectionStatus.Processing;
        if (ready && kind == "card_checkout" &&
            (!Uri.TryCreate(collection.CheckoutUrl, UriKind.Absolute, out var checkout) || checkout.Scheme != "https"))
            return new(kind, collection.CurrencyCode, title, "Checkout is not available yet. Check payment status before retrying.", false, [], [], collection.ProviderExpiresAt);
        if (!ready)
        {
            var message = collection.Status switch
            {
                CollectionStatus.Successful => "Payment confirmed. No further payment is needed.",
                CollectionStatus.Refunded or CollectionStatus.RefundPending or CollectionStatus.RefundFailed =>
                    "This payment is in the refund process. Check its status before taking further action.",
                CollectionStatus.Cancelled => "This payment was cancelled.",
                CollectionStatus.Failed => "This payment failed. Check its status before retrying.",
                _ when expired => "These payment instructions have expired. Check the payment status before retrying.",
                _ => "Start the payment to receive instructions."
            };
            var showExpiry = collection.Status is CollectionStatus.Initiated or CollectionStatus.Processing or CollectionStatus.Expired;
            return new(kind, collection.CurrencyCode, title, message, false, [], [], showExpiry ? collection.ProviderExpiresAt : null);
        }

        // An Interac request goes to the payer. That email is NOT a receiving
        // Auto Deposit address and must never be presented as one.
        return kind == "interac_money_request"
            ? new(kind, "CAD", title, "Check the payer email used for this payment for the Interac money request.",
                true, [], ["Open the Interac request in that email inbox and review the amount and recipient.",
                    "Complete the request through your bank before it expires.",
                    "Return here and check payment status. Your balance updates after confirmation."], collection.ProviderExpiresAt)
            : new(kind, collection.CurrencyCode, title, "Complete this payment in the secure checkout.",
                true, [], ["Open the secure checkout for this payment.",
                    "Return here and check payment status after completing checkout."], collection.ProviderExpiresAt);
    }

    public static FundingInstructionsDto ForBankAccount(string currencyCode, bool isReady,
        string? accountNumber, string? accountName, string? bankName, VirtualAccountBankDetails? bankDetails)
    {
        var currency = currencyCode.Trim().ToUpperInvariant();
        var complete = !string.IsNullOrWhiteSpace(accountName) && !string.IsNullOrWhiteSpace(bankName) && (currency switch
        {
            "GBP" => Has(accountNumber) && Has(bankDetails?.SortCode),
            "EUR" => Has(bankDetails?.Iban),
            "USD" => Has(accountNumber) && Has(bankDetails?.RoutingNumber),
            "NGN" => Has(accountNumber) && Has(bankDetails?.BankCode),
            _ => false
        });
        if (!isReady || !complete)
            return new("bank_transfer", currency, $"{currency} bank transfer", "Complete bank details are not available. Refresh or contact support before sending money.", false, [], []);

        var fields = new List<FundingInstructionFieldDto>();
        Add("accountName", "Account name", accountName);
        Add("bankName", "Bank", bankName);
        Add("accountNumber", "Account number", accountNumber);
        if (currency is "GBP" or "EUR") Add("iban", "IBAN", bankDetails?.Iban);
        if (currency == "GBP") Add("sortCode", "Sort code", bankDetails?.SortCode);
        if (currency == "USD") Add("routingNumber", "Routing / ABA number", bankDetails?.RoutingNumber);
        if (currency == "NGN") Add("bankCode", "Bank code", bankDetails?.BankCode);
        // Neither currency nor an ABA number proves that ACH or wire is enabled.
        // The provider account's activation controls remain authoritative.
        return new("bank_transfer", currency, $"{currency} bank transfer", $"Use these details to send {currency} to this account.",
            true, fields, [$"Send {currency} only, using a transfer method supported by this receiving account.",
                "Copy the receiving details exactly and review them in your banking app.",
                "Your balance updates after the payment is confirmed. Check the account activity before sending again."]);

        void Add(string key, string label, string? value)
        {
            if (Has(value)) fields.Add(new(key, label, value!.Trim()));
        }
    }

    private static bool Has(string? value) => !string.IsNullOrWhiteSpace(value);
}
