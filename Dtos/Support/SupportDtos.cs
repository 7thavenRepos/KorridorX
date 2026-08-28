using System.ComponentModel.DataAnnotations;
using KorridorX.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace KorridorX.Dtos.Support;

public sealed class CreateSupportTicketRequestDto
{
    [Required, MaxLength(250)] public string Subject { get; set; } = "";
    [Required, MaxLength(4000)] public string Description { get; set; } = "";
    public SupportTicketCategory Category { get; set; } = SupportTicketCategory.General;
    public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Normal;
    public Guid? TransferId { get; set; }
}

public class AddSupportMessageRequestDto
{
    [Required, MaxLength(8000)] public string Body { get; set; } = "";
}

public sealed class AdminAddSupportMessageRequestDto : AddSupportMessageRequestDto
{
    public bool IsInternal { get; set; }
}

public sealed class AssignSupportTicketRequestDto
{
    public Guid? AssignedToUserId { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}

public sealed class UpdateSupportTicketRequestDto
{
    public SupportTicketStatus? Status { get; set; }
    public SupportTicketPriority? Priority { get; set; }
    public Guid? AssignedToUserId { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}

public sealed class CreateTransferDisputeRequestDto
{
    public Guid? SupportTicketId { get; set; }
    public TransferDisputeType DisputeType { get; set; }
    [Required, MaxLength(4000)] public string Reason { get; set; } = "";
    public decimal? RequestedRefundAmount { get; set; }
}

public sealed class ResolveTransferDisputeRequestDto
{
    public TransferDisputeStatus Status { get; set; }
    [Required, MaxLength(4000)] public string ResolutionNote { get; set; } = "";
}

public sealed class CreateTransferInvestigationRequestDto
{
    public Guid? TransferDisputeId { get; set; }
    public Guid? SupportTicketId { get; set; }
    [Required, MaxLength(4000)] public string Summary { get; set; } = "";
    public Guid? AssignedToUserId { get; set; }
    public int? DueHours { get; set; }
}

public sealed class UpdateTransferInvestigationRequestDto
{
    public TransferInvestigationStatus? Status { get; set; }
    public TransferInvestigationOutcome? Outcome { get; set; }
    public Guid? AssignedToUserId { get; set; }
    [MaxLength(8000)] public string? Findings { get; set; }
    [MaxLength(200)] public string? ProviderCaseReference { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}

public sealed class UploadSupportEvidenceRequestDto
{
    [MaxLength(2000)] public string? Description { get; set; }
    [Required, MaxLength(100)] public string MimeType { get; set; } = "";
    [Required] public IFormFile File { get; set; } = null!;
}

// Retained for the existing administrator investigation-evidence workflow.
// Consumer ticket and dispute uploads must use UploadSupportEvidenceRequestDto.
public sealed class AddSupportEvidenceRequestDto
{
    [Required, MaxLength(255)] public string Name { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    [MaxLength(100)] public string? MimeType { get; set; }
    [Required, MaxLength(1000)] public string StorageKey { get; set; } = "";
    [MaxLength(2000)] public string? StorageUrl { get; set; }
}

public sealed record SupportEvidenceDownloadDto(
    Stream Content,
    string MimeType,
    string FileName);

public sealed class ProcessSupportSlaRequestDto
{
    [Range(1, 500)]
    public int BatchSize { get; set; } = 100;

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = "";
}
public sealed record SupportTicketListItemDto(
    Guid Id,
    string Reference,
    SupportTicketCategory Category,
    SupportTicketPriority Priority,
    SupportTicketStatus Status,
    string Subject,
    Guid? TransferId,
    string? TransferReference,
    Guid? AssignedToUserId,
    DateTime FirstResponseDueAt,
    DateTime ResolutionDueAt,
    bool IsSlaBreached,
    int EscalationLevel,
    DateTime CreatedAt,
    DateTime? LastUpdatedAt);

public sealed record SupportMessageDto(
    Guid Id,
    Guid? AuthorUserId,
    SupportMessageAuthorType AuthorType,
    string Body,
    bool IsInternal,
    DateTime CreatedAt);

public sealed record SupportEvidenceDto(
    Guid Id,
    string Name,
    string? Description,
    string? MimeType,
    string StorageKey,
    string? StorageUrl,
    Guid SubmittedByUserId,
    DateTime CreatedAt);

public sealed record SupportTicketDetailsDto(
    SupportTicketListItemDto Ticket,
    Guid UserId,
    Guid? CustomerProfileId,
    Guid? BusinessProfileId,
    string Summary,
    DateTime? FirstRespondedAt,
    DateTime? ResolvedAt,
    DateTime? ClosedAt,
    DateTime? LastCustomerMessageAt,
    DateTime? LastAgentMessageAt,
    IReadOnlyList<SupportMessageDto> Messages,
    IReadOnlyList<SupportEvidenceDto> Evidence);

public sealed record TransferDisputeDto(
    Guid Id,
    string Reference,
    Guid TransferId,
    string TransferReference,
    Guid SupportTicketId,
    TransferDisputeType DisputeType,
    TransferDisputeStatus Status,
    string Reason,
    decimal? RequestedRefundAmount,
    string? CurrencyCode,
    Guid? AssignedToUserId,
    DateTime OpenedAt,
    DateTime? ResolvedAt,
    string? ResolutionNote,
    DateTime CreatedAt);

public sealed record TransferInvestigationDto(
    Guid Id,
    string Reference,
    Guid TransferId,
    string TransferReference,
    Guid? TransferDisputeId,
    Guid? SupportTicketId,
    TransferInvestigationStatus Status,
    TransferInvestigationOutcome Outcome,
    Guid? AssignedToUserId,
    string Summary,
    string? Findings,
    string? ProviderCaseReference,
    DateTime StartedAt,
    DateTime DueAt,
    DateTime? ResolvedAt,
    DateTime CreatedAt);

public sealed record TransferDisputeDetailsDto(
    TransferDisputeDto Dispute,
    IReadOnlyList<SupportEvidenceDto> Evidence,
    IReadOnlyList<TransferInvestigationDto> Investigations);

public sealed record TransferInvestigationDetailsDto(
    TransferInvestigationDto Investigation,
    IReadOnlyList<SupportEvidenceDto> Evidence);
public sealed record SupportOperationsSummaryDto(
    int OpenTickets,
    int UnassignedTickets,
    int UrgentTickets,
    int SlaBreachedTickets,
    int DueWithinOneHour,
    int OpenDisputes,
    int OpenInvestigations,
    int OverdueInvestigations,
    decimal FirstResponseSlaPercent,
    decimal ResolutionSlaPercent);
