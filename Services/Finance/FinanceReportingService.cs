using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.Finance;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Finance;

public sealed class FinanceReportingService : IFinanceReportingService
{
    private readonly AppDbContext _db;

    public FinanceReportingService(AppDbContext db) => _db = db;

    public async Task<FinanceSummaryDto> GetSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var (start, end) = NormalizeRange(from, to);
        var transfers = await _db.Transfers.AsNoTracking()
            .Where(x => !x.IsDeleted && x.CreatedAt >= start && x.CreatedAt < end)
            .Select(x => new
            {
                x.Status,
                x.SourceCurrencyCode,
                x.DestinationCurrencyCode,
                x.SourceAmount,
                x.DestinationAmount,
                x.FeeAmount,
                x.FeeCurrencyCode,
                x.CustomerRate,
                x.ProviderRate
            })
            .ToListAsync(ct);

        var completed = transfers.Where(x => x.Status == TransferStatus.Completed).ToList();
        var corridorRows = completed
            .GroupBy(x => new { x.SourceCurrencyCode, x.DestinationCurrencyCode, x.FeeCurrencyCode })
            .Select(g => new FinanceCorridorDto
            {
                SourceCurrencyCode = g.Key.SourceCurrencyCode,
                DestinationCurrencyCode = g.Key.DestinationCurrencyCode,
                FeeCurrencyCode = g.Key.FeeCurrencyCode,
                TransferCount = g.Count(),
                SourceVolume = g.Sum(x => x.SourceAmount),
                DestinationVolume = g.Sum(x => x.DestinationAmount),
                FeeRevenue = g.Sum(x => x.FeeAmount),
                IndicativeSpreadValue = g.Sum(x => Math.Max(0m, x.ProviderRate - x.CustomerRate) * x.SourceAmount),
                IndicativeSpreadCurrencyCode = g.Key.DestinationCurrencyCode
            })
            .OrderByDescending(x => x.SourceVolume)
            .ToList();

        var settlementVariance = await _db.SettlementBatches.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ReconciledAt >= start && x.ReconciledAt < end && x.VarianceAmount != null)
            .Select(x => x.VarianceAmount!.Value)
            .ToListAsync(ct);

        return new FinanceSummaryDto
        {
            From = start,
            To = end,
            CompletedTransferCount = completed.Count,
            RefundedTransferCount = transfers.Count(x => x.Status == TransferStatus.Refunded),
            FailedTransferCount = transfers.Count(x => x.Status == TransferStatus.Failed),
            SourceVolumes = completed.GroupBy(x => x.SourceCurrencyCode)
                .Select(g => new FinanceCurrencyAmountDto { CurrencyCode = g.Key, Amount = g.Sum(x => x.SourceAmount) })
                .OrderBy(x => x.CurrencyCode).ToList(),
            FeeRevenue = completed.GroupBy(x => x.FeeCurrencyCode)
                .Select(g => new FinanceCurrencyAmountDto { CurrencyCode = g.Key, Amount = g.Sum(x => x.FeeAmount) })
                .OrderBy(x => x.CurrencyCode).ToList(),
            Corridors = corridorRows,
            SettlementVarianceAbsoluteTotal = settlementVariance.Sum(x => Math.Abs(x))
        };
    }

    public async Task<byte[]> ExportCsvAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var summary = await GetSummaryAsync(from, to, ct);
        var sb = new StringBuilder();
        sb.AppendLine("source_currency,destination_currency,transfer_count,source_volume,destination_volume,fee_revenue,fee_currency,indicative_spread_value,indicative_spread_currency");
        foreach (var row in summary.Corridors)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Escape(row.SourceCurrencyCode), Escape(row.DestinationCurrencyCode), row.TransferCount.ToString(),
                row.SourceVolume.ToString("0.00"), row.DestinationVolume.ToString("0.00"), row.FeeRevenue.ToString("0.00"),
                Escape(row.FeeCurrencyCode), row.IndicativeSpreadValue.ToString("0.00"), Escape(row.IndicativeSpreadCurrencyCode)
            }));
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static (DateTime Start, DateTime End) NormalizeRange(DateTime? from, DateTime? to)
    {
        var end = to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : DateTime.UtcNow;
        var start = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : end.AddDays(-30);
        if (end <= start) throw new InvalidOperationException("Report end must be after report start.");
        if ((end - start).TotalDays > 366) throw new InvalidOperationException("Finance report range cannot exceed 366 days.");
        return (start, end);
    }

    private static string Escape(string value)
    {
        if (!value.Contains(',') && !value.Contains('"'))
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
