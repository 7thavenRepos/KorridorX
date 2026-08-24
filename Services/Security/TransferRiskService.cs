using System.Text.Json;
using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Compliance;
using KorridorX.Extensions;
using KorridorX.Exceptions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Services.Audit;
using KorridorX.Services.Compliance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Security;

public sealed class TransferRiskService : ITransferRiskService
{
    private const string TransferRiskFlagType = "TRANSFER_RISK";

    private readonly AppDbContext _db;
    private readonly TransferRiskOptions _options;
    private readonly IAuditService _audit;
    private readonly IComplianceCaseService _caseService;

    public TransferRiskService(
        AppDbContext db,
        IOptions<SecurityOptions> options,
        IAuditService audit,
        IComplianceCaseService caseService)
    {
        _db = db;
        _options = options.Value.TransferRisk;
        _audit = audit;
        _caseService = caseService;
    }

    public async Task<TransferRiskDecisionDto> AssessAsync(
        Transfer transfer,
        Guid initiatedByUserId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        if (!_options.IsEnabled)
        {
            ApplyDecision(transfer, 0, RiskLevel.Low, RiskDecision.Allow, null);
            return ToDecisionDto(transfer, []);
        }

        var hits = new List<RiskRuleHit>();
        var ownerQuery = _db.Transfers.AsNoTracking().Where(x =>
            x.Id != transfer.Id &&
            !x.IsDeleted &&
            x.Status != TransferStatus.Cancelled &&
            x.Status != TransferStatus.Rejected &&
            x.Status != TransferStatus.Refunded);

        ownerQuery = transfer.BusinessProfileId.HasValue
            ? ownerQuery.Where(x => x.BusinessProfileId == transfer.BusinessProfileId)
            : ownerQuery.Where(x => x.CustomerProfileId == transfer.CustomerProfileId);

        if (_options.HighValueThresholds.TryGetValue(transfer.SourceCurrencyCode, out var threshold))
        {
            if (transfer.TotalPayableAmount >= threshold * 2m)
                hits.Add(new("VERY_HIGH_VALUE", 40, "Transfer amount is at least twice the configured high-value threshold."));
            else if (transfer.TotalPayableAmount >= threshold)
                hits.Add(new("HIGH_VALUE", 25, "Transfer amount meets the configured high-value threshold."));
        }

        var trackedOwnerTransfers = _db.ChangeTracker.Entries<Transfer>()
            .Where(x =>
                x.State == EntityState.Added &&
                x.Entity.Id != transfer.Id &&
                !x.Entity.IsDeleted &&
                (transfer.BusinessProfileId.HasValue
                    ? x.Entity.BusinessProfileId == transfer.BusinessProfileId
                    : x.Entity.CustomerProfileId == transfer.CustomerProfileId))
            .Select(x => x.Entity)
            .ToList();

        var rapidSince = DateTime.UtcNow.AddMinutes(-_options.RapidTransferWindowMinutes);
        var rapidCount = await ownerQuery.CountAsync(x => x.CreatedAt >= rapidSince, ct) +
                         trackedOwnerTransfers.Count(x => x.CreatedAt >= rapidSince);
        if (rapidCount >= _options.RapidTransferCount)
            hits.Add(new("RAPID_TRANSFER_VELOCITY", 25, "Several transfers were created within a short period."));

        var startOfDay = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var dailyCount = await ownerQuery.CountAsync(x => x.CreatedAt >= startOfDay, ct) +
                         trackedOwnerTransfers.Count(x => x.CreatedAt >= startOfDay);
        if (dailyCount >= _options.DailyTransferCount)
            hits.Add(new("HIGH_DAILY_TRANSFER_COUNT", 20, "The account has created an unusually high number of transfers today."));

        var duplicateSince = DateTime.UtcNow.AddHours(-_options.DuplicateTransferWindowHours);
        var duplicateQuery = ownerQuery.Where(x =>
            x.CreatedAt >= duplicateSince &&
            x.SourceAmount == transfer.SourceAmount &&
            x.DestinationCurrencyCode == transfer.DestinationCurrencyCode);

        duplicateQuery = transfer.BusinessBeneficiaryId.HasValue
            ? duplicateQuery.Where(x => x.BusinessBeneficiaryId == transfer.BusinessBeneficiaryId)
            : duplicateQuery.Where(x => x.RecipientId == transfer.RecipientId);

        var duplicateCount = await duplicateQuery.CountAsync(ct) +
            trackedOwnerTransfers.Count(x =>
                x.CreatedAt >= duplicateSince &&
                x.SourceAmount == transfer.SourceAmount &&
                x.DestinationCurrencyCode == transfer.DestinationCurrencyCode &&
                (transfer.BusinessBeneficiaryId.HasValue
                    ? x.BusinessBeneficiaryId == transfer.BusinessBeneficiaryId
                    : x.RecipientId == transfer.RecipientId));
        if (duplicateCount >= _options.DuplicateTransferCount)
            hits.Add(new("REPEATED_SIMILAR_TRANSFER", 20, "Multiple similar transfers were recently created for the same recipient."));

        var recipientCreatedAt = await ResolveRecipientCreatedAtAsync(transfer, ct);
        if (recipientCreatedAt.HasValue &&
            recipientCreatedAt.Value >= DateTime.UtcNow.AddHours(-_options.RecentRecipientHours))
        {
            hits.Add(new("NEW_RECIPIENT", 15, "The recipient was added recently."));
        }

        var failedLoginSince = DateTime.UtcNow.AddHours(-_options.FailedLoginWindowHours);
        var failedLogins = await _db.LoginHistories
            .AsNoTracking()
            .CountAsync(x =>
                x.UserId == initiatedByUserId &&
                !x.WasSuccessful &&
                x.OccurredAt >= failedLoginSince,
                ct);
        if (failedLogins >= _options.FailedLoginCount)
            hits.Add(new("RECENT_FAILED_LOGINS", 15, "The account has several recent failed login attempts."));

        var deviceSince = DateTime.UtcNow.AddDays(-_options.DistinctDeviceWindowDays);
        var distinctDevices = await _db.LoginHistories
            .AsNoTracking()
            .Where(x =>
                x.UserId == initiatedByUserId &&
                x.WasSuccessful &&
                x.OccurredAt >= deviceSince &&
                x.DeviceFingerprint != null)
            .Select(x => x.DeviceFingerprint!)
            .Distinct()
            .CountAsync(ct);
        if (distinctDevices >= _options.DistinctDeviceCount)
            hits.Add(new("MULTIPLE_RECENT_DEVICES", 10, "The account has recently used several distinct devices."));

        if (transfer.BusinessProfileId.HasValue &&
            transfer.ApprovalStatus == BusinessApprovalStatus.NotRequired &&
            threshold > 0 &&
            transfer.TotalPayableAmount >= threshold)
        {
            hits.Add(new("HIGH_VALUE_WITHOUT_APPROVAL", 10, "A high-value business transfer did not require maker-checker approval."));
        }

        var score = Math.Clamp(hits.Sum(x => x.Points), 0, 100);
        var resolved = TransferRiskPolicy.Resolve(score, _options.ReviewScore, _options.BlockScore);
        var wasOnHold = transfer.IsComplianceHold;
        var effectiveDecision = wasOnHold && resolved.Decision == RiskDecision.Allow
            ? RiskDecision.Review
            : resolved.Decision;
        var effectiveLevel = effectiveDecision == RiskDecision.Review && (int)resolved.Level < (int)RiskLevel.High
            ? RiskLevel.High
            : resolved.Level;
        var holdReason = effectiveDecision == RiskDecision.Allow
            ? null
            : hits.Count > 0
                ? string.Join(" ", hits.Select(x => x.Description))
                : "The risk score has reduced, but the existing compliance hold requires manual release.";

        ApplyDecision(transfer, score, effectiveLevel, effectiveDecision, holdReason);

        _db.ComplianceChecks.Add(new ComplianceCheck
        {
            CustomerProfileId = transfer.CustomerProfileId,
            TransferId = transfer.Id,
            CheckType = "TRANSFER_RISK",
            Status = effectiveDecision.ToString().ToUpperInvariant(),
            ResultJson = JsonSerializer.Serialize(new
            {
                score,
                level = effectiveLevel.ToString(),
                decision = effectiveDecision.ToString(),
                rules = hits
            }),
            CheckedAt = DateTime.UtcNow
        });

        if (transfer.IsComplianceHold)
        {
            var existingFlag = await _db.AmlFlags.FirstOrDefaultAsync(x =>
                x.TransferId == transfer.Id &&
                x.FlagType == TransferRiskFlagType &&
                !x.IsResolved &&
                !x.IsDeleted,
                ct);

            if (existingFlag is null)
            {
                _db.AmlFlags.Add(new AmlFlag
                {
                    CustomerProfileId = transfer.CustomerProfileId,
                    TransferId = transfer.Id,
                    FlagType = TransferRiskFlagType,
                    Severity = effectiveLevel.ToString(),
                    RiskScore = score,
                    IsBlocking = true,
                    Description = holdReason ?? "Transfer requires compliance review.",
                    CreatedByUserId = initiatedByUserId
                });
            }
            else
            {
                existingFlag.Severity = effectiveLevel.ToString();
                existingFlag.RiskScore = score;
                existingFlag.Description = holdReason ?? existingFlag.Description;
                existingFlag.LastUpdatedAt = DateTime.UtcNow;
            }

            if (!wasOnHold)
            {
                _db.TransferTimelineEvents.Add(new TransferTimelineEvent
                {
                    TransferId = transfer.Id,
                    EventType = "COMPLIANCE_REVIEW_REQUIRED",
                    Title = "Transfer under review",
                    Description = "The transfer requires an additional compliance review before payout.",
                    OccurredAt = DateTime.UtcNow
                });
            }
        }

        return ToDecisionDto(transfer, hits.Select(x => x.Code).ToArray());
    }

    public void EnsureCanProceedToPayout(Transfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        if (transfer.IsComplianceHold)
        {
            throw new ComplianceHoldException(
                "This transfer is on compliance hold and cannot be submitted for payout until it is reviewed.");
        }
    }

    public Task<PagedResult<AmlFlagDto>> GetFlagsAsync(
        bool? isResolved,
        string? severity,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.AmlFlags.AsNoTracking().Where(x => !x.IsDeleted);

        if (isResolved.HasValue)
            query = query.Where(x => x.IsResolved == isResolved.Value);
        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalized = severity.Trim();
            query = query.Where(x => x.Severity == normalized);
        }

        return query
            .OrderBy(x => x.IsResolved)
            .ThenByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new AmlFlagDto(
                x.Id,
                x.CustomerProfileId,
                x.TransferId,
                x.Transfer != null ? x.Transfer.Reference : null,
                x.FlagType,
                x.Severity,
                x.RiskScore,
                x.IsBlocking,
                x.Description,
                x.IsResolved,
                x.ReviewDecision,
                x.CreatedAt,
                x.ReviewedAt,
                x.ResolvedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<AmlFlagDetailsDto> GetFlagAsync(
        Guid flagId,
        CancellationToken ct = default)
    {
        var flag = await _db.AmlFlags
            .AsNoTracking()
            .Include(x => x.Transfer)
            .FirstOrDefaultAsync(x => x.Id == flagId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("AML flag not found.");

        return ToDetailsDto(flag);
    }

    public async Task<AmlFlagDetailsDto> ReviewFlagAsync(
        Guid flagId,
        Guid reviewedByUserId,
        ReviewAmlFlagRequestDto request,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(typeof(RiskReviewDecision), request.Decision))
            throw new InvalidOperationException("A valid review decision is required.");

        var note = request.Note?.Trim();
        if (string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException("A review note is required.");

        var flag = await _db.AmlFlags
            .Include(x => x.Transfer)
            .FirstOrDefaultAsync(x => x.Id == flagId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("AML flag not found.");

        if (flag.IsResolved)
            return ToDetailsDto(flag);

        flag.ReviewedAt = DateTime.UtcNow;
        flag.ReviewedByUserId = reviewedByUserId;
        flag.ReviewDecision = request.Decision.ToString();
        flag.ResolutionNote = note;
        flag.LastUpdatedAt = DateTime.UtcNow;
        flag.LastUpdatedByUserId = reviewedByUserId;

        if (request.Decision == RiskReviewDecision.Release)
        {
            flag.IsResolved = true;
            flag.ResolvedAt = DateTime.UtcNow;
            flag.ResolvedByUserId = reviewedByUserId;

            if (flag.Transfer is not null)
            {
                var otherBlockingFlags = await _db.AmlFlags.AsNoTracking().AnyAsync(x =>
                    x.TransferId == flag.TransferId &&
                    x.Id != flag.Id &&
                    x.IsBlocking &&
                    !x.IsResolved &&
                    !x.IsDeleted,
                    ct);

                var blockingCase = await _caseService.HasBlockingCaseAsync(
                    flag.Transfer.Id,
                    flag.Transfer.CustomerProfileId,
                    flag.Transfer.BusinessProfileId,
                    flag.Transfer.RecipientId,
                    flag.Transfer.BusinessBeneficiaryId,
                    ct);

                if (!otherBlockingFlags && !blockingCase)
                {
                    flag.Transfer.IsComplianceHold = false;
                    flag.Transfer.ComplianceHoldReason = null;
                    flag.Transfer.ComplianceReviewedAt = DateTime.UtcNow;
                    flag.Transfer.ComplianceReviewedByUserId = reviewedByUserId;
                    flag.Transfer.LastUpdatedAt = DateTime.UtcNow;
                    flag.Transfer.LastUpdatedByUserId = reviewedByUserId;

                    _db.TransferTimelineEvents.Add(new TransferTimelineEvent
                    {
                        TransferId = flag.Transfer.Id,
                        EventType = "COMPLIANCE_HOLD_RELEASED",
                        Title = "Compliance review completed",
                        Description = "The compliance hold has been released and transfer processing may continue.",
                        OccurredAt = DateTime.UtcNow
                    });
                }
            }
        }

        _audit.Stage(new AuditRecordRequest(
            "AML_FLAG_REVIEWED",
            "Compliance",
            nameof(AmlFlag),
            flag.Id.ToString(),
            null,
            new
            {
                request.Decision,
                Note = note,
                flag.TransferId,
                flag.RiskScore,
                flag.IsResolved
            },
            null,
            reviewedByUserId));

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(flag);
    }

    public async Task<TransferRiskDecisionDto> ReassessAsync(
        Guid transferId,
        Guid reviewedByUserId,
        string reason,
        CancellationToken ct = default)
    {
        var cleanedReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(cleanedReason))
            throw new InvalidOperationException("A reason is required for transfer risk reassessment.");

        if (cleanedReason.Length > 1000)
            throw new InvalidOperationException("Reason cannot exceed 1000 characters.");

        var transfer = await _db.Transfers
            .FirstOrDefaultAsync(
                x => x.Id == transferId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Transfer not found.");

        var result = await AssessAsync(
            transfer,
            reviewedByUserId,
            ct);

        _audit.Stage(new AuditRecordRequest(
            "TRANSFER_RISK_REASSESSED",
            "Compliance",
            nameof(Transfer),
            transfer.Id.ToString(),
            null,
            new
            {
                Reason = cleanedReason,
                result.Score,
                result.Level,
                result.Decision,
                result.IsComplianceHold,
                result.TriggeredRules,
                result.AssessedAt
            },
            null,
            reviewedByUserId));

        await _db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<DateTime?> ResolveRecipientCreatedAtAsync(
        Transfer transfer,
        CancellationToken ct)
    {
        if (transfer.RecipientId.HasValue)
        {
            return await _db.Recipients.AsNoTracking()
                .Where(x => x.Id == transfer.RecipientId.Value)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        if (transfer.BusinessBeneficiaryId.HasValue)
        {
            return await _db.BusinessBeneficiaries.AsNoTracking()
                .Where(x => x.Id == transfer.BusinessBeneficiaryId.Value)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        return null;
    }

    private static void ApplyDecision(
        Transfer transfer,
        int score,
        RiskLevel level,
        RiskDecision decision,
        string? holdReason)
    {
        transfer.RiskScore = score;
        transfer.RiskLevel = level;
        transfer.RiskDecision = decision;
        transfer.RiskAssessedAt = DateTime.UtcNow;
        transfer.IsComplianceHold = decision != RiskDecision.Allow;
        transfer.ComplianceHoldReason = transfer.IsComplianceHold ? holdReason : null;
        transfer.LastUpdatedAt = DateTime.UtcNow;
    }

    private static TransferRiskDecisionDto ToDecisionDto(
        Transfer transfer,
        IReadOnlyList<string> rules) =>
        new(
            transfer.Id,
            transfer.RiskScore,
            transfer.RiskLevel,
            transfer.RiskDecision,
            transfer.IsComplianceHold,
            rules,
            transfer.RiskAssessedAt ?? DateTime.UtcNow);

    private static AmlFlagDetailsDto ToDetailsDto(AmlFlag flag)
    {
        var dto = new AmlFlagDto(
            flag.Id,
            flag.CustomerProfileId,
            flag.TransferId,
            flag.Transfer?.Reference,
            flag.FlagType,
            flag.Severity,
            flag.RiskScore,
            flag.IsBlocking,
            flag.Description,
            flag.IsResolved,
            flag.ReviewDecision,
            flag.CreatedAt,
            flag.ReviewedAt,
            flag.ResolvedAt);

        return new AmlFlagDetailsDto(
            dto,
            flag.ResolutionNote,
            flag.ReviewedByUserId,
            flag.ResolvedByUserId,
            flag.Transfer?.RiskLevel.ToString(),
            flag.Transfer?.IsComplianceHold,
            flag.Transfer?.ComplianceHoldReason);
    }

    private sealed record RiskRuleHit(string Code, int Points, string Description);
}
