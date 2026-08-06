using KorridorX.Dtos.Compliance;

namespace KorridorX.Services.Compliance;

public interface IBusinessKybService
{
    Task<BusinessKybApplicationDto> StartAsync(Guid userId, StartBusinessKybRequestDto request, CancellationToken ct = default);
    Task<BusinessKybStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default);
    Task<BusinessKybApplicationDto> GetApplicationAsync(Guid userId, Guid applicationId, CancellationToken ct = default);
    Task<BusinessKybApplicationDto> UpdateProfileAsync(Guid userId, Guid applicationId, UpdateBusinessKybProfileRequestDto request, CancellationToken ct = default);
    Task<BusinessOwnerDto> AddOwnerAsync(Guid userId, Guid applicationId, CreateBusinessOwnerRequestDto request, CancellationToken ct = default);
    Task<BusinessOwnerDto> UpdateOwnerAsync(Guid userId, Guid applicationId, Guid ownerId, UpdateBusinessOwnerRequestDto request, CancellationToken ct = default);
    Task<BusinessUploadUrlDto> RequestOwnerUploadUrlAsync(Guid userId, Guid applicationId, Guid ownerId, BusinessOwnerUploadUrlRequestDto request, CancellationToken ct = default);
    Task<BusinessOwnerDto> ConfirmOwnerUploadAsync(Guid userId, Guid applicationId, Guid ownerId, ConfirmBusinessOwnerUploadRequestDto request, CancellationToken ct = default);
    Task<BusinessUploadUrlDto> RequestDocumentUploadUrlAsync(Guid userId, Guid applicationId, BusinessDocumentUploadUrlRequestDto request, CancellationToken ct = default);
    Task<BusinessKybDocumentDto> ConfirmDocumentUploadAsync(Guid userId, Guid applicationId, Guid documentId, ConfirmBusinessDocumentUploadRequestDto request, CancellationToken ct = default);
    Task<BusinessKybApplicationDto> SubmitAsync(Guid userId, Guid applicationId, CancellationToken ct = default);
}
