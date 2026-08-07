using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Compliance;

public sealed class TransactionMonitoringService : ITransactionMonitoringService
{
    private const string FlagType = "TRANSACTION_MONITORING";

    private readonly AppDbContext _db;
    private readonly IComplianceCaseService _caseService;
    private readonly TransactionMonitoringOptions _options;

    public TransactionMonitoringService(
        AppDbContext db,
        IComplianceCaseService caseService,
        IOptions<ComplianceScreeningOptions> options)
    {
        _db = db;
        _caseService = caseService;
        _options = options.Value.TransactionMonitoring;
    }

    public async Task<TransactionMonitoringDecisionDto> MonitorAsync(
        Transfer transfer,
        Guid initiatedByUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        if (!_options.IsEnabled)
            return new TransactionMonitoringDecisionDto(
                transfer.Id,
                0,
                RiskLevel.Low,
                RiskDecision.Allow,
                transfer.IsComplianceHold,
                [],
                DateTime.UtcNow);

        var rules = new List<MonitoringRuleHit>();
        var ownerQuery = _db.Transfers.AsNoTracking().Where(x =>
            x.Id != transfer.Id &&
            !x.IsDeleted &&
            x.Status != TransferStatus.Cancelled &&
            x.Status != TransferStatus.Rejected &&
            x.Status != TransferStatus.Refunded);
        ownerQuery = transfer.BusinessProfileId.HasValue
            ? ownerQuery.Where(x => x.BusinessProfileId == transfer.BusinessProfileId)
            : ownerQuery.Where(x => x.CustomerProfileId == transfer.CustomerProfileId);

        var tracked = _db.ChangeTracker.Entries<Transfer>()
            .Where(x =>
                x.State == EntityState.Added &&
                x.Entity.Id != transfer.Id &&
                !x.Entity.IsDeleted &&
                (transfer.BusinessProfileId.HasValue
                    ? x.Entity.BusinessProfileId == transfer.BusinessProfileId
                    : x.Entity.CustomerProfileId == transfer.CustomerProfileId))
            .Select(x => x.Entity)
            .ToList();

        if (_options.RollingAmountThresholds.TryGetValue(transfer.SourceCurrencyCode, out var threshold))
        {
            var rollingSince = DateTime.UtcNow.AddHours(-24);
            var rollingAmount = await ownerQuery
                .Where(x => x.CreatedAt >= rollingSince && x.SourceCurrencyCode == transfer.SourceCurrencyCode)
                .SumAsync(x => (decimal?)x.TotalPayableAmount, ct) ?? 0m;
            rollingAmount += tracked
                .Where(x => x.CreatedAt >= rollingSince && x.SourceCurrencyCode == transfer.SourceCurrencyCode)
                .Sum(x => x.TotalPayableAmount);
            rollingAmount += transfer.TotalPayableAmount;
            if (rollingAmount >= threshold)
                rules.Add(new("ROLLING_AMOUNT_THRESHOLD", 30,
                    $"The rolling 24-hour transfer total of {rollingAmount:0.##} {transfer.SourceCurrencyCode} meets the configured threshold."));

            var structuringSince = DateTime.UtcNow.AddHours(-_options.StructuringWindowHours);
            var lowerBound = threshold * _options.StructuringLowerPercentage;
            if (transfer.TotalPayableAmount >= lowerBound && transfer.TotalPayableAmount < threshold)
            {
                var count = await ownerQuery.CountAsync(x =>
                    x.CreatedAt >= structuringSince &&
                    x.SourceCurrencyCode == transfer.SourceCurrencyCode &&
                    x.TotalPayableAmount >= lowerBound &&
                    x.TotalPayableAmount < threshold,
                    ct);
                count += tracked.Count(x =>
                    x.CreatedAt >= structuringSince &&
                    x.SourceCurrencyCode == transfer.SourceCurrencyCode &&
                    x.TotalPayableAmount >= lowerBound &&
                    x.TotalPayableAmount < threshold);
                count += 1;
                if (count >= _options.StructuringTransferCount)
                    rules.Add(new("POTENTIAL_STRUCTURING", 45,
                        $"{count} transfers were created below the configured threshold within {_options.StructuringWindowHours} hours."));
            }

            var corridorSince = DateTime.UtcNow.AddDays(-_options.NewCorridorLookbackDays);
            var knownCorridor = await ownerQuery.AnyAsync(x =>
                x.CreatedAt >= corridorSince &&
                x.DestinationCountryCode == transfer.DestinationCountryCode &&
                x.DestinationCurrencyCode == transfer.DestinationCurrencyCode,
                ct);
            knownCorridor = knownCorridor || tracked.Any(x =>
                x.CreatedAt >= corridorSince &&
                x.DestinationCountryCode == transfer.DestinationCountryCode &&
                x.DestinationCurrencyCode == transfer.DestinationCurrencyCode);
            if (!knownCorridor && transfer.TotalPayableAmount >= threshold * 0.5m)
                rules.Add(new("NEW_HIGH_VALUE_CORRIDOR", 20,
                    "This is a new destination corridor for the account and the transfer amount is material."));
        }

        var beneficiarySince = DateTime.UtcNow.AddHours(-_options.MultipleBeneficiariesWindowHours);
        var beneficiaryIds = await ownerQuery
            .Where(x => x.CreatedAt >= beneficiarySince)
            .Select(x => x.BusinessBeneficiaryId.HasValue
                ? x.BusinessBeneficiaryId.Value
                : x.RecipientId ?? Guid.Empty)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToListAsync(ct);
        beneficiaryIds.AddRange(tracked
            .Where(x => x.CreatedAt >= beneficiarySince)
            .Select(x => x.BusinessBeneficiaryId ?? x.RecipientId ?? Guid.Empty)
            .Where(x => x != Guid.Empty));
        var currentBeneficiaryId = transfer.BusinessBeneficiaryId ?? transfer.RecipientId;
        if (currentBeneficiaryId.HasValue)
            beneficiaryIds.Add(currentBeneficiaryId.Value);
        if (beneficiaryIds.Distinct().Count() >= _options.MultipleBeneficiariesCount)
            rules.Add(new("MULTIPLE_BENEFICIARIES", 20,
                "The account has sent or prepared payments to several beneficiaries in a short period."));

        var score = Math.Clamp(rules.Sum(x => x.Points), 0, 100);
        var resolved = TransferRiskPolicy.Resolve(score, _options.ReviewScore, _options.BlockScore);
        var monitoredAt = DateTime.UtcNow;
        var wasOnHold = transfer.IsComplianceHold;

        if ((int)resolved.Decision > (int)transfer.RiskDecision)
            transfer.RiskDecision = resolved.Decision;
        if ((int)resolved.Level > (int)transfer.RiskLevel)
            transfer.RiskLevel = resolved.Level;
        transfer.RiskScore = Math.Max(transfer.RiskScore, score);
        transfer.RiskAssessedAt = monitoredAt;
        transfer.LastUpdatedAt = monitoredAt;

        _db.ComplianceChecks.Add(new ComplianceCheck
        {
            CustomerProfileId = transfer.CustomerProfileId,
            TransferId = transfer.Id,
            CheckType = "TRANSACTION_MONITORING",
            Status = resolved.Decision.ToString().ToUpperInvariant(),
            ResultJson = JsonSerializer.Serialize(new
            {
                score,
                level = resolved.Level.ToString(),
                decision = resolved.Decision.ToString(),
                rules
            }),
            CheckedAt = monitoredAt
        });

        if (resolved.Decision != RiskDecision.Allow)
        {
            var blocking = resolved.Decision == RiskDecision.Block;
            var existingFlag = await _db.AmlFlags.FirstOrDefaultAsync(x =>
                x.TransferId == transfer.Id &&
                x.FlagType == FlagType &&
                !x.IsResolved &&
                !x.IsDeleted,
                ct);
            var flag = existingFlag ?? new AmlFlag
            {
                CustomerProfileId = transfer.CustomerProfileId,
                TransferId = transfer.Id,
                FlagType = FlagType,
                CreatedByUserId = initiatedByUserId
            };
            flag.Severity = resolved.Level.ToString();
            flag.RiskScore = score;
            flag.IsBlocking = blocking;
            flag.Description = string.Join(" ", rules.Select(x => x.Description));
            flag.LastUpdatedAt = DateTime.UtcNow;
            if (existingFlag is null)
                _db.AmlFlags.Add(flag);

            var priority = resolved.Level switch
            {
                RiskLevel.Critical => ComplianceCasePriority.Critical,
                RiskLevel.High => ComplianceCasePriority.High,
                RiskLevel.Medium => ComplianceCasePriority.Medium,
                _ => ComplianceCasePriority.Low
            };
            await _caseService.OpenTransactionMonitoringCaseAsync(
                transfer.Id,
                transfer.CustomerProfileId,
                transfer.BusinessProfileId,
                flag,
                priority,
                $"Transaction monitoring review: {transfer.Reference}",
                flag.Description,
                initiatedByUserId,
                ct);

            if (blocking)
            {
                transfer.IsComplianceHold = true;
                transfer.ComplianceHoldReason = flag.Description;
                transfer.RiskDecision = RiskDecision.Block;
                transfer.RiskLevel = resolved.Level;
                transfer.LastUpdatedAt = DateTime.UtcNow;
                if (!wasOnHold)
                {
                    _db.TransferTimelineEvents.Add(new TransferTimelineEvent
                    {
                        TransferId = transfer.Id,
                        EventType = "TRANSACTION_MONITORING_HOLD",
                        Title = "Transaction monitoring review required",
                        Description = "The transfer is on hold while transaction-monitoring alerts are reviewed.",
                        OccurredAt = DateTime.UtcNow
                    });
                }
            }
        }

        return new TransactionMonitoringDecisionDto(
            transfer.Id,
            score,
            resolved.Level,
            resolved.Decision,
            transfer.IsComplianceHold,
            rules.Select(x => x.Code).ToList(),
            monitoredAt);
    }

    private sealed record MonitoringRuleHit(string Code, int Points, string Description);
}
