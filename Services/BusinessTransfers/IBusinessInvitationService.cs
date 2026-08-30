using KorridorX.Dtos.BusinessTransfers;

namespace KorridorX.Services.BusinessTransfers;

public interface IBusinessInvitationService
{
    Task<IReadOnlyList<BusinessInvitationDto>> GetInvitationsAsync(Guid userId, CancellationToken ct = default);
    Task<BusinessInvitationDto> CreateAsync(Guid userId, CreateBusinessInvitationRequestDto request, CancellationToken ct = default);
    Task<BusinessInvitationDto> ResendAsync(Guid userId, Guid invitationId, CancellationToken ct = default);
    Task RevokeAsync(Guid userId, Guid invitationId, CancellationToken ct = default);
    Task<BusinessInvitationPreviewDto> GetPreviewAsync(string token, CancellationToken ct = default);
    Task ValidateForRegistrationAsync(string token, string email, CancellationToken ct = default);
    Task AcceptAsync(string token, Guid userId, string email, bool saveChanges = true, CancellationToken ct = default);
}
