using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Compliance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Models.Transfers;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public sealed class ComplianceCaseService : IComplianceCaseService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public ComplianceCaseService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ComplianceCase> OpenScreeningCaseAsync(
        ScreeningRecord screening,
        Guid? initiatedByUserId,
        CancellationToken ct = default)
    {
        var descriptor = ResolveScreeningCaseDescriptor(screening);
        var existing = await FindOpenScreeningCaseAsync(screening, descriptor.CaseType, ct);

        if (existing is not null)
        {
            existing.ScreeningRecordId = screening.Id;
            existing.ScreeningRecord = screening;
            existing.Title = $"{descriptor.Label} review: {screening.SubjectName}";
            existing.Description = BuildScreeningDescription(screening);
            existing.Priority = (int)existing.Priority >= (int)descriptor.Priority
                ? existing.Priority
                : descriptor.Priority;
            existing.IsBlocking = existing.IsBlocking || screening.IsBlocking;
            existing.DueAt = existing.Priority == ComplianceCasePriority.Critical
                ? DateTime.UtcNow.AddHours(4)
                : existing.DueAt ?? DateTime.UtcNow.AddHours(24);
            existing.LastUpdatedAt = DateTime.UtcNow;
            existing.LastUpdatedByUserId = initiatedByUserId;

            if (screening.TransferId.HasValue)
            {
                var amlFlag = existing.AmlFlag ??
                    CreateScreeningFlag(screening, descriptor.Priority, initiatedByUserId);
                existing.AmlFlag = amlFlag;
                existing.AmlFlagId = amlFlag.Id;
                amlFlag.Severity = descriptor.Priority.ToString();
                amlFlag.RiskScore = Math.Max(
                    amlFlag.RiskScore,
                    (int)Math.Round(screening.HighestMatchScore, MidpointRounding.AwayFromZero));
                amlFlag.IsBlocking = amlFlag.IsBlocking || screening.IsBlocking;
                amlFlag.Description = $"{screening.SubjectName} produced a {screening.Status} watchlist screening result.";
                amlFlag.LastUpdatedAt = DateTime.UtcNow;
                amlFlag.LastUpdatedByUserId = initiatedByUserId;
                if (_db.Entry(amlFlag).State == EntityState.Detached)
                    _db.AmlFlags.Add(amlFlag);
            }

            _audit.Stage(new AuditRecordRequest(
                "COMPLIANCE_CASE_ALERT_DEDUPLICATED",
                "Compliance",
                nameof(ComplianceCase),
                existing.Id.ToString(),
                null,
                new
                {
                    existing.Reference,
                    existing.CaseType,
                    existing.Priority,
                    existing.IsBlocking,
                    existing.ScreeningRecordId,
                    existing.TransferId
                },
                null,
                initiatedByUserId));

            return existing;
        }

        AmlFlag? flag = screening.TransferId.HasValue
            ? CreateScreeningFlag(screening, descriptor.Priority, initiatedByUserId)
            : null;
        if (flag is not null)
            _db.AmlFlags.Add(flag);

        var entity = new ComplianceCase
        {
            Reference = await GenerateReferenceAsync(ct),
            CaseType = descriptor.CaseType,
            Priority = descriptor.Priority,
            Status = ComplianceCaseStatus.Open,
            Title = $"{descriptor.Label} review: {screening.SubjectName}",
            Description = BuildScreeningDescription(screening),
            IsBlocking = screening.IsBlocking,
            CustomerProfileId = screening.CustomerProfileId,
            BusinessProfileId = screening.BusinessProfileId,
            RecipientId = screening.RecipientId,
            BusinessBeneficiaryId = screening.BusinessBeneficiaryId,
            BusinessBeneficialOwnerId = screening.BusinessBeneficialOwnerId,
            TransferId = screening.TransferId,
            ScreeningRecordId = screening.Id,
            ScreeningRecord = screening,
            AmlFlagId = flag?.Id,
            AmlFlag = flag,
            OpenedAt = DateTime.UtcNow,
            DueAt = DateTime.UtcNow.AddHours(descriptor.Priority == ComplianceCasePriority.Critical ? 4 : 24),
            CreatedByUserId = initiatedByUserId
        };
        _db.ComplianceCases.Add(entity);

        _audit.Stage(new AuditRecordRequest(
            "COMPLIANCE_CASE_OPENED",
            "Compliance",
            nameof(ComplianceCase),
            entity.Id.ToString(),
            null,
            new
            {
                entity.Reference,
                entity.CaseType,
                entity.Priority,
                entity.IsBlocking,
                entity.ScreeningRecordId,
                entity.TransferId
            },
            null,
            initiatedByUserId));

        return entity;
    }

    public async Task<ComplianceCase> OpenTransactionMonitoringCaseAsync(
        Guid transferId,
        Guid? customerProfileId,
        Guid? businessProfileId,
        AmlFlag flag,
        ComplianceCasePriority priority,
        string title,
        string description,
        Guid? initiatedByUserId,
        CancellationToken ct = default)
    {
        var existing = await _db.ComplianceCases.FirstOrDefaultAsync(x =>
            x.TransferId == transferId &&
            x.CaseType == ComplianceCaseType.TransactionMonitoring &&
            x.Status != ComplianceCaseStatus.Resolved &&
            x.Status != ComplianceCaseStatus.Closed &&
            !x.IsDeleted,
            ct);
        if (existing is not null)
        {
            existing.Priority = (int)existing.Priority >= (int)priority ? existing.Priority : priority;
            existing.IsBlocking = existing.IsBlocking || flag.IsBlocking;
            existing.Description = description;
            existing.AmlFlagId ??= flag.Id;
            existing.LastUpdatedAt = DateTime.UtcNow;
            existing.LastUpdatedByUserId = initiatedByUserId;
            return existing;
        }

        var entity = new ComplianceCase
        {
            Reference = await GenerateReferenceAsync(ct),
            CaseType = ComplianceCaseType.TransactionMonitoring,
            Priority = priority,
            Status = ComplianceCaseStatus.Open,
            Title = title,
            Description = description,
            IsBlocking = flag.IsBlocking,
            CustomerProfileId = customerProfileId,
            BusinessProfileId = businessProfileId,
            TransferId = transferId,
            AmlFlagId = flag.Id,
            OpenedAt = DateTime.UtcNow,
            DueAt = DateTime.UtcNow.AddHours(priority == ComplianceCasePriority.Critical ? 4 : 24),
            CreatedByUserId = initiatedByUserId
        };
        _db.ComplianceCases.Add(entity);
        _audit.Stage(new AuditRecordRequest(
            "TRANSACTION_MONITORING_CASE_OPENED",
            "Compliance",
            nameof(ComplianceCase),
            entity.Id.ToString(),
            null,
            new
            {
                entity.Reference,
                entity.Priority,
                entity.IsBlocking,
                entity.TransferId,
                entity.AmlFlagId
            },
            null,
            initiatedByUserId));
        return entity;
    }

    public Task<PagedResult<ComplianceCaseDto>> GetCasesAsync(
        ComplianceCaseStatus? status,
        ComplianceCaseType? caseType,
        ComplianceCasePriority? priority,
        bool? isBlocking,
        Guid? assignedToUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ComplianceCases.AsNoTracking().Where(x => !x.IsDeleted);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (caseType.HasValue)
            query = query.Where(x => x.CaseType == caseType.Value);
        if (priority.HasValue)
            query = query.Where(x => x.Priority == priority.Value);
        if (isBlocking.HasValue)
            query = query.Where(x => x.IsBlocking == isBlocking.Value);
        if (assignedToUserId.HasValue)
            query = query.Where(x => x.AssignedToUserId == assignedToUserId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Reference.ToLower().Contains(term) ||
                x.Title.ToLower().Contains(term) ||
                x.Description.ToLower().Contains(term) ||
                (x.Transfer != null && x.Transfer.Reference.ToLower().Contains(term)));
        }

        return query
            .OrderBy(x => x.Status == ComplianceCaseStatus.Resolved || x.Status == ComplianceCaseStatus.Closed)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.OpenedAt)
            .Select(x => new ComplianceCaseDto(
                x.Id,
                x.Reference,
                x.CaseType,
                x.Status,
                x.Priority,
                x.Title,
                x.IsBlocking,
                x.CustomerProfileId,
                x.BusinessProfileId,
                x.RecipientId,
                x.BusinessBeneficiaryId,
                x.BusinessBeneficialOwnerId,
                x.TransferId,
                x.Transfer != null ? x.Transfer.Reference : null,
                x.ScreeningRecordId,
                x.AmlFlagId,
                x.AssignedToUserId,
                x.OpenedAt,
                x.DueAt,
                x.Decision,
                x.DecidedAt,
                x.ResolvedAt))
            .PaginateAsync(page, pageSize, ct);
    }

    public async Task<ComplianceCaseDetailsDto> GetCaseAsync(
        Guid caseId,
        CancellationToken ct = default)
    {
        var entity = await CaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == caseId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Compliance case not found.");
        return ToDetailsDto(entity);
    }

    public async Task<ComplianceCaseDetailsDto> AssignAsync(
        Guid caseId,
        Guid actionedByUserId,
        AssignComplianceCaseRequestDto request,
        CancellationToken ct = default)
    {
        var entity = await CaseQuery()
            .FirstOrDefaultAsync(x => x.Id == caseId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Compliance case not found.");
        EnsureCaseOpen(entity);

        if (request.AssignedToUserId.HasValue &&
            !await _db.Users.AsNoTracking().AnyAsync(x => x.Id == request.AssignedToUserId.Value, ct))
        {
            throw new InvalidOperationException("The selected compliance-case assignee does not exist.");
        }

        var oldAssigned = entity.AssignedToUserId;
        entity.AssignedToUserId = request.AssignedToUserId;
        entity.Status = request.AssignedToUserId.HasValue
            ? ComplianceCaseStatus.InReview
            : ComplianceCaseStatus.Open;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = actionedByUserId;

        var note = Clean(request.Note, 1000);
        if (note is not null)
        {
            entity.Notes.Add(new ComplianceCaseNote
            {
                Note = note,
                IsInternal = true,
                CreatedByUserId = actionedByUserId
            });
        }

        _audit.Stage(new AuditRecordRequest(
            "COMPLIANCE_CASE_ASSIGNED",
            "Compliance",
            nameof(ComplianceCase),
            entity.Id.ToString(),
            new { AssignedToUserId = oldAssigned },
            new { entity.AssignedToUserId, entity.Status },
            null,
            actionedByUserId));

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(entity);
    }

    public async Task<ComplianceCaseDetailsDto> AddNoteAsync(
        Guid caseId,
        Guid actionedByUserId,
        AddComplianceCaseNoteRequestDto request,
        CancellationToken ct = default)
    {
        var entity = await CaseQuery()
            .FirstOrDefaultAsync(x => x.Id == caseId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Compliance case not found.");
        EnsureCaseOpen(entity);
        var note = Clean(request.Note, 4000)
            ?? throw new InvalidOperationException("A case note is required.");

        entity.Notes.Add(new ComplianceCaseNote
        {
            Note = note,
            IsInternal = request.IsInternal,
            CreatedByUserId = actionedByUserId
        });
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = actionedByUserId;

        _audit.Stage(new AuditRecordRequest(
            "COMPLIANCE_CASE_NOTE_ADDED",
            "Compliance",
            nameof(ComplianceCase),
            entity.Id.ToString(),
            null,
            new { request.IsInternal },
            null,
            actionedByUserId));

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(entity);
    }

    public async Task<ComplianceCaseDetailsDto> AddEvidenceAsync(
        Guid caseId,
        Guid actionedByUserId,
        AddComplianceCaseEvidenceRequestDto request,
        CancellationToken ct = default)
    {
        var entity = await CaseQuery()
            .FirstOrDefaultAsync(x => x.Id == caseId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Compliance case not found.");
        EnsureCaseOpen(entity);

        var evidenceType = Clean(request.EvidenceType, 100)
            ?? throw new InvalidOperationException("An evidence type is required.");
        var title = Clean(request.Title, 300)
            ?? throw new InvalidOperationException("An evidence title is required.");

        if (!string.IsNullOrWhiteSpace(request.MetadataJson))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(request.MetadataJson);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new InvalidOperationException("Evidence metadata must be valid JSON.");
            }
        }

        entity.Evidence.Add(new ComplianceCaseEvidence
        {
            EvidenceType = evidenceType,
            Title = title,
            Description = Clean(request.Description, 4000),
            Source = Clean(request.Source, 200),
            ExternalReference = Clean(request.ExternalReference, 500),
            StorageKey = Clean(request.StorageKey, 1000),
            MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson)
                ? null
                : request.MetadataJson.Trim(),
            AddedByUserId = actionedByUserId
        });
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = actionedByUserId;

        _audit.Stage(new AuditRecordRequest(
            "COMPLIANCE_CASE_EVIDENCE_ADDED",
            "Compliance",
            nameof(ComplianceCase),
            entity.Id.ToString(),
            null,
            new
            {
                EvidenceType = evidenceType,
                Title = title,
                Source = Clean(request.Source, 200),
                ExternalReference = Clean(request.ExternalReference, 500),
                HasStorageKey = !string.IsNullOrWhiteSpace(request.StorageKey)
            },
            null,
            actionedByUserId));

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(entity);
    }

    public async Task<ComplianceCaseDetailsDto> DecideAsync(
        Guid caseId,
        Guid actionedByUserId,
        DecideComplianceCaseRequestDto request,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Decision))
            throw new InvalidOperationException("A valid compliance-case decision is required.");
        var reason = Clean(request.Reason, 4000)
            ?? throw new InvalidOperationException("A decision reason is required.");

        var entity = await CaseQuery()
            .FirstOrDefaultAsync(x => x.Id == caseId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Compliance case not found.");
        EnsureCaseOpen(entity);

        entity.Decision = request.Decision;
        entity.DecisionReason = reason;
        entity.DecidedAt = DateTime.UtcNow;
        entity.DecidedByUserId = actionedByUserId;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = actionedByUserId;

        if (request.Decision == ComplianceCaseDecision.Escalated)
        {
            entity.Status = ComplianceCaseStatus.Escalated;
            entity.Priority = (int)entity.Priority < (int)ComplianceCasePriority.High
                ? ComplianceCasePriority.High
                : entity.Priority;
            entity.IsBlocking = true;
        }
        else
        {
            entity.Status = ComplianceCaseStatus.Resolved;
            entity.ResolvedAt = DateTime.UtcNow;
            var releases = request.Decision is ComplianceCaseDecision.Cleared or
                ComplianceCaseDecision.FalsePositive or
                ComplianceCaseDecision.ClosedNoAction;
            if (releases)
            {
                entity.IsBlocking = false;
                if (entity.ScreeningRecord is not null)
                {
                    entity.ScreeningRecord.Status = ScreeningStatus.Clear;
                    entity.ScreeningRecord.IsBlocking = false;
                    entity.ScreeningRecord.LastUpdatedAt = DateTime.UtcNow;
                    entity.ScreeningRecord.LastUpdatedByUserId = actionedByUserId;

                    if (request.Decision == ComplianceCaseDecision.FalsePositive)
                    {
                        foreach (var match in entity.ScreeningRecord.Matches)
                            match.IsFalsePositive = true;
                    }
                }
                if (entity.AmlFlag is not null)
                {
                    entity.AmlFlag.IsResolved = true;
                    entity.AmlFlag.ResolvedAt = DateTime.UtcNow;
                    entity.AmlFlag.ResolvedByUserId = actionedByUserId;
                    entity.AmlFlag.ResolutionNote = reason;
                    entity.AmlFlag.ReviewDecision = request.Decision.ToString();
                    entity.AmlFlag.ReviewedAt = DateTime.UtcNow;
                    entity.AmlFlag.ReviewedByUserId = actionedByUserId;
                }
            }
        }

        entity.Notes.Add(new ComplianceCaseNote
        {
            Note = $"Decision: {request.Decision}. {reason}",
            IsInternal = true,
            CreatedByUserId = actionedByUserId
        });

        if (entity.Transfer is not null)
            await RefreshTransferHoldAsync(entity, actionedByUserId, ct);

        _audit.Stage(new AuditRecordRequest(
            "COMPLIANCE_CASE_DECIDED",
            "Compliance",
            nameof(ComplianceCase),
            entity.Id.ToString(),
            null,
            new
            {
                request.Decision,
                Reason = reason,
                entity.Status,
                entity.IsBlocking,
                entity.TransferId
            },
            null,
            actionedByUserId));

        await _db.SaveChangesAsync(ct);
        return ToDetailsDto(entity);
    }

    public async Task<ComplianceCaseSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var query = _db.ComplianceCases.AsNoTracking().Where(x => !x.IsDeleted);
        var last30Days = DateTime.UtcNow.AddDays(-30);
        return new ComplianceCaseSummaryDto(
            await query.CountAsync(x => x.Status == ComplianceCaseStatus.Open, ct),
            await query.CountAsync(x => x.Status == ComplianceCaseStatus.InReview, ct),
            await query.CountAsync(x => x.Status == ComplianceCaseStatus.Escalated, ct),
            await query.CountAsync(x => x.IsBlocking && x.Status != ComplianceCaseStatus.Closed, ct),
            await query.CountAsync(x => x.Priority == ComplianceCasePriority.Critical && x.Status != ComplianceCaseStatus.Closed, ct),
            await query.CountAsync(x => x.ResolvedAt.HasValue && x.ResolvedAt.Value >= last30Days, ct),
            await _db.ScreeningRecords.AsNoTracking().CountAsync(x =>
                !x.IsDeleted &&
                (x.Status == ScreeningStatus.PotentialMatch || x.Status == ScreeningStatus.ConfirmedMatch),
                ct));
    }

    public Task<bool> HasBlockingCaseAsync(
        Guid transferId,
        Guid? customerProfileId,
        Guid? businessProfileId,
        Guid? recipientId,
        Guid? businessBeneficiaryId,
        CancellationToken ct = default)
    {
        return _db.ComplianceCases.AsNoTracking().AnyAsync(x =>
            !x.IsDeleted &&
            x.IsBlocking &&
            x.Status != ComplianceCaseStatus.Closed &&
            (x.Status != ComplianceCaseStatus.Resolved ||
             x.Decision == ComplianceCaseDecision.ConfirmedMatch ||
             x.Decision == ComplianceCaseDecision.ReportFiled) &&
            (x.TransferId == transferId ||
             (customerProfileId.HasValue && x.CustomerProfileId == customerProfileId) ||
             (businessProfileId.HasValue && x.BusinessProfileId == businessProfileId) ||
             (recipientId.HasValue && x.RecipientId == recipientId) ||
             (businessBeneficiaryId.HasValue && x.BusinessBeneficiaryId == businessBeneficiaryId)),
            ct);
    }

    private IQueryable<ComplianceCase> CaseQuery() =>
        _db.ComplianceCases
            .Include(x => x.Transfer)
            .Include(x => x.ScreeningRecord).ThenInclude(x => x!.Matches)
            .Include(x => x.AmlFlag)
            .Include(x => x.Notes)
            .Include(x => x.Evidence);

    private async Task RefreshTransferHoldAsync(
        ComplianceCase currentCase,
        Guid actionedByUserId,
        CancellationToken ct)
    {
        var transfer = currentCase.Transfer
            ?? throw new InvalidOperationException("Compliance case transfer is unavailable.");
        var otherCaseBlock = await _db.ComplianceCases.AsNoTracking().AnyAsync(x =>
            x.Id != currentCase.Id &&
            !x.IsDeleted &&
            x.IsBlocking &&
            x.Status != ComplianceCaseStatus.Closed &&
            (x.Status != ComplianceCaseStatus.Resolved ||
             x.Decision == ComplianceCaseDecision.ConfirmedMatch ||
             x.Decision == ComplianceCaseDecision.ReportFiled) &&
            (x.TransferId == transfer.Id ||
             (transfer.CustomerProfileId.HasValue && x.CustomerProfileId == transfer.CustomerProfileId) ||
             (transfer.BusinessProfileId.HasValue && x.BusinessProfileId == transfer.BusinessProfileId) ||
             (transfer.RecipientId.HasValue && x.RecipientId == transfer.RecipientId) ||
             (transfer.BusinessBeneficiaryId.HasValue && x.BusinessBeneficiaryId == transfer.BusinessBeneficiaryId)),
            ct);
        var otherFlagBlock = await _db.AmlFlags.AsNoTracking().AnyAsync(x =>
            x.Id != currentCase.AmlFlagId &&
            x.TransferId == transfer.Id &&
            x.IsBlocking &&
            !x.IsResolved &&
            !x.IsDeleted,
            ct);
        if (currentCase.IsBlocking || otherCaseBlock || otherFlagBlock)
            return;

        transfer.IsComplianceHold = false;
        transfer.ComplianceHoldReason = null;
        transfer.ComplianceReviewedAt = DateTime.UtcNow;
        transfer.ComplianceReviewedByUserId = actionedByUserId;
        transfer.LastUpdatedAt = DateTime.UtcNow;
        transfer.LastUpdatedByUserId = actionedByUserId;
        _db.TransferTimelineEvents.Add(new TransferTimelineEvent
        {
            TransferId = transfer.Id,
            EventType = "COMPLIANCE_CASE_CLEARED",
            Title = "Compliance review completed",
            Description = "All blocking compliance cases for this transfer have been cleared.",
            OccurredAt = DateTime.UtcNow
        });
    }

    private async Task<string> GenerateReferenceAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var reference = $"CC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..31].ToUpperInvariant();
            if (!await _db.ComplianceCases.AsNoTracking().AnyAsync(x => x.Reference == reference, ct))
                return reference;
        }
        throw new InvalidOperationException("Unable to generate a unique compliance-case reference.");
    }

    private async Task<ComplianceCase?> FindOpenScreeningCaseAsync(
        ScreeningRecord screening,
        ComplianceCaseType caseType,
        CancellationToken ct)
    {
        var query = _db.ComplianceCases
            .Include(x => x.AmlFlag)
            .Where(x =>
                !x.IsDeleted &&
                x.CaseType == caseType &&
                x.Status != ComplianceCaseStatus.Resolved &&
                x.Status != ComplianceCaseStatus.Closed);

        if (screening.TransferId.HasValue)
            query = query.Where(x => x.TransferId == screening.TransferId.Value);
        else
            query = query.Where(x => x.TransferId == null);

        query = screening.SubjectType switch
        {
            ScreeningSubjectType.Customer => query.Where(x => x.CustomerProfileId == screening.CustomerProfileId),
            ScreeningSubjectType.Business => query.Where(x => x.BusinessProfileId == screening.BusinessProfileId),
            ScreeningSubjectType.Recipient => query.Where(x => x.RecipientId == screening.RecipientId),
            ScreeningSubjectType.BusinessBeneficiary => query.Where(x => x.BusinessBeneficiaryId == screening.BusinessBeneficiaryId),
            ScreeningSubjectType.BusinessBeneficialOwner => query.Where(x => x.BusinessBeneficialOwnerId == screening.BusinessBeneficialOwnerId),
            _ => query.Where(x => false)
        };

        return await query.OrderByDescending(x => x.OpenedAt).FirstOrDefaultAsync(ct);
    }

    private static AmlFlag CreateScreeningFlag(
        ScreeningRecord screening,
        ComplianceCasePriority priority,
        Guid? initiatedByUserId) => new()
    {
        CustomerProfileId = screening.CustomerProfileId,
        TransferId = screening.TransferId,
        FlagType = "WATCHLIST_SCREENING",
        Severity = priority.ToString(),
        Description = $"{screening.SubjectName} produced a {screening.Status} watchlist screening result.",
        RiskScore = (int)Math.Round(screening.HighestMatchScore, MidpointRounding.AwayFromZero),
        IsBlocking = screening.IsBlocking,
        CreatedByUserId = initiatedByUserId
    };

    private static ScreeningCaseDescriptor ResolveScreeningCaseDescriptor(ScreeningRecord screening)
    {
        if (screening.Status == ScreeningStatus.Failed)
        {
            return new ScreeningCaseDescriptor(
                ComplianceCaseType.ManualReview,
                screening.IsBlocking ? ComplianceCasePriority.Critical : ComplianceCasePriority.High,
                "Screening provider failure");
        }

        var primaryType = screening.Matches
            .OrderByDescending(x => x.MatchScore)
            .Select(x => (WatchlistType?)x.WatchlistType)
            .FirstOrDefault();
        var caseType = primaryType switch
        {
            WatchlistType.Pep => ComplianceCaseType.PepScreening,
            WatchlistType.AdverseMedia => ComplianceCaseType.AdverseMediaScreening,
            _ => ComplianceCaseType.SanctionsScreening
        };
        var priority = screening.IsBlocking
            ? ComplianceCasePriority.Critical
            : primaryType == WatchlistType.Pep
                ? ComplianceCasePriority.High
                : screening.HighestMatchScore >= 95m
                    ? ComplianceCasePriority.Critical
                    : screening.HighestMatchScore >= 85m
                        ? ComplianceCasePriority.High
                        : ComplianceCasePriority.Medium;
        return new ScreeningCaseDescriptor(
            caseType,
            priority,
            primaryType?.ToString() ?? "Watchlist");
    }

    private static string BuildScreeningDescription(ScreeningRecord screening)
    {
        var matches = screening.Matches
            .OrderByDescending(x => x.MatchScore)
            .Take(5)
            .Select(x => $"{x.WatchlistType}: {x.MatchedName} ({x.MatchScore:0.##}%)")
            .ToList();
        var details = matches.Count == 0
            ? "No provider match details were returned."
            : string.Join("; ", matches);
        if (!string.IsNullOrWhiteSpace(screening.ErrorMessage))
            details += $" Provider error: {screening.ErrorMessage}";
        return $"Screening provider {screening.ProviderCode} returned {screening.Status}. {details}";
    }

    private sealed record ScreeningCaseDescriptor(
        ComplianceCaseType CaseType,
        ComplianceCasePriority Priority,
        string Label);

    private static ComplianceCaseDto ToDto(ComplianceCase x) => new(
        x.Id,
        x.Reference,
        x.CaseType,
        x.Status,
        x.Priority,
        x.Title,
        x.IsBlocking,
        x.CustomerProfileId,
        x.BusinessProfileId,
        x.RecipientId,
        x.BusinessBeneficiaryId,
        x.BusinessBeneficialOwnerId,
        x.TransferId,
        x.Transfer != null ? x.Transfer.Reference : null,
        x.ScreeningRecordId,
        x.AmlFlagId,
        x.AssignedToUserId,
        x.OpenedAt,
        x.DueAt,
        x.Decision,
        x.DecidedAt,
        x.ResolvedAt);

    private static ComplianceCaseDetailsDto ToDetailsDto(ComplianceCase x) => new(
        ToDto(x),
        x.Description,
        x.DecisionReason,
        x.DecidedByUserId,
        x.ScreeningRecord is null ? null : ComplianceScreeningService.ToDto(x.ScreeningRecord),
        x.AmlFlag is null ? null : ToAmlFlagDto(x.AmlFlag, x.Transfer),
        x.Notes.OrderByDescending(n => n.CreatedAt)
            .Select(n => new ComplianceCaseNoteDto(n.Id, n.Note, n.IsInternal, n.CreatedByUserId, n.CreatedAt))
            .ToList(),
        x.Evidence.OrderByDescending(e => e.CreatedAt)
            .Select(e => new ComplianceCaseEvidenceDto(
                e.Id,
                e.EvidenceType,
                e.Title,
                e.Description,
                e.Source,
                e.ExternalReference,
                e.StorageKey,
                e.MetadataJson,
                e.AddedByUserId,
                e.CreatedAt))
            .ToList());

    private static AmlFlagDto ToAmlFlagDto(AmlFlag x, Transfer? transfer) => new(
        x.Id,
        x.CustomerProfileId,
        x.TransferId,
        transfer?.Reference,
        x.FlagType,
        x.Severity,
        x.RiskScore,
        x.IsBlocking,
        x.Description,
        x.IsResolved,
        x.ReviewDecision,
        x.CreatedAt,
        x.ReviewedAt,
        x.ResolvedAt);

    private static void EnsureCaseOpen(ComplianceCase entity)
    {
        if (entity.Status is ComplianceCaseStatus.Resolved or ComplianceCaseStatus.Closed)
            throw new InvalidOperationException("The compliance case is already resolved or closed.");
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var cleaned = value.Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
