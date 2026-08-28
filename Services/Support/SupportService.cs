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
using KorridorX.Services.BusinessContext;
using KorridorX.Services.Notifications;
using KorridorX.Services.References;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Support;

public sealed class SupportService : ISupportService
{
    private readonly AppDbContext _db;
    private readonly IReferenceGenerator _references;
    private readonly INotificationQueueService _notifications;
    private readonly IAuditService _audit;
    private readonly IBusinessContextAccessor _businessContext;
    private readonly ISupportEvidenceStorage _evidenceStorage;
    private readonly SupportOptions _options;

    public SupportService(
        AppDbContext db,
        IReferenceGenerator references,
        INotificationQueueService notifications,
        IAuditService audit,
        IBusinessContextAccessor businessContext,
        ISupportEvidenceStorage evidenceStorage,
        IOptions<SupportOptions> options)
    {
        _db = db;
        _references = references;
        _notifications = notifications;
        _audit = audit;
        _businessContext = businessContext;
        _evidenceStorage = evidenceStorage;
        _options = options.Value;
    }

    public async Task<SupportTicketDetailsDto> CreateTicketAsync(
        Guid userId,
        CreateSupportTicketRequestDto request,
        CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var now = DateTime.UtcNow;
        Guid? customerProfileId = null;
        Guid? businessProfileId = null;
        Transfer? transfer = null;

        if (request.TransferId.HasValue)
        {
            transfer = await OwnedTransfers(userId, selectedBusinessProfileId)
                .FirstOrDefaultAsync(x => x.Id == request.TransferId.Value, ct)
                ?? throw new InvalidOperationException("Transfer not found or is not accessible to the authenticated user.");
            customerProfileId = transfer.CustomerProfileId;
            businessProfileId = transfer.BusinessProfileId;
        }
        else if (selectedBusinessProfileId.HasValue)
        {
            businessProfileId = selectedBusinessProfileId;
        }
        else
        {
            customerProfileId = await _db.CustomerProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(ct);
        }

        var target = GetSlaTarget(request.Priority);
        var ticket = new SupportTicket
        {
            Reference = await GenerateUniqueReferenceAsync(_references.GenerateSupportTicketReference, x => _db.SupportTickets.AnyAsync(t => t.Reference == x, ct)),
            UserId = userId,
            CustomerProfileId = customerProfileId,
            BusinessProfileId = businessProfileId,
            TransferId = transfer?.Id,
            Category = request.Category,
            Priority = request.Priority,
            Status = SupportTicketStatus.Open,
            Subject = request.Subject.Trim(),
            Summary = request.Description.Trim(),
            FirstResponseDueAt = now.AddMinutes(target.FirstResponseMinutes),
            ResolutionDueAt = now.AddMinutes(target.ResolutionMinutes),
            LastCustomerMessageAt = now,
            CreatedByUserId = userId
        };

        ticket.Messages.Add(new SupportTicketMessage
        {
            AuthorUserId = userId,
            AuthorType = SupportMessageAuthorType.Customer,
            Body = request.Description.Trim(),
            IsInternal = false
        });

        _db.SupportTickets.Add(ticket);
        AddTransferTimeline(transfer, "SUPPORT_TICKET_OPENED", "Support ticket opened", $"Support ticket {ticket.Reference} was opened.");
        _audit.Stage(new AuditRecordRequest("SupportTicketCreated", "Support", nameof(SupportTicket), ticket.Id.ToString(), null, new { ticket.Reference, ticket.Category, ticket.Priority, ticket.TransferId }, UserId: userId));
        await _notifications.QueueUserAsync(userId, $"Support ticket {ticket.Reference} created", "We received your support request and will update you as it progresses.", nameof(SupportTicket), ticket.Id, ct);
        await _db.SaveChangesAsync(ct);
        return await GetMyTicketAsync(userId, ticket.Id, ct);
    }

    public async Task<PagedResult<SupportTicketListItemDto>> GetMyTicketsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var paged = await _db.SupportTickets
            .AsNoTracking()
            .Include(x => x.Transfer)
            .Where(x => x.UserId == userId && !x.IsDeleted && x.BusinessProfileId == selectedBusinessProfileId)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);
        return new PagedResult<SupportTicketListItemDto>
        {
            Items = paged.Items.Select(ToListDtoForAdmin).ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<SupportTicketDetailsDto> GetMyTicketAsync(Guid userId, Guid ticketId, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var ticket = await TicketDetailsQuery()
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.UserId == userId && x.BusinessProfileId == selectedBusinessProfileId, ct)
            ?? throw new InvalidOperationException("Support ticket not found.");
        return ToDetailsDto(ticket, includeInternalMessages: false);
    }

    public async Task<SupportTicketDetailsDto> AddCustomerMessageAsync(Guid userId, Guid ticketId, AddSupportMessageRequestDto request, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var ticket = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == ticketId && x.UserId == userId && !x.IsDeleted && x.BusinessProfileId == selectedBusinessProfileId, ct)
            ?? throw new InvalidOperationException("Support ticket not found.");
        if (ticket.Status == SupportTicketStatus.Closed)
            throw new InvalidOperationException("A closed support ticket cannot receive new messages.");

        var now = DateTime.UtcNow;
        _db.SupportTicketMessages.Add(new SupportTicketMessage
        {
            SupportTicketId = ticket.Id,
            AuthorUserId = userId,
            AuthorType = SupportMessageAuthorType.Customer,
            Body = request.Body.Trim()
        });
        ticket.LastCustomerMessageAt = now;
        ticket.LastUpdatedAt = now;
        ticket.LastUpdatedByUserId = userId;
        if (ticket.Status is SupportTicketStatus.AwaitingCustomer or SupportTicketStatus.Resolved)
        {
            ticket.Status = SupportTicketStatus.InProgress;
            ticket.ResolvedAt = null;
        }
        _audit.Stage(new AuditRecordRequest("SupportTicketCustomerReply", "Support", nameof(SupportTicket), ticket.Id.ToString(), UserId: userId));
        await _db.SaveChangesAsync(ct);
        return await GetMyTicketAsync(userId, ticketId, ct);
    }

    public async Task<SupportEvidenceDto> AddTicketEvidenceAsync(Guid userId, Guid ticketId, UploadSupportEvidenceRequestDto request, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var ticket = await _db.SupportTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.UserId == userId && !x.IsDeleted && x.BusinessProfileId == selectedBusinessProfileId, ct)
            ?? throw new InvalidOperationException("Support ticket not found.");
        if (ticket.Status == SupportTicketStatus.Closed)
            throw new InvalidOperationException("Evidence cannot be added to a closed support ticket.");

        return await SaveEvidenceAsync(
            userId,
            request,
            ticketId,
            null,
            "SupportEvidenceAdded",
            new { ticketId },
            ct);
    }

    public async Task<TransferDisputeDto> CreateDisputeAsync(Guid userId, Guid transferId, CreateTransferDisputeRequestDto request, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var transfer = await OwnedTransfers(userId, selectedBusinessProfileId).FirstOrDefaultAsync(x => x.Id == transferId, ct)
            ?? throw new InvalidOperationException("Transfer not found or is not accessible to the authenticated user.");
        if (transfer.Status is TransferStatus.Draft or TransferStatus.Quoted)
            throw new InvalidOperationException("A dispute cannot be opened before a transfer has been submitted for payment.");

        var active = await _db.TransferDisputes.AsNoTracking().FirstOrDefaultAsync(x => x.TransferId == transferId && !x.IsDeleted && x.Status != TransferDisputeStatus.Resolved && x.Status != TransferDisputeStatus.Rejected && x.Status != TransferDisputeStatus.Withdrawn, ct);
        if (active is not null) throw new InvalidOperationException($"An active dispute already exists for this transfer ({active.Reference}).");

        SupportTicket ticket;
        if (request.SupportTicketId.HasValue)
        {
            ticket = await _db.SupportTickets.FirstOrDefaultAsync(x => x.Id == request.SupportTicketId.Value && x.UserId == userId && !x.IsDeleted && x.BusinessProfileId == selectedBusinessProfileId, ct)
                ?? throw new InvalidOperationException("Support ticket not found.");
        }
        else
        {
            var now = DateTime.UtcNow;
            var target = GetSlaTarget(SupportTicketPriority.High);
            ticket = new SupportTicket
            {
                Reference = await GenerateUniqueReferenceAsync(_references.GenerateSupportTicketReference, x => _db.SupportTickets.AnyAsync(t => t.Reference == x, ct)),
                UserId = userId,
                CustomerProfileId = transfer.CustomerProfileId,
                BusinessProfileId = transfer.BusinessProfileId,
                TransferId = transfer.Id,
                Category = SupportTicketCategory.Transfer,
                Priority = SupportTicketPriority.High,
                Status = SupportTicketStatus.Open,
                Subject = $"Transfer dispute - {transfer.Reference}",
                Summary = request.Reason.Trim(),
                FirstResponseDueAt = now.AddMinutes(target.FirstResponseMinutes),
                ResolutionDueAt = now.AddMinutes(target.ResolutionMinutes),
                LastCustomerMessageAt = now,
                CreatedByUserId = userId
            };
            ticket.Messages.Add(new SupportTicketMessage { AuthorUserId = userId, AuthorType = SupportMessageAuthorType.Customer, Body = request.Reason.Trim() });
            _db.SupportTickets.Add(ticket);
        }

        var dispute = new TransferDispute
        {
            Reference = await GenerateUniqueReferenceAsync(_references.GenerateDisputeReference, x => _db.TransferDisputes.AnyAsync(d => d.Reference == x, ct)),
            UserId = userId,
            TransferId = transfer.Id,
            SupportTicketId = ticket.Id,
            CustomerProfileId = transfer.CustomerProfileId,
            BusinessProfileId = transfer.BusinessProfileId,
            DisputeType = request.DisputeType,
            Reason = request.Reason.Trim(),
            RequestedRefundAmount = request.RequestedRefundAmount,
            CurrencyCode = transfer.SourceCurrencyCode,
            OpenedAt = DateTime.UtcNow,
            CreatedByUserId = userId
        };
        _db.TransferDisputes.Add(dispute);
        ApplyOperationalHold(transfer, $"Active transfer dispute {dispute.Reference}.");
        AddTransferTimeline(transfer, "TRANSFER_DISPUTE_OPENED", "Transfer dispute opened", $"Dispute {dispute.Reference} was opened for this transfer.");
        _audit.Stage(new AuditRecordRequest("TransferDisputeCreated", "Support", nameof(TransferDispute), dispute.Id.ToString(), Metadata: new { transfer.Id, dispute.Reference, dispute.DisputeType }, UserId: userId));
        await _notifications.QueueUserAsync(userId, $"Transfer dispute {dispute.Reference} opened", "We received your transfer dispute. The transfer will be reviewed by our support team.", nameof(TransferDispute), dispute.Id, ct);
        await _db.SaveChangesAsync(ct);
        return await GetOwnedDisputeAsync(userId, dispute.Id, ct);
    }

    public async Task<PagedResult<TransferDisputeDto>> GetMyDisputesAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var paged = await _db.TransferDisputes
            .AsNoTracking()
            .Include(x => x.Transfer)
            .Where(x => x.UserId == userId && !x.IsDeleted && x.BusinessProfileId == selectedBusinessProfileId)
            .OrderByDescending(x => x.CreatedAt)
            .PaginateAsync(page, pageSize, ct);
        return new PagedResult<TransferDisputeDto>
        {
            Items = paged.Items.Select(ToDisputeDto).ToList(),
            Meta = paged.Meta
        };
    }

    public async Task<TransferDisputeDto> WithdrawDisputeAsync(Guid userId, Guid disputeId, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var dispute = await _db.TransferDisputes.Include(x => x.Transfer).FirstOrDefaultAsync(x => x.Id == disputeId && x.UserId == userId && !x.IsDeleted && x.BusinessProfileId == selectedBusinessProfileId, ct)
            ?? throw new InvalidOperationException("Transfer dispute not found.");
        if (dispute.Status is TransferDisputeStatus.Resolved or TransferDisputeStatus.Rejected or TransferDisputeStatus.Withdrawn)
            return ToDisputeDto(dispute);
        dispute.Status = TransferDisputeStatus.Withdrawn;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.ResolutionNote = "Withdrawn by the customer.";
        dispute.LastUpdatedAt = DateTime.UtcNow;
        dispute.LastUpdatedByUserId = userId;
        await ReleaseOperationalHoldIfClearAsync(dispute.Transfer, dispute.Id, ct);
        AddTransferTimeline(dispute.Transfer, "TRANSFER_DISPUTE_WITHDRAWN", "Transfer dispute withdrawn", $"Dispute {dispute.Reference} was withdrawn by the customer.");
        _audit.Stage(new AuditRecordRequest("TransferDisputeWithdrawn", "Support", nameof(TransferDispute), dispute.Id.ToString(), UserId: userId));
        await _db.SaveChangesAsync(ct);
        return ToDisputeDto(dispute);
    }

    public async Task<SupportEvidenceDto> AddDisputeEvidenceAsync(Guid userId, Guid disputeId, UploadSupportEvidenceRequestDto request, CancellationToken ct = default)
    {
        var selectedBusinessProfileId = await ResolveSelectedBusinessAsync(userId, ct);
        var dispute = await _db.TransferDisputes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == disputeId && x.UserId == userId && !x.IsDeleted && x.BusinessProfileId == selectedBusinessProfileId, ct)
            ?? throw new InvalidOperationException("Transfer dispute not found.");
        if (dispute.Status is TransferDisputeStatus.Resolved or TransferDisputeStatus.Rejected or TransferDisputeStatus.Withdrawn)
            throw new InvalidOperationException("Evidence cannot be added to a closed transfer dispute.");

        return await SaveEvidenceAsync(
            userId,
            request,
            dispute.SupportTicketId,
            dispute.Id,
            "DisputeEvidenceAdded",
            new { disputeId },
            ct);
    }

    public async Task<SupportEvidenceDownloadDto> DownloadEvidenceAsync(
        Guid userId,
        Guid evidenceId,
        bool canManageSupport,
        CancellationToken ct = default)
    {
        var selectedBusinessProfileId = canManageSupport ? null : await ResolveSelectedBusinessAsync(userId, ct);
        var evidence = await _db.SupportEvidence
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == evidenceId &&
                !x.IsDeleted &&
                (canManageSupport ||
                 (x.SupportTicket != null && x.SupportTicket.UserId == userId && !x.SupportTicket.IsDeleted && x.SupportTicket.BusinessProfileId == selectedBusinessProfileId) ||
                 (x.TransferDispute != null && x.TransferDispute.UserId == userId && !x.TransferDispute.IsDeleted && x.TransferDispute.BusinessProfileId == selectedBusinessProfileId)),
                ct)
            ?? throw new InvalidOperationException("Support evidence not found.");

        var opened = await _evidenceStorage.OpenReadAsync(
            evidence.StorageKey,
            evidence.MimeType ?? "application/octet-stream",
            ct);
        return new SupportEvidenceDownloadDto(opened.Content, opened.MimeType, evidence.Name);
    }

    private IQueryable<Transfer> OwnedTransfers(Guid userId, Guid? selectedBusinessProfileId) => _db.Transfers
        .Include(x => x.CustomerProfile)
        .Include(x => x.BusinessProfile)
        .Where(x => !x.IsDeleted && (selectedBusinessProfileId.HasValue
            ? x.BusinessProfileId == selectedBusinessProfileId && x.BusinessProfile != null && (x.BusinessProfile.OwnerUserId == userId || x.BusinessProfile.Users.Any(u => u.UserId == userId && u.IsActive && !u.IsDeleted))
            : x.BusinessProfileId == null && x.CustomerProfileId != null && x.CustomerProfile!.UserId == userId));

    private async Task<Guid?> ResolveSelectedBusinessAsync(Guid userId, CancellationToken ct)
    {
        var selected = _businessContext.GetSelectedBusinessProfileId();
        if (!selected.HasValue) return null;
        var allowed = await _db.BusinessProfiles.AnyAsync(x => x.Id == selected.Value && !x.IsDeleted && (x.OwnerUserId == userId || x.Users.Any(u => u.UserId == userId && u.IsActive && !u.IsDeleted)), ct);
        if (!allowed) throw new InvalidOperationException("The authenticated user does not have access to the selected business profile.");
        return selected;
    }

    private SupportSlaTargetOptions GetSlaTarget(SupportTicketPriority priority) =>
        _options.SlaTargets.TryGetValue(priority, out var target) ? target : throw new InvalidOperationException($"SLA target is not configured for {priority} priority.");

    private IQueryable<SupportTicket> TicketDetailsQuery() => _db.SupportTickets.AsNoTracking()
        .Include(x => x.Transfer)
        .Include(x => x.Messages)
        .Include(x => x.Evidence)
        .Where(x => !x.IsDeleted);

    private async Task<TransferDisputeDto> GetOwnedDisputeAsync(Guid userId, Guid disputeId, CancellationToken ct) =>
        ToDisputeDto(await _db.TransferDisputes.AsNoTracking().Include(x => x.Transfer).FirstAsync(x => x.Id == disputeId && x.UserId == userId && !x.IsDeleted, ct));

    internal static SupportTicketListItemDto ToListDtoForAdmin(SupportTicket x) => new(x.Id, x.Reference, x.Category, x.Priority, x.Status, x.Subject, x.TransferId, x.Transfer != null ? x.Transfer.Reference : null, x.AssignedToUserId, x.FirstResponseDueAt, x.ResolutionDueAt, x.IsSlaBreached, x.EscalationLevel, x.CreatedAt, x.LastUpdatedAt);

    internal static SupportTicketDetailsDto ToDetailsDto(SupportTicket x, bool includeInternalMessages) => new(
        ToListDtoForAdmin(x), x.UserId, x.CustomerProfileId, x.BusinessProfileId, x.Summary, x.FirstRespondedAt, x.ResolvedAt, x.ClosedAt, x.LastCustomerMessageAt, x.LastAgentMessageAt,
        x.Messages.Where(m => includeInternalMessages || !m.IsInternal).OrderBy(m => m.CreatedAt).Select(m => new SupportMessageDto(m.Id, m.AuthorUserId, m.AuthorType, m.Body, m.IsInternal, m.CreatedAt)).ToList(),
        x.Evidence.OrderByDescending(e => e.CreatedAt).Select(ToEvidenceDto).ToList());

    internal static TransferDisputeDto ToDisputeDto(TransferDispute x) => new(x.Id, x.Reference, x.TransferId, x.Transfer != null ? x.Transfer.Reference : "", x.SupportTicketId, x.DisputeType, x.Status, x.Reason, x.RequestedRefundAmount, x.CurrencyCode, x.AssignedToUserId, x.OpenedAt, x.ResolvedAt, x.ResolutionNote, x.CreatedAt);
    internal static TransferInvestigationDto ToInvestigationDto(TransferInvestigation x) => new(x.Id, x.Reference, x.TransferId, x.Transfer != null ? x.Transfer.Reference : "", x.TransferDisputeId, x.SupportTicketId, x.Status, x.Outcome, x.AssignedToUserId, x.Summary, x.Findings, x.ProviderCaseReference, x.StartedAt, x.DueAt, x.ResolvedAt, x.CreatedAt);
    internal static SupportEvidenceDto ToEvidenceDto(SupportEvidence x) => new(x.Id, x.Name, x.Description, x.MimeType, x.StorageKey, x.StorageUrl, x.SubmittedByUserId, x.CreatedAt);

    private async Task<SupportEvidenceDto> SaveEvidenceAsync(
        Guid userId,
        UploadSupportEvidenceRequestDto request,
        Guid? ticketId,
        Guid? disputeId,
        string auditAction,
        object auditMetadata,
        CancellationToken ct)
    {
        var stored = await _evidenceStorage.SaveAsync(request.File, request.MimeType, ct);
        try
        {
            var evidence = new SupportEvidence
            {
                SupportTicketId = ticketId,
                TransferDisputeId = disputeId,
                SubmittedByUserId = userId,
                Name = stored.FileName,
                Description = request.Description?.Trim(),
                MimeType = stored.MimeType,
                StorageKey = stored.StorageKey,
                StorageUrl = null
            };
            evidence.StorageUrl = $"/api/support/evidence/{evidence.Id}/download";
            _db.SupportEvidence.Add(evidence);
            _audit.Stage(new AuditRecordRequest(
                auditAction,
                "Support",
                nameof(SupportEvidence),
                evidence.Id.ToString(),
                Metadata: auditMetadata,
                UserId: userId));
            await _db.SaveChangesAsync(ct);
            return ToEvidenceDto(evidence);
        }
        catch
        {
            await _evidenceStorage.DeleteIfExistsAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    private static void ApplyOperationalHold(Transfer transfer, string reason)
    {
        if (transfer.Status is TransferStatus.Completed or TransferStatus.Cancelled or TransferStatus.Refunded) return;
        transfer.IsOperationalHold = true;
        transfer.OperationalHoldReason = reason;
        transfer.LastUpdatedAt = DateTime.UtcNow;
    }

    private async Task ReleaseOperationalHoldIfClearAsync(Transfer transfer, Guid resolvingDisputeId, CancellationToken ct)
    {
        var otherDisputes = await _db.TransferDisputes.AnyAsync(x => x.TransferId == transfer.Id && x.Id != resolvingDisputeId && !x.IsDeleted && x.Status != TransferDisputeStatus.Resolved && x.Status != TransferDisputeStatus.Rejected && x.Status != TransferDisputeStatus.Withdrawn, ct);
        var investigations = await _db.TransferInvestigations.AnyAsync(x => x.TransferId == transfer.Id && !x.IsDeleted && x.Status != TransferInvestigationStatus.Resolved && x.Status != TransferInvestigationStatus.Closed, ct);
        if (!otherDisputes && !investigations) { transfer.IsOperationalHold = false; transfer.OperationalHoldReason = null; transfer.LastUpdatedAt = DateTime.UtcNow; }
    }

    private void AddTransferTimeline(Transfer? transfer, string eventType, string title, string description)
    {
        if (transfer is null) return;
        _db.TransferTimelineEvents.Add(new TransferTimelineEvent { TransferId = transfer.Id, EventType = eventType, Title = title, Description = description });
    }

    private static async Task<string> GenerateUniqueReferenceAsync(Func<string> generate, Func<string, Task<bool>> exists)
    {
        for (var i = 0; i < 8; i++) { var value = generate(); if (!await exists(value)) return value; }
        throw new InvalidOperationException("Unable to generate a unique reference.");
    }
}
