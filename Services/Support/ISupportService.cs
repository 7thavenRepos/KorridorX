using KorridorX.Dtos.Support;
using KorridorX.Infrastructure;

namespace KorridorX.Services.Support;

public interface ISupportService
{
    Task<SupportTicketDetailsDto> CreateTicketAsync(Guid userId, CreateSupportTicketRequestDto request, CancellationToken ct = default);
    Task<PagedResult<SupportTicketListItemDto>> GetMyTicketsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<SupportTicketDetailsDto> GetMyTicketAsync(Guid userId, Guid ticketId, CancellationToken ct = default);
    Task<SupportTicketDetailsDto> AddCustomerMessageAsync(Guid userId, Guid ticketId, AddSupportMessageRequestDto request, CancellationToken ct = default);
    Task<SupportEvidenceDto> AddTicketEvidenceAsync(Guid userId, Guid ticketId, UploadSupportEvidenceRequestDto request, CancellationToken ct = default);
    Task<TransferDisputeDto> CreateDisputeAsync(Guid userId, Guid transferId, CreateTransferDisputeRequestDto request, CancellationToken ct = default);
    Task<PagedResult<TransferDisputeDto>> GetMyDisputesAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<TransferDisputeDto> WithdrawDisputeAsync(Guid userId, Guid disputeId, CancellationToken ct = default);
    Task<SupportEvidenceDto> AddDisputeEvidenceAsync(Guid userId, Guid disputeId, UploadSupportEvidenceRequestDto request, CancellationToken ct = default);
    Task<SupportEvidenceDownloadDto> DownloadEvidenceAsync(Guid userId, Guid evidenceId, bool canManageSupport, CancellationToken ct = default);
}
