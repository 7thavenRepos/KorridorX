using System.Globalization;
using System.Text;
using KorridorX.Data;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.BusinessTransfers;

public class BusinessReportExportService : IBusinessReportExportService
{
    private readonly AppDbContext _db;
    private readonly IBusinessAccessService _accessService;

    public BusinessReportExportService(AppDbContext db, IBusinessAccessService accessService)
    {
        _db = db;
        _accessService = accessService;
    }

    public async Task<byte[]> ExportTransfersCsvAsync(
        Guid userId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewReports, ct);
        var query = _db.Transfers.AsNoTracking()
            .Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted);

        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.ToUniversalTime().AddDays(1));

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Reference,
                Beneficiary = x.BusinessBeneficiary!.Name,
                x.SourceCurrencyCode,
                x.SourceAmount,
                x.FeeAmount,
                x.TotalPayableAmount,
                x.DestinationCurrencyCode,
                x.DestinationAmount,
                x.Status,
                x.ApprovalStatus,
                x.BusinessFundingSource,
                x.CreatedAt,
                x.CompletedAt
            })
            .ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("reference,beneficiary,sourceCurrency,sourceAmount,feeAmount,totalPayable,destinationCurrency,destinationAmount,status,approvalStatus,fundingSource,createdAt,completedAt");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',',
                Csv(row.Reference),
                Csv(row.Beneficiary),
                Csv(row.SourceCurrencyCode),
                Number(row.SourceAmount),
                Number(row.FeeAmount),
                Number(row.TotalPayableAmount),
                Csv(row.DestinationCurrencyCode),
                Number(row.DestinationAmount),
                Csv(row.Status.ToString()),
                Csv(row.ApprovalStatus.ToString()),
                Csv(row.BusinessFundingSource?.ToString() ?? ""),
                Csv(row.CreatedAt.ToString("O")),
                Csv(row.CompletedAt?.ToString("O") ?? "")));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    public async Task<byte[]> ExportBatchCsvAsync(
        Guid userId,
        Guid batchId,
        CancellationToken ct = default)
    {
        var access = await _accessService.EnsurePermissionAsync(userId, BusinessPermission.ViewBatches, ct);
        var batch = await _db.BusinessPaymentBatches.AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(x => x.BusinessBeneficiary)
            .FirstOrDefaultAsync(x =>
                x.Id == batchId &&
                x.BusinessProfileId == access.BusinessProfileId &&
                !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Business payment batch not found.");

        var csv = new StringBuilder();
        csv.AppendLine("rowNumber,externalReference,beneficiary,destinationCountry,destinationCurrency,sourceAmount,destinationAmount,feeAmount,totalPayable,status,transferId,validationErrors");
        foreach (var item in batch.Items.OrderBy(x => x.RowNumber))
        {
            csv.AppendLine(string.Join(',',
                item.RowNumber.ToString(CultureInfo.InvariantCulture),
                Csv(item.ExternalReference ?? ""),
                Csv(item.BusinessBeneficiary?.Name ?? ""),
                Csv(item.DestinationCountryCode),
                Csv(item.DestinationCurrencyCode),
                Number(item.SourceAmount),
                Number(item.DestinationAmount),
                Number(item.FeeAmount),
                Number(item.TotalPayableAmount),
                Csv(item.Status.ToString()),
                Csv(item.TransferId?.ToString() ?? ""),
                Csv(item.ValidationErrors ?? "")));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";
}
