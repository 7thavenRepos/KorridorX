using System.Globalization;
using KorridorX.Data;
using KorridorX.Dtos.Transfers;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Transfers;

public sealed class TransferReceiptService(AppDbContext db)
{
    public async Task<TransferReceiptDto> GetAsync(
        Guid userId, Guid transferId, CancellationToken ct = default)
    {
        var transfer = await db.Transfers
            .AsNoTracking()
            .Include(x => x.CustomerProfile)
            .Include(x => x.Recipient)
            .Include(x => x.RecipientBankAccount)
            .FirstOrDefaultAsync(x =>
                x.Id == transferId && !x.IsDeleted &&
                x.BusinessProfileId == null &&
                x.CustomerProfile != null && !x.CustomerProfile.IsDeleted &&
                x.CustomerProfile.UserId == userId, ct)
            ?? throw new InvalidOperationException("Transfer not found or is not accessible to the authenticated user.");

        if (transfer.Status != TransferStatus.Completed)
            throw new InvalidOperationException("A delivery receipt is available only for a completed transfer.");

        var completedAt = transfer.CompletedAt;
        if (!completedAt.HasValue)
        {
            completedAt = await db.TransferTimelineEvents.AsNoTracking()
                .Where(x => x.TransferId == transferId && !x.IsDeleted &&
                    x.EventType == "TRANSFER_COMPLETED")
                .OrderByDescending(x => x.OccurredAt)
                .Select(x => (DateTime?)x.OccurredAt)
                .FirstOrDefaultAsync(ct);
        }

        var bank = transfer.RecipientBankAccount;
        var account = bank?.AccountNumber;
        var masked = !string.IsNullOrWhiteSpace(account) && account.Length >= 4
            ? "****" + account[^4..] : null;
        static string Name(string first, string last, string fallback)
        {
            var name = string.Join(" ", new[] { first, last }
                .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            return name.Length == 0 ? fallback : name;
        }
        return new TransferReceiptDto(
            transfer.Id, transfer.Reference, "Completed",
            Name(transfer.CustomerProfile!.FirstName, transfer.CustomerProfile.LastName, "Sender"),
            transfer.Recipient is null ? "Recipient" :
                Name(transfer.Recipient.FirstName, transfer.Recipient.LastName, "Recipient"),
            transfer.SourceCurrencyCode, Amount(transfer.SourceAmount),
            transfer.DestinationCurrencyCode, Amount(transfer.DestinationAmount),
            string.IsNullOrWhiteSpace(transfer.FeeCurrencyCode)
                ? transfer.SourceCurrencyCode : transfer.FeeCurrencyCode,
            Amount(transfer.FeeAmount), Amount(transfer.TotalPayableAmount),
            transfer.CustomerRate.ToString("0.##################", CultureInfo.InvariantCulture),
            bank?.BankName, masked, transfer.CreatedAt, completedAt, DateTime.UtcNow);
    }

    private static string Amount(decimal amount) =>
        amount.ToString("0.00################", CultureInfo.InvariantCulture);
}
