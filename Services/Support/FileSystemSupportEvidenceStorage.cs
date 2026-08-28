using KorridorX.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace KorridorX.Services.Support;

public sealed class FileSystemSupportEvidenceStorage : ISupportEvidenceStorage
{
    private readonly string _rootPath;
    private readonly long _maxFileSizeBytes;

    public FileSystemSupportEvidenceStorage(
        IWebHostEnvironment environment,
        IOptions<SupportOptions> options)
    {
        var configured = options.Value.EvidenceRootPath.Trim();
        _rootPath = Path.GetFullPath(
            Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured));
        _maxFileSizeBytes = options.Value.EvidenceMaxFileSizeBytes;
    }

    public async Task<StoredSupportEvidence> SaveAsync(
        IFormFile file,
        string declaredMimeType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var policy = SupportEvidenceFilePolicy.ValidateMetadata(
            file.FileName,
            declaredMimeType,
            file.Length,
            _maxFileSizeBytes);

        var storageKey = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{policy.Extension}";
        var destination = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Support evidence storage path is invalid.");
        Directory.CreateDirectory(directory);

        var temporary = destination + ".uploading-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var input = file.OpenReadStream())
            await using (var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81_920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var prefix = new byte[8];
                var prefixLength = 0;
                var total = 0L;
                var buffer = new byte[81_920];

                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0) break;

                    if (prefixLength < prefix.Length)
                    {
                        var copy = Math.Min(read, prefix.Length - prefixLength);
                        buffer.AsSpan(0, copy).CopyTo(prefix.AsSpan(prefixLength));
                        prefixLength += copy;
                    }

                    total += read;
                    if (total > _maxFileSizeBytes)
                        throw new InvalidOperationException($"Support evidence cannot exceed {_maxFileSizeBytes / (1024 * 1024)} MB.");

                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }

                if (total != file.Length)
                    throw new InvalidOperationException("The uploaded support evidence length did not match the request.");

                SupportEvidenceFilePolicy.ValidateSignature(policy.MimeType, prefix.AsSpan(0, prefixLength));
                await output.FlushAsync(ct);
            }

            File.Move(temporary, destination, overwrite: false);
            return new StoredSupportEvidence(storageKey, policy.FileName, policy.MimeType);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    public Task<OpenedSupportEvidence> OpenReadAsync(
        string storageKey,
        string mimeType,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
            throw new InvalidOperationException("Support evidence file is unavailable.");

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81_920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(new OpenedSupportEvidence(stream, mimeType));
    }

    public Task DeleteIfExistsAsync(string storageKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var path = ResolvePath(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        var rootWithSeparator = _rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Support evidence storage key is invalid.");
        return path;
    }
}

public sealed record SupportEvidenceFileMetadata(
    string FileName,
    string MimeType,
    string Extension);

public static class SupportEvidenceFilePolicy
{
    private static readonly IReadOnlyDictionary<string, string> Extensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png"
        };

    public static SupportEvidenceFileMetadata ValidateMetadata(
        string fileName,
        string declaredMimeType,
        long fileSizeBytes,
        long maxFileSizeBytes)
    {
        if (fileSizeBytes <= 0)
            throw new InvalidOperationException("Support evidence cannot be empty.");
        if (fileSizeBytes > maxFileSizeBytes)
            throw new InvalidOperationException($"Support evidence cannot exceed {maxFileSizeBytes / (1024 * 1024)} MB.");

        var mimeType = declaredMimeType.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (!Extensions.TryGetValue(mimeType, out var extension))
            throw new InvalidOperationException("Support evidence must be a PDF, JPEG, or PNG file.");

        var safeName = Path.GetFileName(fileName).Trim();
        if (string.IsNullOrWhiteSpace(safeName) || safeName.Length > 255 || safeName.Any(char.IsControl))
            throw new InvalidOperationException("Support evidence file name is invalid.");

        var suppliedExtension = Path.GetExtension(safeName);
        var extensionMatches = mimeType == "image/jpeg"
            ? suppliedExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || suppliedExtension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            : suppliedExtension.Equals(extension, StringComparison.OrdinalIgnoreCase);
        if (!extensionMatches)
            throw new InvalidOperationException("Support evidence file extension does not match its declared type.");

        return new SupportEvidenceFileMetadata(safeName, mimeType, extension);
    }

    public static void ValidateSignature(string mimeType, ReadOnlySpan<byte> prefix)
    {
        var valid = mimeType switch
        {
            "application/pdf" => prefix.StartsWith("%PDF-"u8),
            "image/jpeg" => prefix.Length >= 3 && prefix[0] == 0xFF && prefix[1] == 0xD8 && prefix[2] == 0xFF,
            "image/png" => prefix.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException("Support evidence content does not match its declared file type.");
    }
}
