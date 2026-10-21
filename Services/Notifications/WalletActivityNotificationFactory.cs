using System.Globalization;
using System.Text.Encodings.Web;
using KorridorX.Models.Payments;

namespace KorridorX.Services.Notifications;

public static class WalletActivityNotificationFactory
{
    public const string FundingEntityType = "WalletFunding";
    // The AccountSecurity prefix keeps full bank details out of push previews.
    // The dedicated type retains the useful account copy in authenticated notification history.
    public const string ReceivingAccountEntityType = "AccountSecurity.ReceivingAccountReady";

    public static (string Subject, string Body) ReceivingAccountReady(WalletDepositAccount account)
    {
        if (account.Status != "Active" ||
            (string.IsNullOrWhiteSpace(account.AccountNumber) && string.IsNullOrWhiteSpace(account.Iban)))
            throw new InvalidOperationException("A receiving-account notice requires confirmed bank details.");

        var currency = HtmlEncoder.Default.Encode(account.AssetCode);
        var details = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                details.Add($"<p><strong>{label}:</strong> {HtmlEncoder.Default.Encode(value)}</p>");
        }
        Add("Currency", account.AssetCode);
        Add("Bank", account.BankName);
        Add("Account name", account.AccountName);
        Add("Account number", account.AccountNumber);
        Add("IBAN", account.Iban);
        Add("Sort code", account.SortCode);
        Add("Routing number", account.RoutingNumber);

        return ($"Your {account.AssetCode} receiving bank account is ready",
            $"<p>Your {currency} receiving bank account is ready to use with your KorridorX wallet.</p>" +
            string.Concat(details) +
            $"<p>Send only {currency} using these bank details. Your wallet is credited after the deposit is confirmed.</p>" +
            "<p>If you did not request this receiving account, contact KorridorX support immediately.</p>" +
            "<p>KorridorX will never ask you to share your password, transaction PIN or verification code.</p>");
    }

    public static (string Subject, string Body) WalletAdded(string currencyCode)
    {
        var currency = HtmlEncoder.Default.Encode(currencyCode);
        return ($"Your {currencyCode} wallet is ready",
            $"<p>Your {currency} wallet has been added to KorridorX.</p>" +
            "<p>Open Wallet to view your receiving details and funding instructions. " +
            "Receiving details may remain pending while the provider completes activation.</p>");
    }

    public static (string Subject, string Body) FundingReceived(string currencyCode, decimal amount, string reference)
    {
        var currency = HtmlEncoder.Default.Encode(currencyCode);
        // Two places for familiar fiat receipts, retaining every additional
        // significant decimal if the credited asset has finer precision.
        var formattedAmount = amount.ToString("#,0.00##########################", CultureInfo.InvariantCulture);
        var safeReference = HtmlEncoder.Default.Encode(reference);
        return ($"Your {currencyCode} wallet has been funded",
            $"<p>Your {currency} wallet has received a confirmed deposit.</p>" +
            $"<p><strong>Amount credited: {currency} {formattedAmount}</strong></p>" +
            $"<p>Reference: {safeReference}</p>" +
            "<p>This is the amount added to your wallet. Open KorridorX to view the transaction and your available balance.</p>");
    }

    public static (string Subject, string Body) PinConfigured(string firstName, bool replacing)
    {
        var action = replacing ? "changed" : "set";
        return ($"Your KorridorX transaction PIN was {action}",
            $"<p>Hello {HtmlEncoder.Default.Encode(firstName)},</p>" +
            $"<p>Your transaction PIN was {action} successfully.</p>" +
            "<p>If you did not make this change, contact KorridorX support immediately.</p>" +
            "<p>KorridorX will never ask you to share your PIN by email, message or phone.</p>");
    }
}
