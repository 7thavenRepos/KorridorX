using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Dtos.Audit;
using KorridorX.Dtos.Support;
using KorridorX.Extensions;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;
using KorridorX.Models.Support;
using KorridorX.Models.Transfers;
using KorridorX.Services.Audit;
using KorridorX.Services.Notifications;
using KorridorX.Services.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Support;

public sealed class AdminSupportService : IAdminSupportService
{
    private readonly AppDbContext _db;
    private readonly IReferenceGenerator _references;
    private readonly INotificationQueueService _notifications;
    private readonly IAuditService _audit;
    private readonly SupportOptions _options;

    public AdminSupportService(AppDbContext db, IReferenceGenerator references, INotificationQueueService notifications, IAuditService audit, IOptions<SupportOptions> options)
    {
        _db = db;
        _references = references;
        _notifications = notifications;
        _audit = audit;
        _options = options.Value;
    }

    public async Task<PagedResult<SupportTicketListItemDto>> GetTicketsAsync(SupportTicketStatus? status, SupportTicketPriority? priority, Guid? assignedToUserId, bool? unassigned, bool? slaBreached, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.SupportTickets.AsNoTracking().Include(x => x.Transfer).Where(x => !x.IsDeleted);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (priority.HasValue) query = query.Where(x => x.Priority == priority.Value);
        if (assignedToUserId.HasValue)
            query = query.Where(x => x.AssignedToUserId == assignedToUserId.Value);

        if (unassigned == true)
            query = query.Where(x => x.AssignedToUserId == null);

        if (slaBreached.HasValue)
            query = query.Where(x => x.IsSlaBreached == slaBreached.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(x => x.Reference.ToLower().Contains(value) || x.Subject.ToLower().Contains(value) || (x.Transfer != null && x.Transfer.Reference.ToLower().Contains(value)));
        }
        var paged = await query.OrderByDescending(x => x.Priority).ThenBy(x => x.ResolutionDueAt).PaginateAsync(page, pageSize, ct);
        return new PagedResult<SupportTicketListItemDto> { Items = paged.Items.Select(SupportService.ToListDtoForAdmin).ToList(), Meta = paged.Meta };
    }

    public async Task<SupportTicketDetailsDto> GetTicketAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await TicketQuery().FirstOrDefaultAsync(x => x.Id == ticketId, ct) ?? throw new InvalidOperationException("Support ticket not found.");
        return SupportService.ToDetailsDto(ticket, true);
    }

    public async Task<SupportTicketDetailsDto> ReplyAsync(Guid agentUserId, Guid ticketId, AdminAddSupportMessageRequestDto request, CancellationToken ct = default)
    {
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == ticketId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Support ticket not found.");
        if (ticket.Status == SupportTicketStatus.Closed) throw new InvalidOperationException("A closed support ticket cannot receive new messages.");
        var now = DateTime.UtcNow;
        _db.SupportTicketMessages.Add(new SupportTicketMessage { SupportTicketId = ticket.Id, AuthorUserId = agentUserId, AuthorType = SupportMessageAuthorType.Agent, Body = request.Body.Trim(), IsInternal = request.IsInternal });
        ticket.AssignedToUserId ??= agentUserId;
        ticket.LastAgentMessageAt = now;
        ticket.LastUpdatedAt = now;
        ticket.LastUpdatedByUserId = agentUserId;
        if (!request.IsInternal)
        {
            ticket.FirstRespondedAt ??= now;
            ticket.Status = SupportTicketStatus.AwaitingCustomer;
            await _notifications.QueueUserAsync(ticket.UserId, $"Update on support ticket {ticket.Reference}", request.Body.Trim(), nameof(SupportTicket), ticket.Id, ct);
        }
        _audit.Stage(new AuditRecordRequest("SupportTicketAgentReply", "Support", nameof(SupportTicket), ticket.Id.ToString(), Metadata: new { request.IsInternal }, UserId: agentUserId));
        await _db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticket.Id, ct);
    }

    public async Task<SupportTicketDetailsDto> AssignTicketAsync(
        Guid agentUserId,
        Guid ticketId,
        AssignSupportTicketRequestDto request,
        CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to change support ticket assignment.");

        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(
                x => x.Id == ticketId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Support ticket not found.");

        if (request.AssignedToUserId.HasValue)
        {
            var assigneeExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.AssignedToUserId.Value,
                    ct);

            if (!assigneeExists)
                throw new InvalidOperationException("The selected support assignee was not found.");
        }

        var oldAssignedToUserId = ticket.AssignedToUserId;

        ticket.AssignedToUserId = request.AssignedToUserId;
        ticket.LastUpdatedAt = DateTime.UtcNow;
        ticket.LastUpdatedByUserId = agentUserId;

        _audit.Stage(new AuditRecordRequest(
            "SupportTicketAssignmentChanged",
            "Support",
            nameof(SupportTicket),
            ticket.Id.ToString(),
            new
            {
                AssignedToUserId = oldAssignedToUserId
            },
            new
            {
                ticket.AssignedToUserId
            },
            new
            {
                Reason = reason
            },
            agentUserId));

        await _db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticket.Id, ct);
    }
    public async Task<SupportTicketDetailsDto> UpdateTicketAsync(
        Guid agentUserId,
        Guid ticketId,
        UpdateSupportTicketRequestDto request,
        CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to update a support ticket.");

        var ticket = await _db.SupportTickets
            .FirstOrDefaultAsync(
                x => x.Id == ticketId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException("Support ticket not found.");

        var old = new
        {
            ticket.Status,
            ticket.Priority,
            ticket.AssignedToUserId
        };

        var now = DateTime.UtcNow;

        if (request.Priority.HasValue &&
            request.Priority.Value != ticket.Priority)
        {
            ticket.Priority = request.Priority.Value;

            var target = GetSlaTarget(ticket.Priority);

            ticket.FirstResponseDueAt = ticket.FirstRespondedAt.HasValue
                ? ticket.FirstResponseDueAt
                : ticket.CreatedAt.AddMinutes(
                    target.FirstResponseMinutes);

            ticket.ResolutionDueAt = ticket.CreatedAt.AddMinutes(
                target.ResolutionMinutes);
        }

        if (request.AssignedToUserId.HasValue)
        {
            var assigneeExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.AssignedToUserId.Value,
                    ct);

            if (!assigneeExists)
                throw new InvalidOperationException("The selected support assignee was not found.");

            ticket.AssignedToUserId = request.AssignedToUserId;
        }

        if (request.Status.HasValue)
        {
            ticket.Status = request.Status.Value;

            if (ticket.Status == SupportTicketStatus.Resolved)
            {
                ticket.ResolvedAt ??= now;
            }
            else if (ticket.Status == SupportTicketStatus.Closed)
            {
                ticket.ResolvedAt ??= now;
                ticket.ClosedAt ??= now;
            }
            else
            {
                ticket.ResolvedAt = null;
                ticket.ClosedAt = null;
            }
        }

        ticket.LastUpdatedAt = now;
        ticket.LastUpdatedByUserId = agentUserId;

        _audit.Stage(new AuditRecordRequest(
            "SupportTicketUpdated",
            "Support",
            nameof(SupportTicket),
            ticket.Id.ToString(),
            old,
            new
            {
                ticket.Status,
                ticket.Priority,
                ticket.AssignedToUserId
            },
            new
            {
                Reason = reason
            },
            agentUserId));

        if (request.Status.HasValue)
        {
            await _notifications.QueueUserAsync(
                ticket.UserId,
                $"Support ticket {ticket.Reference} status updated",
                $"Your support ticket is now {ticket.Status}.",
                nameof(SupportTicket),
                ticket.Id,
                ct);
        }

        await _db.SaveChangesAsync(ct);
        return await GetTicketAsync(ticket.Id, ct);
    }
    public async Task<PagedResult<TransferDisputeDto>> GetDisputesAsync(
        TransferDisputeStatus? status,
        Guid? transferId,
        Guid? assignedToUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.TransferDisputes
            .AsNoTracking()
            .Include(x => x.Transfer)
            .Where(x => !x.IsDeleted);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (transferId.HasValue)
            query = query.Where(x => x.TransferId == transferId.Value);

        if (assignedToUserId.HasValue)
            query = query.Where(x => x.AssignedToUserId == assignedToUserId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.Reference.ToLower().Contains(value) ||
                x.Transfer.Reference.ToLower().Contains(value) ||
                x.Reason.ToLower().Contains(value));
        }

        var paged = await query
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<TransferDisputeDto>
        {
            Items = paged.Items
                .Select(SupportService.ToDisputeDto)
                .ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<TransferDisputeDetailsDto> GetDisputeAsync(
        Guid disputeId,
        CancellationToken ct = default)
    {
        var dispute = await _db.TransferDisputes
            .AsNoTracking()
            .Include(x => x.Transfer)
            .Include(x => x.Evidence)
            .Include(x => x.Investigations)
                .ThenInclude(x => x.Transfer)
            .FirstOrDefaultAsync(
                x => x.Id == disputeId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Transfer dispute not found.");

        return new TransferDisputeDetailsDto(
            SupportService.ToDisputeDto(dispute),
            dispute.Evidence
                .OrderByDescending(x => x.CreatedAt)
                .Select(SupportService.ToEvidenceDto)
                .ToList(),
            dispute.Investigations
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .Select(SupportService.ToInvestigationDto)
                .ToList());
    }
    public async Task<TransferDisputeDto> ResolveDisputeAsync(Guid agentUserId, Guid disputeId, ResolveTransferDisputeRequestDto request, CancellationToken ct = default)
    {
        if (request.Status is not TransferDisputeStatus.Resolved and not TransferDisputeStatus.Rejected)
            throw new InvalidOperationException("A dispute can only be resolved or rejected through this action.");
        var dispute = await _db.TransferDisputes.Include(x => x.Transfer).FirstOrDefaultAsync(x => x.Id == disputeId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Transfer dispute not found.");
        dispute.Status = request.Status;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.ResolutionNote = request.ResolutionNote.Trim();
        dispute.AssignedToUserId ??= agentUserId;
        dispute.LastUpdatedAt = DateTime.UtcNow;
        dispute.LastUpdatedByUserId = agentUserId;
        await ReleaseOperationalHoldIfClearAsync(dispute.Transfer, dispute.Id, null, ct);
        AddTimeline(dispute.Transfer, "TRANSFER_DISPUTE_RESOLVED", "Transfer dispute reviewed", $"Dispute {dispute.Reference} was {request.Status.ToString().ToLowerInvariant()}.");
        _audit.Stage(new AuditRecordRequest("TransferDisputeResolved", "Support", nameof(TransferDispute), dispute.Id.ToString(), NewValues: new { dispute.Status, dispute.ResolutionNote }, UserId: agentUserId));
        await _notifications.QueueUserAsync(dispute.UserId, $"Transfer dispute {dispute.Reference} updated", $"Your transfer dispute has been {request.Status.ToString().ToLowerInvariant()}.", nameof(TransferDispute), dispute.Id, ct);
        await _db.SaveChangesAsync(ct);
        return SupportService.ToDisputeDto(dispute);
    }

    public async Task<TransferInvestigationDto> CreateInvestigationAsync(Guid agentUserId, Guid transferId, CreateTransferInvestigationRequestDto request, CancellationToken ct = default)
    {
        var transfer = await _db.Transfers.FirstOrDefaultAsync(x => x.Id == transferId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Transfer not found.");
        if (request.TransferDisputeId.HasValue && !await _db.TransferDisputes.AnyAsync(x => x.Id == request.TransferDisputeId.Value && x.TransferId == transferId && !x.IsDeleted, ct)) throw new InvalidOperationException("Transfer dispute not found for this transfer.");
        if (request.SupportTicketId.HasValue && !await _db.SupportTickets.AnyAsync(x => x.Id == request.SupportTicketId.Value && x.TransferId == transferId && !x.IsDeleted, ct)) throw new InvalidOperationException("Support ticket not found for this transfer.");
        var existing = await _db.TransferInvestigations.AsNoTracking().Include(x => x.Transfer).FirstOrDefaultAsync(x => x.TransferId == transferId && !x.IsDeleted && x.Status != TransferInvestigationStatus.Resolved && x.Status != TransferInvestigationStatus.Closed, ct);
        if (existing is not null) return SupportService.ToInvestigationDto(existing);
        var investigation = new TransferInvestigation
        {
            Reference = await GenerateUniqueReferenceAsync(ct),
            TransferId = transferId,
            TransferDisputeId = request.TransferDisputeId,
            SupportTicketId = request.SupportTicketId,
            Summary = request.Summary.Trim(),
            AssignedToUserId = request.AssignedToUserId ?? agentUserId,
            DueAt = DateTime.UtcNow.AddHours(request.DueHours.HasValue ? Math.Clamp(request.DueHours.Value, 1, 720) : _options.InvestigationDueHours),
            CreatedByUserId = agentUserId
        };
        _db.TransferInvestigations.Add(investigation);
        if (request.TransferDisputeId.HasValue)
        {
            var dispute = await _db.TransferDisputes.FirstAsync(x => x.Id == request.TransferDisputeId.Value, ct);
            dispute.Status = TransferDisputeStatus.Investigating;
            dispute.AssignedToUserId ??= investigation.AssignedToUserId;
            dispute.LastUpdatedAt = DateTime.UtcNow;
        }
        ApplyOperationalHold(transfer, $"Active transfer investigation {investigation.Reference}.");
        AddTimeline(transfer, "TRANSFER_INVESTIGATION_OPENED", "Transfer investigation opened", $"Investigation {investigation.Reference} was opened.");
        _audit.Stage(new AuditRecordRequest("TransferInvestigationCreated", "Support", nameof(TransferInvestigation), investigation.Id.ToString(), Metadata: new { transferId, investigation.Reference }, UserId: agentUserId));
        await _db.SaveChangesAsync(ct);
        investigation.Transfer = transfer;
        return SupportService.ToInvestigationDto(investigation);
    }

    public async Task<PagedResult<TransferInvestigationDto>> GetInvestigationsAsync(
        TransferInvestigationStatus? status,
        Guid? assignedToUserId,
        Guid? transferId,
        bool? overdue,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var query = _db.TransferInvestigations
            .AsNoTracking()
            .Include(x => x.Transfer)
            .Where(x => !x.IsDeleted);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        if (assignedToUserId.HasValue)
            query = query.Where(x => x.AssignedToUserId == assignedToUserId.Value);

        if (transferId.HasValue)
            query = query.Where(x => x.TransferId == transferId.Value);

        if (overdue.HasValue)
        {
            query = overdue.Value
                ? query.Where(x =>
                    x.DueAt < now &&
                    x.Status != TransferInvestigationStatus.Resolved &&
                    x.Status != TransferInvestigationStatus.Closed)
                : query.Where(x =>
                    x.DueAt >= now ||
                    x.Status == TransferInvestigationStatus.Resolved ||
                    x.Status == TransferInvestigationStatus.Closed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLowerInvariant();

            query = query.Where(x =>
                x.Reference.ToLower().Contains(value) ||
                x.Transfer.Reference.ToLower().Contains(value) ||
                x.Summary.ToLower().Contains(value) ||
                (x.ProviderCaseReference != null &&
                 x.ProviderCaseReference.ToLower().Contains(value)));
        }

        var paged = await query
            .OrderBy(x => x.DueAt)
            .PaginateAsync(page, pageSize, ct);

        return new PagedResult<TransferInvestigationDto>
        {
            Items = paged.Items
                .Select(SupportService.ToInvestigationDto)
                .ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<TransferInvestigationDetailsDto> GetInvestigationAsync(
        Guid investigationId,
        CancellationToken ct = default)
    {
        var investigation = await _db.TransferInvestigations
            .AsNoTracking()
            .Include(x => x.Transfer)
            .Include(x => x.Evidence)
            .FirstOrDefaultAsync(
                x => x.Id == investigationId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Transfer investigation not found.");

        return new TransferInvestigationDetailsDto(
            SupportService.ToInvestigationDto(investigation),
            investigation.Evidence
                .OrderByDescending(x => x.CreatedAt)
                .Select(SupportService.ToEvidenceDto)
                .ToList());
    }
    public async Task<TransferInvestigationDto> UpdateInvestigationAsync(
        Guid agentUserId,
        Guid investigationId,
        UpdateTransferInvestigationRequestDto request,
        CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();

        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException(
                "A reason is required to update a transfer investigation.");

        var investigation = await _db.TransferInvestigations
            .Include(x => x.Transfer)
            .FirstOrDefaultAsync(
                x => x.Id == investigationId && !x.IsDeleted,
                ct)
            ?? throw new InvalidOperationException(
                "Transfer investigation not found.");

        var old = new
        {
            investigation.Status,
            investigation.Outcome,
            investigation.AssignedToUserId,
            investigation.Findings,
            investigation.ProviderCaseReference
        };

        var targetStatus = request.Status ?? investigation.Status;
        var targetOutcome = request.Outcome ?? investigation.Outcome;
        var targetFindings = request.Findings is null
            ? investigation.Findings
            : request.Findings.Trim();

        if (targetStatus is TransferInvestigationStatus.Resolved or
            TransferInvestigationStatus.Closed)
        {
            if (targetOutcome == TransferInvestigationOutcome.None)
                throw new InvalidOperationException(
                    "A completed investigation requires an outcome.");

            if (string.IsNullOrWhiteSpace(targetFindings))
                throw new InvalidOperationException(
                    "A completed investigation requires findings.");
        }

        if (request.AssignedToUserId.HasValue)
        {
            var assigneeExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.AssignedToUserId.Value,
                    ct);

            if (!assigneeExists)
                throw new InvalidOperationException(
                    "The selected investigation assignee was not found.");

            investigation.AssignedToUserId = request.AssignedToUserId;
        }

        if (request.Findings is not null)
            investigation.Findings = request.Findings.Trim();

        if (request.ProviderCaseReference is not null)
            investigation.ProviderCaseReference =
                request.ProviderCaseReference.Trim();

        if (request.Outcome.HasValue)
            investigation.Outcome = request.Outcome.Value;

        if (request.Status.HasValue)
        {
            investigation.Status = request.Status.Value;

            if (investigation.Status is
                TransferInvestigationStatus.Resolved or
                TransferInvestigationStatus.Closed)
            {
                investigation.ResolvedAt ??= DateTime.UtcNow;
            }
            else
            {
                investigation.ResolvedAt = null;
            }
        }

        investigation.LastUpdatedAt = DateTime.UtcNow;
        investigation.LastUpdatedByUserId = agentUserId;

        if (investigation.Status is
            TransferInvestigationStatus.Resolved or
            TransferInvestigationStatus.Closed)
        {
            await ReleaseOperationalHoldIfClearAsync(
                investigation.Transfer,
                null,
                investigation.Id,
                ct);

            AddTimeline(
                investigation.Transfer,
                "TRANSFER_INVESTIGATION_RESOLVED",
                "Transfer investigation completed",
                $"Investigation {investigation.Reference} was completed with outcome {investigation.Outcome}.");
        }

        _audit.Stage(new AuditRecordRequest(
            "TransferInvestigationUpdated",
            "Support",
            nameof(TransferInvestigation),
            investigation.Id.ToString(),
            old,
            new
            {
                investigation.Status,
                investigation.Outcome,
                investigation.AssignedToUserId,
                investigation.Findings,
                investigation.ProviderCaseReference
            },
            new
            {
                Reason = reason
            },
            agentUserId));

        await _db.SaveChangesAsync(ct);
        return SupportService.ToInvestigationDto(investigation);
    }
    public async Task<SupportEvidenceDto> AddInvestigationEvidenceAsync(Guid agentUserId, Guid investigationId, AddSupportEvidenceRequestDto request, CancellationToken ct = default)
    {
        var investigation = await _db.TransferInvestigations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == investigationId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("Transfer investigation not found.");
        var evidence = new SupportEvidence { TransferInvestigationId = investigation.Id, SupportTicketId = investigation.SupportTicketId, TransferDisputeId = investigation.TransferDisputeId, SubmittedByUserId = agentUserId, Name = request.Name.Trim(), Description = request.Description?.Trim(), MimeType = request.MimeType?.Trim(), StorageKey = request.StorageKey.Trim(), StorageUrl = request.StorageUrl?.Trim() };
        _db.SupportEvidence.Add(evidence);
        _audit.Stage(new AuditRecordRequest("InvestigationEvidenceAdded", "Support", nameof(SupportEvidence), evidence.Id.ToString(), Metadata: new { investigationId }, UserId: agentUserId));
        await _db.SaveChangesAsync(ct);
        return SupportService.ToEvidenceDto(evidence);
    }

    public async Task<SupportOperationsSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var terminal = new[] { SupportTicketStatus.Resolved, SupportTicketStatus.Closed };
        var totalResolved = await _db.SupportTickets.CountAsync(x => !x.IsDeleted && (x.Status == SupportTicketStatus.Resolved || x.Status == SupportTicketStatus.Closed), ct);
        var responseMet = await _db.SupportTickets.CountAsync(x => !x.IsDeleted && x.FirstRespondedAt.HasValue && x.FirstRespondedAt.Value <= x.FirstResponseDueAt, ct);
        var responseTotal = await _db.SupportTickets.CountAsync(x => !x.IsDeleted && x.FirstRespondedAt.HasValue, ct);
        var resolutionMet = await _db.SupportTickets.CountAsync(x => !x.IsDeleted && x.ResolvedAt.HasValue && x.ResolvedAt.Value <= x.ResolutionDueAt, ct);
        return new SupportOperationsSummaryDto(
            await _db.SupportTickets.CountAsync(x => !x.IsDeleted && !terminal.Contains(x.Status), ct),
            await _db.SupportTickets.CountAsync(x => !x.IsDeleted && !terminal.Contains(x.Status) && x.AssignedToUserId == null, ct),
            await _db.SupportTickets.CountAsync(x => !x.IsDeleted && !terminal.Contains(x.Status) && x.Priority == SupportTicketPriority.Urgent, ct),
            await _db.SupportTickets.CountAsync(x => !x.IsDeleted && !terminal.Contains(x.Status) && x.IsSlaBreached, ct),
            await _db.SupportTickets.CountAsync(x => !x.IsDeleted && !terminal.Contains(x.Status) && x.ResolutionDueAt > now && x.ResolutionDueAt <= now.AddHours(1), ct),
            await _db.TransferDisputes.CountAsync(x => !x.IsDeleted && x.Status != TransferDisputeStatus.Resolved && x.Status != TransferDisputeStatus.Rejected && x.Status != TransferDisputeStatus.Withdrawn, ct),
            await _db.TransferInvestigations.CountAsync(x => !x.IsDeleted && x.Status != TransferInvestigationStatus.Resolved && x.Status != TransferInvestigationStatus.Closed, ct),
            await _db.TransferInvestigations.CountAsync(x => !x.IsDeleted && x.DueAt < now && x.Status != TransferInvestigationStatus.Resolved && x.Status != TransferInvestigationStatus.Closed, ct),
            responseTotal == 0 ? 100m : Math.Round(responseMet * 100m / responseTotal, 2),
            totalResolved == 0 ? 100m : Math.Round(resolutionMet * 100m / totalResolved, 2));
    }

    public Task<int> ProcessSlaBreachesAsync(
        int batchSize,
        CancellationToken ct = default) =>
        ProcessSlaBreachesCoreAsync(
            batchSize,
            initiatedByUserId: null,
            manualReason: null,
            auditManualRun: false,
            ct);

    public Task<int> ProcessSlaBreachesAsync(
        int batchSize,
        Guid initiatedByUserId,
        string reason,
        CancellationToken ct = default)
    {
        var cleanedReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(cleanedReason))
            throw new InvalidOperationException(
                "A reason is required for manual support SLA processing.");

        if (cleanedReason.Length > 1000)
            throw new InvalidOperationException(
                "Reason cannot exceed 1000 characters.");

        return ProcessSlaBreachesCoreAsync(
            batchSize,
            initiatedByUserId,
            cleanedReason,
            auditManualRun: true,
            ct);
    }

    private async Task<int> ProcessSlaBreachesCoreAsync(
        int batchSize,
        Guid? initiatedByUserId,
        string? manualReason,
        bool auditManualRun,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        batchSize = Math.Clamp(batchSize, 1, 500);

        var tickets = await _db.SupportTickets
            .Where(x =>
                !x.IsDeleted &&
                x.Status != SupportTicketStatus.Resolved &&
                x.Status != SupportTicketStatus.Closed &&
                ((!x.FirstRespondedAt.HasValue &&
                  x.FirstResponseDueAt < now) ||
                 x.ResolutionDueAt < now) &&
                (!x.IsSlaBreached ||
                 x.LastEscalatedAt == null ||
                 x.LastEscalatedAt < now.AddHours(-24)))
            .OrderBy(x => x.ResolutionDueAt)
            .Take(batchSize)
            .ToListAsync(ct);

        foreach (var ticket in tickets)
        {
            ticket.IsSlaBreached = true;
            ticket.EscalationLevel++;
            ticket.LastEscalatedAt = now;
            ticket.LastUpdatedAt = now;

            if (ticket.AssignedToUserId.HasValue)
            {
                await _notifications.QueueUserAsync(
                    ticket.AssignedToUserId.Value,
                    $"SLA escalation: {ticket.Reference}",
                    $"Support ticket {ticket.Reference} has breached an SLA target and requires attention.",
                    nameof(SupportTicket),
                    ticket.Id,
                    ct);
            }

            _audit.Stage(new AuditRecordRequest(
                "SupportSlaBreached",
                "Support",
                nameof(SupportTicket),
                ticket.Id.ToString(),
                NewValues: new
                {
                    ticket.EscalationLevel,
                    ticket.FirstResponseDueAt,
                    ticket.ResolutionDueAt
                },
                Metadata: auditManualRun
                    ? new
                    {
                        Trigger = "Manual",
                        Reason = manualReason
                    }
                    : new
                    {
                        Trigger = "BackgroundWorker"
                    },
                UserId: initiatedByUserId));
        }

        if (auditManualRun)
        {
            _audit.Stage(new AuditRecordRequest(
                "SupportSlaProcessingRun",
                "Support",
                nameof(SupportTicket),
                "SLA",
                NewValues: new
                {
                    BatchSize = batchSize,
                    Processed = tickets.Count,
                    CheckedAt = now
                },
                Metadata: new
                {
                    Reason = manualReason
                },
                UserId: initiatedByUserId));
        }

        if (tickets.Count > 0 || auditManualRun)
            await _db.SaveChangesAsync(ct);

        return tickets.Count;
    }
    private IQueryable<SupportTicket> TicketQuery() => _db.SupportTickets.AsNoTracking().Include(x => x.Transfer).Include(x => x.Messages).Include(x => x.Evidence).Where(x => !x.IsDeleted);
    private SupportSlaTargetOptions GetSlaTarget(SupportTicketPriority priority) => _options.SlaTargets.TryGetValue(priority, out var target) ? target : throw new InvalidOperationException($"SLA target is not configured for {priority} priority.");

    private async Task ReleaseOperationalHoldIfClearAsync(Transfer transfer, Guid? resolvingDisputeId, Guid? resolvingInvestigationId, CancellationToken ct)
    {
        var disputes = await _db.TransferDisputes.AnyAsync(x => x.TransferId == transfer.Id && (!resolvingDisputeId.HasValue || x.Id != resolvingDisputeId.Value) && !x.IsDeleted && x.Status != TransferDisputeStatus.Resolved && x.Status != TransferDisputeStatus.Rejected && x.Status != TransferDisputeStatus.Withdrawn, ct);
        var investigations = await _db.TransferInvestigations.AnyAsync(x => x.TransferId == transfer.Id && (!resolvingInvestigationId.HasValue || x.Id != resolvingInvestigationId.Value) && !x.IsDeleted && x.Status != TransferInvestigationStatus.Resolved && x.Status != TransferInvestigationStatus.Closed, ct);
        if (!disputes && !investigations) { transfer.IsOperationalHold = false; transfer.OperationalHoldReason = null; transfer.LastUpdatedAt = DateTime.UtcNow; }
    }

    private static void ApplyOperationalHold(Transfer transfer, string reason)
    {
        if (transfer.Status is TransferStatus.Completed or TransferStatus.Cancelled or TransferStatus.Refunded) return;
        transfer.IsOperationalHold = true; transfer.OperationalHoldReason = reason; transfer.LastUpdatedAt = DateTime.UtcNow;
    }

    private void AddTimeline(Transfer transfer, string eventType, string title, string description) => _db.TransferTimelineEvents.Add(new TransferTimelineEvent { TransferId = transfer.Id, EventType = eventType, Title = title, Description = description });

    private async Task<string> GenerateUniqueReferenceAsync(CancellationToken ct)
    {
        for (var i = 0; i < 8; i++) { var value = _references.GenerateInvestigationReference(); if (!await _db.TransferInvestigations.AnyAsync(x => x.Reference == value, ct)) return value; }
        throw new InvalidOperationException("Unable to generate a unique investigation reference.");
    }
}
