using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Services.Notifications;
namespace KorridorX.Tests;
public sealed class TransferCompletionNotificationTests
{
    [Fact]
    public void Receipt_preserves_the_agreed_recipient_amount_and_original_customer_charge()
    {
        var receipt = TransferCompletionNotificationStager.BuildReceipt(Transfer());
        Assert.Contains("NGN 4,795.20", receipt.Body);
        Assert.Contains("CAD 5.00", receipt.Body);
        Assert.Contains("CAD 1.00", receipt.Body);
        Assert.Contains("CAD 6.00", receipt.Body);
        Assert.Contains("KXTR-20261027-689914", receipt.Subject);
    }
    [Fact]
    public void Receipt_encodes_reference_text()
    {
        var transfer = Transfer(); transfer.Reference = "<script>example</script>";
        var receipt = TransferCompletionNotificationStager.BuildReceipt(transfer);
        Assert.DoesNotContain("<script>", receipt.Body);
        Assert.Contains("&lt;script&gt;", receipt.Body);
    }
    [Fact]
    public void A_processing_transfer_cannot_be_described_as_delivered()
    {
        var transfer = Transfer(); transfer.Status = TransferStatus.Processing;
        Assert.Throws<InvalidOperationException>(() =>
            TransferCompletionNotificationStager.BuildReceipt(transfer));
    }
    private static Transfer Transfer() => new() { Reference = "KXTR-20261027-689914",
        Status = TransferStatus.Completed, SourceCurrencyCode = "CAD", SourceAmount = 5m,
        DestinationCurrencyCode = "NGN", DestinationAmount = 4795.20m,
        FeeCurrencyCode = "CAD", FeeAmount = 1m, TotalPayableAmount = 6m };
}
