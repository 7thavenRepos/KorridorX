using System.IO.Compression;
using System.Text;
using System.Text.Json;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Compliance;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Compliance;
using KorridorX.Models.Enums;
using KorridorX.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Compliance;

public sealed class RegulatoryReportingService : IRegulatoryReportingService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public RegulatoryReportingService(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<RegulatoryReportDto> CreateAsync(
        Guid complianceCaseId,
        Guid userId,
        CreateRegulatoryReportRequestDto request,
        CancellationToken ct = default)
    {
        ValidateRequest(request);
        var complianceCase = await _db.ComplianceCases
            .FirstOrDefaultAsync(x => x.Id == complianceCaseId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Compliance case not found.");

        var duplicate = await _db.RegulatoryReports.AsNoTracking().AnyAsync(x =>
            x.ComplianceCaseId == complianceCaseId &&
            x.ReportType == request.ReportType &&
            x.Status != RegulatoryReportStatus.Withdrawn &&
            !x.IsDeleted,
            ct);
        if (duplicate)
            throw new InvalidOperationException("An active regulatory report of this type already exists for the compliance case.");

        var entity = new RegulatoryReport
        {
            Reference = await GenerateReferenceAsync(ct),
            ReportType = request.ReportType,
            Status = RegulatoryReportStatus.Draft,
            ComplianceCaseId = complianceCase.Id,
            JurisdictionCode = NormalizeCode(request.JurisdictionCode),
            RegulatoryAuthority = Clean(request.RegulatoryAuthority, 200),
            Narrative = CleanRequired(request.Narrative, 12000, "Narrative"),
            SuspicionReason = CleanRequired(request.SuspicionReason, 4000, "Suspicion reason"),
            ActivityStartedAt = request.ActivityStartedAt,
            ActivityEndedAt = request.ActivityEndedAt,
            TotalAmount = request.TotalAmount,
            CurrencyCode = NormalizeNullableCode(request.CurrencyCode),
            PreparedByUserId = userId,
            FilingDueAt = request.FilingDueAt,
            CreatedByUserId = userId
        };
        _db.RegulatoryReports.Add(entity);

        complianceCase.Notes.Add(new ComplianceCaseNote
        {
            Note = $"Regulatory report {entity.Reference} was created in draft status.",
            IsInternal = true,
            CreatedByUserId = userId
        });
        complianceCase.LastUpdatedAt = DateTime.UtcNow;
        complianceCase.LastUpdatedByUserId = userId;

        _audit.Stage(new AuditRecordRequest(
            "REGULATORY_REPORT_CREATED",
            "Compliance",
            nameof(RegulatoryReport),
            entity.Id.ToString(),
            null,
            new
            {
                entity.Reference,
                entity.ReportType,
                entity.Status,
                entity.ComplianceCaseId,
                entity.JurisdictionCode,
                entity.FilingDueAt
            },
            null,
            userId));

        await _db.SaveChangesAsync(ct);
        entity.ComplianceCase = complianceCase;
        return ToDto(entity);
    }

    public async Task<RegulatoryReportDto> UpdateAsync(
        Guid reportId,
        Guid userId,
        UpdateRegulatoryReportRequestDto request,
        CancellationToken ct = default)
    {
        ValidateRequest(request);
        var entity = await ReportQuery()
            .FirstOrDefaultAsync(x => x.Id == reportId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Regulatory report not found.");
        EnsureEditable(entity);

        var old = new
        {
            entity.ReportType,
            entity.JurisdictionCode,
            entity.RegulatoryAuthority,
            entity.ActivityStartedAt,
            entity.ActivityEndedAt,
            entity.TotalAmount,
            entity.CurrencyCode,
            entity.FilingDueAt,
            entity.Version
        };

        entity.ReportType = request.ReportType;
        entity.JurisdictionCode = NormalizeCode(request.JurisdictionCode);
        entity.RegulatoryAuthority = Clean(request.RegulatoryAuthority, 200);
        entity.Narrative = CleanRequired(request.Narrative, 12000, "Narrative");
        entity.SuspicionReason = CleanRequired(request.SuspicionReason, 4000, "Suspicion reason");
        entity.ActivityStartedAt = request.ActivityStartedAt;
        entity.ActivityEndedAt = request.ActivityEndedAt;
        entity.TotalAmount = request.TotalAmount;
        entity.CurrencyCode = NormalizeNullableCode(request.CurrencyCode);
        entity.FilingDueAt = request.FilingDueAt;
        entity.Status = RegulatoryReportStatus.Draft;
        entity.RejectionReason = null;
        entity.Version++;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;

        _audit.Stage(new AuditRecordRequest(
            "REGULATORY_REPORT_UPDATED",
            "Compliance",
            nameof(RegulatoryReport),
            entity.Id.ToString(),
            old,
            new
            {
                entity.ReportType,
                entity.JurisdictionCode,
                entity.RegulatoryAuthority,
                entity.ActivityStartedAt,
                entity.ActivityEndedAt,
                entity.TotalAmount,
                entity.CurrencyCode,
                entity.FilingDueAt,
                entity.Version
            },
            null,
            userId));

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<RegulatoryReportDto> SubmitForApprovalAsync(
        Guid reportId,
        Guid userId,
        CancellationToken ct = default)
    {
        var entity = await ReportQuery()
            .FirstOrDefaultAsync(x => x.Id == reportId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Regulatory report not found.");
        EnsureEditable(entity);
        ValidateReportCompleteness(entity);

        entity.Status = RegulatoryReportStatus.PendingApproval;
        entity.SubmittedForApprovalAt = DateTime.UtcNow;
        entity.RejectionReason = null;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        entity.ComplianceCase.Status = ComplianceCaseStatus.Escalated;
        entity.ComplianceCase.IsBlocking = true;
        entity.ComplianceCase.LastUpdatedAt = DateTime.UtcNow;
        entity.ComplianceCase.LastUpdatedByUserId = userId;
        entity.ComplianceCase.Notes.Add(new ComplianceCaseNote
        {
            Note = $"Regulatory report {entity.Reference} was submitted for approval.",
            IsInternal = true,
            CreatedByUserId = userId
        });

        _audit.Stage(new AuditRecordRequest(
            "REGULATORY_REPORT_SUBMITTED",
            "Compliance",
            nameof(RegulatoryReport),
            entity.Id.ToString(),
            null,
            new { entity.Status, entity.SubmittedForApprovalAt, entity.Version },
            null,
            userId));

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<RegulatoryReportDto> ReviewAsync(
        Guid reportId,
        Guid userId,
        ReviewRegulatoryReportRequestDto request,
        CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Decision))
            throw new InvalidOperationException("A valid regulatory report review decision is required.");
        var reason = CleanRequired(request.Reason, 4000, "Review reason");
        var entity = await ReportQuery()
            .FirstOrDefaultAsync(x => x.Id == reportId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Regulatory report not found.");
        if (entity.Status != RegulatoryReportStatus.PendingApproval)
            throw new InvalidOperationException("Only reports pending approval can be reviewed.");
        if (entity.PreparedByUserId == userId)
            throw new InvalidOperationException("The report preparer cannot approve or reject the same report.");

        if (request.Decision == RegulatoryReportApprovalDecision.Approve)
        {
            entity.Status = RegulatoryReportStatus.Approved;
            entity.ApprovedByUserId = userId;
            entity.ApprovedAt = DateTime.UtcNow;
            entity.RejectionReason = null;
        }
        else
        {
            entity.Status = RegulatoryReportStatus.Rejected;
            entity.ApprovedByUserId = null;
            entity.ApprovedAt = null;
            entity.RejectionReason = reason;
        }

        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        entity.ComplianceCase.Notes.Add(new ComplianceCaseNote
        {
            Note = $"Regulatory report {entity.Reference} review decision: {request.Decision}. {reason}",
            IsInternal = true,
            CreatedByUserId = userId
        });

        _audit.Stage(new AuditRecordRequest(
            "REGULATORY_REPORT_REVIEWED",
            "Compliance",
            nameof(RegulatoryReport),
            entity.Id.ToString(),
            null,
            new
            {
                request.Decision,
                Reason = reason,
                entity.Status,
                entity.ApprovedAt,
                entity.ApprovedByUserId
            },
            null,
            userId));

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<RegulatoryReportDto> MarkFiledAsync(
        Guid reportId,
        Guid userId,
        MarkRegulatoryReportFiledRequestDto request,
        CancellationToken ct = default)
    {
        var filingReference = CleanRequired(request.FilingReference, 200, "Filing reference");
        var entity = await ReportQuery()
            .FirstOrDefaultAsync(x => x.Id == reportId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Regulatory report not found.");
        if (entity.Status != RegulatoryReportStatus.Approved)
            throw new InvalidOperationException("Only an approved regulatory report can be marked as filed.");

        entity.Status = RegulatoryReportStatus.Filed;
        entity.FilingReference = filingReference;
        entity.FiledAt = request.FiledAt?.ToUniversalTime() ?? DateTime.UtcNow;
        entity.FiledByUserId = userId;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        entity.ComplianceCase.Decision = ComplianceCaseDecision.ReportFiled;
        entity.ComplianceCase.DecisionReason = $"Regulatory report {entity.Reference} was filed with reference {filingReference}.";
        entity.ComplianceCase.DecidedAt = DateTime.UtcNow;
        entity.ComplianceCase.DecidedByUserId = userId;
        entity.ComplianceCase.Status = ComplianceCaseStatus.Resolved;
        entity.ComplianceCase.ResolvedAt = DateTime.UtcNow;
        entity.ComplianceCase.IsBlocking = true;
        entity.ComplianceCase.LastUpdatedAt = DateTime.UtcNow;
        entity.ComplianceCase.LastUpdatedByUserId = userId;
        entity.ComplianceCase.Notes.Add(new ComplianceCaseNote
        {
            Note = $"Regulatory report {entity.Reference} was filed with reference {filingReference}.",
            IsInternal = true,
            CreatedByUserId = userId
        });

        _audit.Stage(new AuditRecordRequest(
            "REGULATORY_REPORT_FILED",
            "Compliance",
            nameof(RegulatoryReport),
            entity.Id.ToString(),
            null,
            new
            {
                entity.Status,
                entity.FilingReference,
                entity.FiledAt,
                entity.FiledByUserId,
                entity.ComplianceCaseId
            },
            null,
            userId));

        await _db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<RegulatoryReportDto> GetAsync(Guid reportId, CancellationToken ct = default)
    {
        var entity = await ReportQuery().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == reportId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Regulatory report not found.");
        return ToDto(entity);
    }

    public async Task<PagedResult<RegulatoryReportDto>> GetPagedAsync(
        RegulatoryReportStatus? status,
        RegulatoryReportType? reportType,
        string? jurisdictionCode,
        Guid? complianceCaseId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.RegulatoryReports.AsNoTracking()
            .Include(x => x.ComplianceCase)
            .Where(x => !x.IsDeleted);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (reportType.HasValue)
            query = query.Where(x => x.ReportType == reportType.Value);
        if (!string.IsNullOrWhiteSpace(jurisdictionCode))
        {
            var normalized = NormalizeCode(jurisdictionCode);
            query = query.Where(x => x.JurisdictionCode == normalized);
        }
        if (complianceCaseId.HasValue)
            query = query.Where(x => x.ComplianceCaseId == complianceCaseId.Value);

        var paged = await query
            .OrderBy(x => x.Status == RegulatoryReportStatus.Filed)
            .ThenBy(x => x.FilingDueAt)
            .ThenByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<RegulatoryReportDto>
        {
            Items = paged.Items.Select(ToDto).ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<RegulatoryReportSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var last30Days = now.AddDays(-30);
        var query = _db.RegulatoryReports.AsNoTracking().Where(x => !x.IsDeleted);
        return new RegulatoryReportSummaryDto(
            await query.CountAsync(x => x.Status == RegulatoryReportStatus.Draft, ct),
            await query.CountAsync(x => x.Status == RegulatoryReportStatus.PendingApproval, ct),
            await query.CountAsync(x => x.Status == RegulatoryReportStatus.Approved, ct),
            await query.CountAsync(x => x.Status == RegulatoryReportStatus.Filed, ct),
            await query.CountAsync(x => x.Status == RegulatoryReportStatus.Rejected, ct),
            await query.CountAsync(x =>
                x.FilingDueAt.HasValue &&
                x.FilingDueAt.Value < now &&
                x.Status != RegulatoryReportStatus.Filed &&
                x.Status != RegulatoryReportStatus.Withdrawn,
                ct),
            await query.CountAsync(x => x.FiledAt.HasValue && x.FiledAt.Value >= last30Days, ct));
    }

    public async Task<RegulatoryReportExportResult> ExportPackageAsync(
        Guid reportId,
        Guid userId,
        CancellationToken ct = default)
    {
        var entity = await _db.RegulatoryReports
            .Include(x => x.ComplianceCase).ThenInclude(x => x.Notes)
            .Include(x => x.ComplianceCase).ThenInclude(x => x.Evidence)
            .Include(x => x.ComplianceCase).ThenInclude(x => x.ScreeningRecord).ThenInclude(x => x!.Matches)
            .Include(x => x.ComplianceCase).ThenInclude(x => x.AmlFlag)
            .Include(x => x.ComplianceCase).ThenInclude(x => x.Transfer)
            .FirstOrDefaultAsync(x => x.Id == reportId && !x.IsDeleted, ct)
            ?? throw new InvalidOperationException("Regulatory report not found.");

        var auditLogs = await _db.AuditLogs.AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                ((x.EntityName == nameof(RegulatoryReport) && x.EntityId == entity.Id.ToString()) ||
                 (x.EntityName == nameof(ComplianceCase) && x.EntityId == entity.ComplianceCaseId.ToString())))
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(ct);

        var package = new
        {
            generatedAt = DateTime.UtcNow,
            report = ToDto(entity),
            complianceCase = new
            {
                entity.ComplianceCase.Id,
                entity.ComplianceCase.Reference,
                entity.ComplianceCase.CaseType,
                entity.ComplianceCase.Status,
                entity.ComplianceCase.Priority,
                entity.ComplianceCase.Title,
                entity.ComplianceCase.Description,
                entity.ComplianceCase.IsBlocking,
                entity.ComplianceCase.Decision,
                entity.ComplianceCase.DecisionReason,
                entity.ComplianceCase.OpenedAt,
                entity.ComplianceCase.ResolvedAt
            },
            transfer = entity.ComplianceCase.Transfer is null ? null : new
            {
                entity.ComplianceCase.Transfer.Id,
                entity.ComplianceCase.Transfer.Reference,
                entity.ComplianceCase.Transfer.Status,
                entity.ComplianceCase.Transfer.SourceCountryCode,
                entity.ComplianceCase.Transfer.DestinationCountryCode,
                entity.ComplianceCase.Transfer.SourceCurrencyCode,
                entity.ComplianceCase.Transfer.DestinationCurrencyCode,
                entity.ComplianceCase.Transfer.SourceAmount,
                entity.ComplianceCase.Transfer.DestinationAmount,
                entity.ComplianceCase.Transfer.CreatedAt,
                entity.ComplianceCase.Transfer.CompletedAt
            },
            screening = entity.ComplianceCase.ScreeningRecord,
            amlFlag = entity.ComplianceCase.AmlFlag,
            notes = entity.ComplianceCase.Notes.OrderBy(x => x.CreatedAt),
            evidence = entity.ComplianceCase.Evidence.OrderBy(x => x.CreatedAt),
            auditLogs
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        };
        await using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "report-package.json", JsonSerializer.Serialize(package, jsonOptions));
            WriteEntry(archive, "narrative.txt", entity.Narrative);
            WriteEntry(archive, "suspicion-reason.txt", entity.SuspicionReason);
            WriteEntry(
                archive,
                "evidence-manifest.json",
                JsonSerializer.Serialize(entity.ComplianceCase.Evidence.OrderBy(x => x.CreatedAt), jsonOptions));
            WriteEntry(
                archive,
                "audit-manifest.json",
                JsonSerializer.Serialize(auditLogs, jsonOptions));
        }

        entity.LastExportedAt = DateTime.UtcNow;
        entity.LastUpdatedAt = DateTime.UtcNow;
        entity.LastUpdatedByUserId = userId;
        _audit.Stage(new AuditRecordRequest(
            "REGULATORY_REPORT_EXPORTED",
            "Compliance",
            nameof(RegulatoryReport),
            entity.Id.ToString(),
            null,
            new { entity.Reference, entity.Status, entity.LastExportedAt },
            null,
            userId));
        await _db.SaveChangesAsync(ct);

        return new RegulatoryReportExportResult(
            memory.ToArray(),
            $"{entity.Reference}-regulatory-package.zip",
            "application/zip");
    }

    private IQueryable<RegulatoryReport> ReportQuery() =>
        _db.RegulatoryReports.Include(x => x.ComplianceCase);

    private async Task<string> GenerateReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var value = $"REG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30].ToUpperInvariant();
            if (!await _db.RegulatoryReports.AsNoTracking().AnyAsync(x => x.Reference == value, ct))
                return value;
        }
        throw new InvalidOperationException("Unable to generate a unique regulatory report reference.");
    }

    private static void ValidateRequest(CreateRegulatoryReportRequestDto request)
    {
        if (!Enum.IsDefined(request.ReportType))
            throw new InvalidOperationException("A valid regulatory report type is required.");
        if (request.ActivityStartedAt.HasValue && request.ActivityEndedAt.HasValue &&
            request.ActivityEndedAt.Value < request.ActivityStartedAt.Value)
        {
            throw new InvalidOperationException("Activity end date cannot be before activity start date.");
        }
        if (request.TotalAmount.HasValue && request.TotalAmount.Value < 0)
            throw new InvalidOperationException("Total amount cannot be negative.");
        if (request.TotalAmount.HasValue && string.IsNullOrWhiteSpace(request.CurrencyCode))
            throw new InvalidOperationException("Currency code is required when total amount is supplied.");
    }

    private static void ValidateReportCompleteness(RegulatoryReport entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Narrative) || string.IsNullOrWhiteSpace(entity.SuspicionReason))
            throw new InvalidOperationException("Narrative and suspicion reason are required before submission.");
        if (string.IsNullOrWhiteSpace(entity.JurisdictionCode))
            throw new InvalidOperationException("Jurisdiction code is required before submission.");
    }

    private static void EnsureEditable(RegulatoryReport entity)
    {
        if (entity.Status is not RegulatoryReportStatus.Draft and not RegulatoryReportStatus.Rejected)
            throw new InvalidOperationException("Only draft or rejected regulatory reports can be edited.");
    }

    private static RegulatoryReportDto ToDto(RegulatoryReport x) => new()
    {
        Id = x.Id,
        Reference = x.Reference,
        ReportType = x.ReportType,
        Status = x.Status,
        ComplianceCaseId = x.ComplianceCaseId,
        ComplianceCaseReference = x.ComplianceCase?.Reference ?? "",
        JurisdictionCode = x.JurisdictionCode,
        RegulatoryAuthority = x.RegulatoryAuthority,
        FilingReference = x.FilingReference,
        Narrative = x.Narrative,
        SuspicionReason = x.SuspicionReason,
        ActivityStartedAt = x.ActivityStartedAt,
        ActivityEndedAt = x.ActivityEndedAt,
        TotalAmount = x.TotalAmount,
        CurrencyCode = x.CurrencyCode,
        PreparedByUserId = x.PreparedByUserId,
        SubmittedForApprovalAt = x.SubmittedForApprovalAt,
        ApprovedByUserId = x.ApprovedByUserId,
        ApprovedAt = x.ApprovedAt,
        RejectionReason = x.RejectionReason,
        FiledByUserId = x.FiledByUserId,
        FiledAt = x.FiledAt,
        FilingDueAt = x.FilingDueAt,
        LastExportedAt = x.LastExportedAt,
        Version = x.Version,
        CreatedAt = x.CreatedAt,
        LastUpdatedAt = x.LastUpdatedAt
    };

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string NormalizeCode(string value) =>
        CleanRequired(value, 10, "Code").ToUpperInvariant();

    private static string? NormalizeNullableCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

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
}
