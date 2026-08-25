using KorridorX.Data;
using KorridorX.Dtos.EmbeddedFinance;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessTransfers;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinanceManagementService : IEmbeddedFinanceManagementService
{
    private readonly AppDbContext _db; private readonly IBusinessAccessService _access;
    public EmbeddedFinanceManagementService(AppDbContext db, IBusinessAccessService access) { _db = db; _access = access; }

    public async Task<IReadOnlyList<ApiApplicationDto>> GetApplicationsAsync(Guid userId, CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        var rows = await _db.ApiApplications.AsNoTracking().Where(x => x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted).OrderBy(x => x.Name).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ApiApplicationDto> CreateApplicationAsync(Guid userId, CreateApiApplicationRequestDto request, CancellationToken ct = default)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        var name = Required(request.Name, 120, "Application name");
        if (request.Scopes == EmbeddedFinanceScope.None || (request.Scopes & ~EmbeddedFinanceScope.All) != 0) throw new InvalidOperationException("At least one valid Embedded Finance scope is required.");
        if (await _db.ApiApplications.AnyAsync(x => x.BusinessProfileId == access.BusinessProfileId && x.Name == name && !x.IsDeleted, ct)) throw new InvalidOperationException("An API application with this name already exists.");
        var ranges = NormalizeRanges(request.AllowedIpRanges);
        var entity = new ApiApplication { BusinessProfileId = access.BusinessProfileId, Name = name, Description = Optional(request.Description, 1000), Scopes = request.Scopes, Status = ApiApplicationStatus.Active, AllowedIpRanges = ranges.Count == 0 ? null : string.Join(',', ranges), CreatedByUserId = userId };
        _db.ApiApplications.Add(entity); await _db.SaveChangesAsync(ct); return ToDto(entity);
    }

    public async Task<IReadOnlyList<ApiCredentialDto>> GetCredentialsAsync(Guid userId, Guid apiApplicationId, CancellationToken ct = default)
    {
        var app = await OwnedApplication(userId, apiApplicationId, ct);
        var now = DateTime.UtcNow;

        var rows = await _db.ApiCredentials
            .AsNoTracking()
            .Where(x => x.ApiApplicationId == app.Id && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows
            .Select(x => new ApiCredentialDto(
                x.Id,
                x.ApiApplicationId,
                x.Name,
                x.KeyId,
                x.SecretLastFour,
                EffectiveCredentialStatus(x.Status, x.ExpiresAt, now),
                x.ExpiresAt,
                x.LastUsedAt,
                x.LastUsedIpAddress,
                x.CreatedAt))
            .ToList();
    }

    public async Task<ApiCredentialCreatedDto> CreateCredentialAsync(Guid userId, Guid apiApplicationId, CreateApiCredentialRequestDto request, CancellationToken ct = default)
    {
        var app = await OwnedApplication(userId, apiApplicationId, ct);
        if (app.Status != ApiApplicationStatus.Active) throw new InvalidOperationException("API application must be active before credentials can be created.");
        if (request.ExpiresAt.HasValue && request.ExpiresAt <= DateTime.UtcNow) throw new InvalidOperationException("Credential expiry must be in the future.");
        var generated = EmbeddedApiKey.Generate();
        var entity = new ApiCredential { ApiApplicationId = app.Id, Name = Required(request.Name, 120, "Credential name"), KeyId = generated.KeyId, SecretHash = generated.SecretHash, SecretLastFour = generated.SecretLastFour, Status = ApiCredentialStatus.Active, ExpiresAt = request.ExpiresAt, CreatedByUserId = userId };
        _db.ApiCredentials.Add(entity); await _db.SaveChangesAsync(ct);
        return new ApiCredentialCreatedDto(entity.Id, entity.ApiApplicationId, entity.Name, entity.KeyId, generated.ApiKey, entity.SecretLastFour, entity.Status, entity.ExpiresAt, entity.CreatedAt);
    }

    public async Task RevokeCredentialAsync(Guid userId, Guid apiApplicationId, Guid credentialId, CancellationToken ct = default)
    {
        var app = await OwnedApplication(userId, apiApplicationId, ct);
        var entity = await _db.ApiCredentials.FirstOrDefaultAsync(x => x.Id == credentialId && x.ApiApplicationId == app.Id && !x.IsDeleted, ct) ?? throw new InvalidOperationException("API credential not found.");
        if (entity.Status == ApiCredentialStatus.Revoked) return;
        entity.Status = ApiCredentialStatus.Revoked; entity.LastUpdatedAt = DateTime.UtcNow; entity.LastUpdatedByUserId = userId; await _db.SaveChangesAsync(ct);
    }

    private async Task<ApiApplication> OwnedApplication(Guid userId, Guid id, CancellationToken ct)
    {
        var access = await _access.EnsurePermissionAsync(userId, BusinessPermission.ManageApiAccess, ct);
        return await _db.ApiApplications.FirstOrDefaultAsync(x => x.Id == id && x.BusinessProfileId == access.BusinessProfileId && !x.IsDeleted, ct) ?? throw new InvalidOperationException("API application not found.");
    }

    private static ApiCredentialStatus EffectiveCredentialStatus(
        ApiCredentialStatus status,
        DateTime? expiresAt,
        DateTime now) =>
        status == ApiCredentialStatus.Active &&
        expiresAt.HasValue &&
        expiresAt.Value <= now
            ? ApiCredentialStatus.Expired
            : status;

    private static ApiApplicationDto ToDto(ApiApplication x) => new(x.Id, x.Name, x.Description, x.Scopes, x.Status, ParseRanges(x.AllowedIpRanges), x.CreatedAt, x.LastAuthenticatedAt);
    private static IReadOnlyList<string> ParseRanges(string? x) => string.IsNullOrWhiteSpace(x) ? Array.Empty<string>() : x.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static IReadOnlyList<string> NormalizeRanges(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0) return Array.Empty<string>();
        var result = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var value in result)
        {
            var parts = value.Split('/');
            var valid = parts.Length == 1 ? System.Net.IPAddress.TryParse(parts[0], out _) : parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var address) && int.TryParse(parts[1], out var prefix) && prefix >= 0 && prefix <= (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
            if (!valid) throw new InvalidOperationException($"Invalid allowed IP address or CIDR range: {value}");
        }
        return result;
    }
    private static string Required(string value, int max, string label) { var x = (value ?? "").Trim(); if (x.Length == 0) throw new InvalidOperationException($"{label} is required."); if (x.Length > max) throw new InvalidOperationException($"{label} cannot exceed {max} characters."); return x; }
    private static string? Optional(string? value, int max) { if (string.IsNullOrWhiteSpace(value)) return null; var x = value.Trim(); if (x.Length > max) throw new InvalidOperationException($"Value cannot exceed {max} characters."); return x; }
}
