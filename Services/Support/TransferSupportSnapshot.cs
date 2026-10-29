using System.Globalization;
using KorridorX.Models.Transfers;

namespace KorridorX.Services.Support;

public static class TransferSupportSnapshot
{
    // A point-in-time support message, separate from the live transfer link.
    // Do not include full destination accounts, tokens or provider response bodies.
    public static string Build(Transfer transfer, string description)
    {
        static string Clean(string? value, int maximum = 200)
        {
            var text = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= maximum ? text : text[..maximum];
        }
        static string Amount(decimal amount) =>
            amount.ToString("0.00################", CultureInfo.InvariantCulture);
        static string Utc(DateTime at) =>
            DateTime.SpecifyKind(at, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

        var recipient = transfer.Recipient is null ? "Recipient" :
            $"{transfer.Recipient.FirstName} {transfer.Recipient.LastName}";
        var feeCurrency = string.IsNullOrWhiteSpace(transfer.FeeCurrencyCode)
            ? transfer.SourceCurrencyCode : transfer.FeeCurrencyCode;
        var lines = new List<string> {
            description.Trim(), "", "Transaction details (recorded when reported)",
            $"Reference: {Clean(transfer.Reference, 50)}",
            $"Transaction ID: {transfer.Id}",
            $"Status: {transfer.Status}",
            $"Created at (UTC): {Utc(transfer.CreatedAt)}",
            $"Sent: {Amount(transfer.SourceAmount)} {Clean(transfer.SourceCurrencyCode, 10)}",
            $"Recipient amount: {Amount(transfer.DestinationAmount)} {Clean(transfer.DestinationCurrencyCode, 10)}",
            $"Fee: {Amount(transfer.FeeAmount)} {Clean(feeCurrency, 10)}",
            $"Total charge: {Amount(transfer.TotalPayableAmount)} {Clean(transfer.SourceCurrencyCode, 10)}",
            $"Exchange rate: {transfer.CustomerRate.ToString("0.##################", CultureInfo.InvariantCulture)}",
            $"Recipient: {Clean(recipient)}",
            $"Destination country: {Clean(transfer.DestinationCountryCode, 10)}"
        };
        if (transfer.CompletedAt.HasValue)
            lines.Add($"Completed at (UTC): {Utc(transfer.CompletedAt.Value)}");
        if (!string.IsNullOrWhiteSpace(transfer.ProviderReference))
            lines.Add($"Provider reference: {Clean(transfer.ProviderReference)}");
        return string.Join("\n", lines);
    }
}
