using KorridorX.Dtos.Support;
using KorridorX.Infrastructure;
using KorridorX.Models.Enums;

namespace KorridorX.Services.Support;

public interface IAdminSupportService
{
    Task<PagedResult<SupportTicketListItemDto>> GetTicketsAsync(SupportTicketStatus? status, SupportTicketPriority? priority, Guid? assignedToUserId, bool? slaBreached, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<SupportTicketDetailsDto> GetTicketAsync(Guid ticketId, CancellationToken ct = default);
    Task<SupportTicketDetailsDto> ReplyAsync(Guid agentUserId, Guid ticketId, AdminAddSupportMessageRequestDto request, CancellationToken ct = default);
    Task<SupportTicketDetailsDto> UpdateTicketAsync(Guid agentUserId, Guid ticketId, UpdateSupportTicketRequestDto request, CancellationToken ct = default);
    Task<PagedResult<TransferDisputeDto>> GetDisputesAsync(TransferDisputeStatus? status, Guid? transferId, int page, int pageSize, CancellationToken ct = default);
    Task<TransferDisputeDto> ResolveDisputeAsync(Guid agentUserId, Guid disputeId, ResolveTransferDisputeRequestDto request, CancellationToken ct = default);
    Task<TransferInvestigationDto> CreateInvestigationAsync(Guid agentUserId, Guid transferId, CreateTransferInvestigationRequestDto request, CancellationToken ct = default);
    Task<PagedResult<TransferInvestigationDto>> GetInvestigationsAsync(TransferInvestigationStatus? status, Guid? assignedToUserId, bool? overdue, int page, int pageSize, CancellationToken ct = default);
    Task<TransferInvestigationDto> UpdateInvestigationAsync(Guid agentUserId, Guid investigationId, UpdateTransferInvestigationRequestDto request, CancellationToken ct = default);
    Task<SupportEvidenceDto> AddInvestigationEvidenceAsync(Guid agentUserId, Guid investigationId, AddSupportEvidenceRequestDto request, CancellationToken ct = default);
    Task<SupportOperationsSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<int> ProcessSlaBreachesAsync(int batchSize, CancellationToken ct = default);
}
