using Microsoft.AspNetCore.Http;

namespace KorridorX.Services.Support;

public sealed record StoredSupportEvidence(
    string StorageKey,
    string FileName,
    string MimeType);

public sealed record OpenedSupportEvidence(
    Stream Content,
    string MimeType);

public interface ISupportEvidenceStorage
{
    Task<StoredSupportEvidence> SaveAsync(
        IFormFile file,
        string declaredMimeType,
        CancellationToken ct = default);

    Task<OpenedSupportEvidence> OpenReadAsync(
        string storageKey,
        string mimeType,
        CancellationToken ct = default);

    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken ct = default);
}
