using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Compliance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Audit;
using KorridorX.Models.Common;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Models.Notifications;
using KorridorX.Models.Providers;
using KorridorX.Models.Webhooks;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public sealed class DataRetentionService : IDataRetentionService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly DataRetentionOptions _options;

    public DataRetentionService(
        AppDbContext db,
        IAuditService audit,
        Microsoft.Extensions.Options.IOptions<DataRetentionOptions> options)
    {
        _db = db;
        _audit = audit;
        _options = options.Value;
    }

    public Task<PagedResult<RetentionPolicyDto>> GetPoliciesAsync(
        bool? isActive,
        RetentionRecordType? recordType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.DataRetentionPolicies.AsNoTracking().Where(x => !x.IsDeleted);
        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);
        if (recordType.HasValue)
            query = query.Where(x => x.RecordType == recordType.Value);

        return query
            .OrderBy(x => x.RecordType)
            .ThenBy(x => x.Name)
            .Select(x => new RetentionPolicyDto(
                x.Id,
                x.Name,
                x.RecordType,
                x.RetentionDays,
                x.Action,
                x.IsActive,
                x.Description,
                x.CreatedAt,
                x.LastUpdatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<RetentionPolicyDto> CreatePolicyAsync(
        Guid userId,
        UpsertRetentionPolicyRequestDto request,
        CancellationToken ct = default)
    {
        ValidatePolicyRequest(request);
        var duplicate = await _db.DataRetentionPolicies.AsNoTracking().AnyAsync(x =>
            !x.IsDeleted &&
            x.RecordType == request.RecordType &&
            x.IsActive,
            ct);
        if (duplicate && request.IsActive)
            throw new InvalidOperationException("An active retention policy already exists for this record type.");

        var entity = new DataRetentionPolicy
        {
            Name = CleanRequired(request.Name, 200, "Policy name"),
            RecordType = request.RecordType,
            RetentionDays = request.RetentionDays,
            Action = request.Action,
            IsActive = request.IsActive,
            Description = Clean(request.Description, 2000),
            CreatedByUserId = userId
        };
        _db.DataRetentionPolicies.Add(entity);
        _audit.Stage(new AuditRecordRequest(
            "DATA_RETENTION_POLICY_CREATED",
            "Compliance",
            nameof(DataRetentionPolicy),
            entity.Id.ToString(),
            null,
            new { entity.Name, entity.RecordType, entity.RetentionDays, entity.Action, entity.IsActive },
            null,
            userId));
        await _db.SaveChangesAsync(ct);
        return ToPolicyDto(entity);
    }

    public async Task<RetentionPolicyDto> UpdatePolicyAsync(
        Guid policyId,
        Guid userId,
        UpsertRetentionPolicyRequestDto request,
        CancellationToken ct = default)
    {
        ValidatePolicyRequest(request);
        var entity = await _db.DataRetentionPolicies
            .FirstOrDefaultAsync(x => x.Id == policyId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Data retention policy not found.");
        var duplicate = await _db.DataRetentionPolicies.AsNoTracking().AnyAsync(x =>
            x.Id != policyId &&
            !x.IsDeleted &&
            x.RecordType == request.RecordType &&
            x.IsActive,
            ct);
        if (duplicate && request.IsActive)
            throw new InvalidOperationException("Another active retention policy already exists for this record type.");

        var old = new { entity.Name, entity.RecordType, entity.RetentionDays, entity.Action, entity.IsActive };
        entity.Name = CleanRequired(request.Name, 200, "Policy name");
        entity.RecordType = request.RecordType;
        entity.RetentionDays = request.RetentionDays;
        entity.Action = request.Action;
        entity.IsActive = request.IsActive;
        entity.Description = Clean(request.Description, 2000);
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        _audit.Stage(new AuditRecordRequest(
            "DATA_RETENTION_POLICY_UPDATED",
            "Compliance",
            nameof(DataRetentionPolicy),
            entity.Id.ToString(),
            old,
            new { entity.Name, entity.RecordType, entity.RetentionDays, entity.Action, entity.IsActive },
            null,
            userId));
        await _db.SaveChangesAsync(ct);
        return ToPolicyDto(entity);
    }

    public Task<PagedResult<LegalHoldDto>> GetLegalHoldsAsync(
        LegalHoldStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.LegalHolds.AsNoTracking().Where(x => !x.IsDeleted);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        return query
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.EffectiveAt)
            .Select(x => new LegalHoldDto(
                x.Id,
                x.Reference,
                x.Name,
                x.Reason,
                x.Status,
                x.AppliesToAllComplianceData,
                x.ComplianceCaseId,
                x.RegulatoryReportId,
                x.EntityName,
                x.EntityId,
                x.EffectiveAt,
                x.ReleasedAt,
                x.ReleasedByUserId,
                x.ReleaseReason,
                x.CreatedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<LegalHoldDto> CreateLegalHoldAsync(
        Guid userId,
        CreateLegalHoldRequestDto request,
        CancellationToken ct = default)
    {
        ValidateLegalHoldRequest(request);
        if (request.ComplianceCaseId.HasValue &&
            !await _db.ComplianceCases.AsNoTracking().AnyAsync(x =>
                x.Id == request.ComplianceCaseId.Value && !x.IsDeleted, ct))
        {
            throw new InvalidOperationException("Compliance case not found for legal hold.");
        }
        if (request.RegulatoryReportId.HasValue &&
            !await _db.RegulatoryReports.AsNoTracking().AnyAsync(x =>
                x.Id == request.RegulatoryReportId.Value && !x.IsDeleted, ct))
        {
            throw new InvalidOperationException("Regulatory report not found for legal hold.");
        }

        var entity = new LegalHold
        {
            Reference = await GenerateLegalHoldReferenceAsync(ct),
            Name = CleanRequired(request.Name, 200, "Legal hold name"),
            Reason = CleanRequired(request.Reason, 4000, "Legal hold reason"),
            Status = LegalHoldStatus.Active,
            AppliesToAllComplianceData = request.AppliesToAllComplianceData,
            ComplianceCaseId = request.ComplianceCaseId,
            RegulatoryReportId = request.RegulatoryReportId,
            EntityName = Clean(request.EntityName, 150),
            EntityId = Clean(request.EntityId, 100),
            EffectiveAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _db.LegalHolds.Add(entity);
        _audit.Stage(new AuditRecordRequest(
            "LEGAL_HOLD_CREATED",
            "Compliance",
            nameof(LegalHold),
            entity.Id.ToString(),
            null,
            new
            {
                entity.Reference,
                entity.Name,
                entity.AppliesToAllComplianceData,
                entity.ComplianceCaseId,
                entity.RegulatoryReportId,
                entity.EntityName,
                entity.EntityId
            },
            null,
            userId));
        await _db.SaveChangesAsync(ct);
        return ToLegalHoldDto(entity);
    }

    public async Task<LegalHoldDto> ReleaseLegalHoldAsync(
        Guid legalHoldId,
        Guid userId,
        ReleaseLegalHoldRequestDto request,
        CancellationToken ct = default)
    {
        var reason = CleanRequired(request.Reason, 4000, "Release reason");
        var entity = await _db.LegalHolds
            .FirstOrDefaultAsync(x => x.Id == legalHoldId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Legal hold not found.");
        if (entity.Status == LegalHoldStatus.Released)
            return ToLegalHoldDto(entity);

        entity.Status = LegalHoldStatus.Released;
        entity.ReleasedAt = DateTime.UtcNow;
        entity.ReleasedByUserId = userId;
        entity.ReleaseReason = reason;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        _audit.Stage(new AuditRecordRequest(
            "LEGAL_HOLD_RELEASED",
            "Compliance",
            nameof(LegalHold),
            entity.Id.ToString(),
            null,
            new { entity.Reference, entity.ReleasedAt, entity.ReleasedByUserId, entity.ReleaseReason },
            null,
            userId));
        await _db.SaveChangesAsync(ct);
        return ToLegalHoldDto(entity);
    }

    public Task<RetentionExecutionDto> ExecutePolicyAsync(
        Guid policyId,
        bool dryRun,
        Guid? userId,
        CancellationToken ct = default) =>
        ExecutePolicyInternalAsync(
            policyId,
            dryRun,
            userId,
            Math.Clamp(_options.BatchSize, 1, 5000),
            ct);

    private async Task<RetentionExecutionDto> ExecutePolicyInternalAsync(
        Guid policyId,
        bool dryRun,
        Guid? userId,
        int candidateLimit,
        CancellationToken ct)
    {
        var policy = await _db.DataRetentionPolicies
            .FirstOrDefaultAsync(x => x.Id == policyId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Data retention policy not found.");
        if (!policy.IsActive)
            throw new InvalidOperationException("Only active retention policies can be executed.");

        var execution = new RetentionExecutionLog
        {
            DataRetentionPolicyId = policy.Id,
            IsDryRun = dryRun || policy.Action == RetentionAction.ReviewOnly,
            StartedAt = DateTime.UtcNow,
            ExecutedByUserId = userId
        };
        _db.RetentionExecutionLogs.Add(execution);

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-policy.RetentionDays);
            var candidates = await LoadCandidatesAsync(policy.RecordType, cutoff, candidateLimit, ct);
            execution.CandidateCount = candidates.Count;

            foreach (var candidate in candidates)
            {
                var protectedByHold = await HasActiveLegalHoldAsync(candidate, ct);
                var canDelete = RetentionEligibilityPolicy.CanSoftDelete(
                    policy.RecordType,
                    candidate.IsTerminal,
                    protectedByHold);
                if (protectedByHold)
                {
                    execution.SkippedLegalHoldCount++;
                    continue;
                }
                if (!canDelete || execution.IsDryRun)
                    continue;

                await SoftDeleteAsync(policy.RecordType, candidate.Id, userId, ct);
                execution.ProcessedCount++;
            }

            execution.CompletedAt = DateTime.UtcNow;
            _audit.Stage(new AuditRecordRequest(
                "DATA_RETENTION_POLICY_EXECUTED",
                "Compliance",
                nameof(DataRetentionPolicy),
                policy.Id.ToString(),
                null,
                new
                {
                    policy.Name,
                    policy.RecordType,
                    policy.RetentionDays,
                    policy.Action,
                    execution.IsDryRun,
                    execution.CandidateCount,
                    execution.ProcessedCount,
                    execution.SkippedLegalHoldCount
                },
                null,
                userId));
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            execution.ErrorMessage = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
            execution.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        return ToExecutionDto(execution, policy.Name);
    }

    public async Task<int> ExecuteActivePoliciesAsync(
        int batchSize,
        CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 5000);
        var policyIds = await _db.DataRetentionPolicies.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.RecordType)
            .Select(x => x.Id)
            .ToListAsync(ct);
        var executed = 0;
        foreach (var policyId in policyIds)
        {
            await ExecutePolicyInternalAsync(policyId, false, null, batchSize, ct);
            executed++;
        }
        return executed;
    }

    private async Task<List<RetentionCandidate>> LoadCandidatesAsync(
        RetentionRecordType recordType,
        DateTime cutoff,
        int limit,
        CancellationToken ct)
    {
        return recordType switch
        {
            RetentionRecordType.NotificationMessage => await _db.NotificationMessages.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RetentionCandidate(x.Id, nameof(NotificationMessage), null, null,
                    x.Status == "Sent" || x.Status == "DeadLetter"))
                .ToListAsync(ct),
            RetentionRecordType.ProviderRequestLog => await _db.ProviderRequestLogs.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RetentionCandidate(x.Id, nameof(ProviderRequestLog), null, null,
                    x.Status != ProviderRequestStatus.Pending))
                .ToListAsync(ct),
            RetentionRecordType.WebhookEvent => await _db.WebhookEvents.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RetentionCandidate(x.Id, nameof(WebhookEvent), null, null,
                    x.ProcessingStatus != WebhookProcessingStatus.Pending &&
                    x.ProcessingStatus != WebhookProcessingStatus.Processing))
                .ToListAsync(ct),
            RetentionRecordType.AuditLog => await _db.AuditLogs.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RetentionCandidate(x.Id, nameof(AuditLog), null, null, true))
                .ToListAsync(ct),
            RetentionRecordType.ScreeningRecord => await _db.ScreeningRecords.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RetentionCandidate(
                    x.Id,
                    nameof(ScreeningRecord),
                    _db.ComplianceCases.Where(c => c.ScreeningRecordId == x.Id && !c.IsDeleted)
                        .Select(c => (Guid?)c.Id).FirstOrDefault(),
                    null,
                    x.Status != ScreeningStatus.ManualReview))
                .ToListAsync(ct),
            RetentionRecordType.ComplianceCase => await _db.ComplianceCases.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RetentionCandidate(
                    x.Id,
                    nameof(ComplianceCase),
                    x.Id,
                    null,
                    x.Status == ComplianceCaseStatus.Resolved || x.Status == ComplianceCaseStatus.Closed))
                .ToListAsync(ct),
            RetentionRecordType.RegulatoryReport => await _db.RegulatoryReports.AsNoTracking()
                .Where(x => !x.IsDeleted && x.CreatedAt < cutoff)
                .OrderBy(x => x.CreatedAt)
                .Take(limit)
                .Select(x => new RetentionCandidate(
                    x.Id,
                    nameof(RegulatoryReport),
                    x.ComplianceCaseId,
                    x.Id,
                    x.Status == RegulatoryReportStatus.Filed ||
                    x.Status == RegulatoryReportStatus.Withdrawn))
                .ToListAsync(ct),
            _ => throw new InvalidOperationException("Unsupported retention record type.")
        };
    }

    private async Task<bool> HasActiveLegalHoldAsync(
        RetentionCandidate candidate,
        CancellationToken ct)
    {
        return await _db.LegalHolds.AsNoTracking().AnyAsync(x =>
            !x.IsDeleted &&
            x.Status == LegalHoldStatus.Active &&
            (x.AppliesToAllComplianceData ||
             (x.ComplianceCaseId.HasValue && candidate.ComplianceCaseId == x.ComplianceCaseId) ||
             (x.RegulatoryReportId.HasValue && candidate.RegulatoryReportId == x.RegulatoryReportId) ||
             (x.EntityName == candidate.EntityName && x.EntityId == candidate.Id.ToString())),
            ct);
    }

    private async Task SoftDeleteAsync(
        RetentionRecordType recordType,
        Guid id,
        Guid? userId,
        CancellationToken ct)
    {
        BaseEntity? entity = recordType switch
        {
            RetentionRecordType.NotificationMessage => await _db.NotificationMessages.FirstOrDefaultAsync(x => x.Id == id, ct),
            RetentionRecordType.ProviderRequestLog => await _db.ProviderRequestLogs.FirstOrDefaultAsync(x => x.Id == id, ct),
            RetentionRecordType.WebhookEvent => await _db.WebhookEvents.FirstOrDefaultAsync(x => x.Id == id, ct),
            RetentionRecordType.AuditLog => await _db.AuditLogs.FirstOrDefaultAsync(x => x.Id == id, ct),
            RetentionRecordType.ScreeningRecord => await _db.ScreeningRecords.FirstOrDefaultAsync(x => x.Id == id, ct),
            RetentionRecordType.ComplianceCase => await _db.ComplianceCases.FirstOrDefaultAsync(x => x.Id == id, ct),
            RetentionRecordType.RegulatoryReport => await _db.RegulatoryReports.FirstOrDefaultAsync(x => x.Id == id, ct),
            _ => null
        };
        if (entity is null || entity.IsDeleted)
            return;
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.LastUpdatedAt = DateTime.UtcNow;
        if (entity is AuditableEntity auditable)
        {
            auditable.DeletedByUserId = userId;
            auditable.LastUpdatedByUserId = userId;
        }
    }

    private async Task<string> GenerateLegalHoldReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var value = $"LH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant();
            if (!await _db.LegalHolds.AsNoTracking().AnyAsync(x => x.Reference == value, ct))
                return value;
        }
        throw new InvalidOperationException("Unable to generate a unique legal hold reference.");
    }

    private static void ValidatePolicyRequest(UpsertRetentionPolicyRequestDto request)
    {
        if (!Enum.IsDefined(request.RecordType))
            throw new InvalidOperationException("A valid retention record type is required.");
        if (!Enum.IsDefined(request.Action))
            throw new InvalidOperationException("A valid retention action is required.");
        if (request.RetentionDays is < 30 or > 36500)
            throw new InvalidOperationException("Retention days must be between 30 and 36500.");
    }

    private static void ValidateLegalHoldRequest(CreateLegalHoldRequestDto request)
    {
        var targets = (request.AppliesToAllComplianceData ? 1 : 0) +
            (request.ComplianceCaseId.HasValue ? 1 : 0) +
            (request.RegulatoryReportId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(request.EntityName) || !string.IsNullOrWhiteSpace(request.EntityId) ? 1 : 0);
        if (targets != 1)
            throw new InvalidOperationException("A legal hold must target exactly one scope.");
        if ((!string.IsNullOrWhiteSpace(request.EntityName) && string.IsNullOrWhiteSpace(request.EntityId)) ||
            (string.IsNullOrWhiteSpace(request.EntityName) && !string.IsNullOrWhiteSpace(request.EntityId)))
        {
            throw new InvalidOperationException("Entity name and entity ID must be supplied together.");
        }
    }

    private static RetentionPolicyDto ToPolicyDto(DataRetentionPolicy x) => new(
        x.Id,
        x.Name,
        x.RecordType,
        x.RetentionDays,
        x.Action,
        x.IsActive,
        x.Description,
        x.CreatedAt,
        x.LastUpdatedAt);

    private static LegalHoldDto ToLegalHoldDto(LegalHold x) => new(
        x.Id,
        x.Reference,
        x.Name,
        x.Reason,
        x.Status,
        x.AppliesToAllComplianceData,
        x.ComplianceCaseId,
        x.RegulatoryReportId,
        x.EntityName,
        x.EntityId,
        x.EffectiveAt,
        x.ReleasedAt,
        x.ReleasedByUserId,
        x.ReleaseReason,
        x.CreatedAt);

    private static RetentionExecutionDto ToExecutionDto(RetentionExecutionLog x, string policyName) => new(
        x.Id,
        x.DataRetentionPolicyId,
        policyName,
        x.IsDryRun,
        x.StartedAt,
        x.CompletedAt,
        x.CandidateCount,
        x.ProcessedCount,
        x.SkippedLegalHoldCount,
        x.ErrorMessage);

    private static string CleanRequired(string? value, int maxLength, string label)
    {
        var cleaned = Clean(value, maxLength);
        return cleaned ?? throw new InvalidOperationException($"{label} is required.");
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }

    private sealed record RetentionCandidate(
        Guid Id,
        string EntityName,
        Guid? ComplianceCaseId,
        Guid? RegulatoryReportId,
        bool IsTerminal);
}
