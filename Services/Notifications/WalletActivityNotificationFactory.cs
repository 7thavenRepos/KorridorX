using System.Globalization;
using System.Text.Encodings.Web;

namespace KorridorX.Services.Notifications;

public static class WalletActivityNotificationFactory
{
    public const string FundingEntityType = "WalletFunding";

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
