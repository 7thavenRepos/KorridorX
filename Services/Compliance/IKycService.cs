using KorridorX.Dtos.Compliance;

namespace KorridorX.Services.Compliance;

public interface IKycService
{
    Task<KycApplicationDto> StartMyApplicationAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<KycStatusDto> GetMyStatusAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<KycApplicationDto> SaveIdentityAsync(
        Guid userId,
        Guid applicationId,
        SaveKycIdentityRequestDto request,
        CancellationToken ct = default);

    Task<KycDocumentUploadUrlDto> CreateDocumentUploadUrlAsync(
        Guid userId,
        Guid applicationId,
        CreateKycDocumentUploadUrlRequestDto request,
        CancellationToken ct = default);

    Task<KycDocumentDto> ConfirmDocumentUploadAsync(
        Guid userId,
        Guid applicationId,
        Guid documentId,
        ConfirmKycDocumentUploadRequestDto request,
        CancellationToken ct = default);

    Task<KycApplicationDto> SubmitApplicationAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken ct = default);
}
