using System.Net;
using KorridorX.Data;
using KorridorX.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedFinanceCredentialAuthenticator : IEmbeddedFinanceCredentialAuthenticator
{
    private readonly AppDbContext _db;
    public EmbeddedFinanceCredentialAuthenticator(AppDbContext db) => _db = db;

    public async Task<EmbeddedFinancePrincipal?> AuthenticateAsync(string apiKey, string? remoteIpAddress, CancellationToken ct = default)
    {
        if (!EmbeddedApiKey.TryParse(apiKey, out var keyId, out var secret)) return null;
        var credential = await _db.ApiCredentials.Include(x => x.ApiApplication).FirstOrDefaultAsync(x => x.KeyId == keyId && !x.IsDeleted && !x.ApiApplication.IsDeleted, ct);
        if (credential is null || credential.Status != ApiCredentialStatus.Active || credential.ApiApplication.Status != ApiApplicationStatus.Active) return null;
        var now = DateTime.UtcNow;
        if (credential.ExpiresAt.HasValue && credential.ExpiresAt <= now)
        {
            credential.Status = ApiCredentialStatus.Expired;
            credential.LastUpdatedAt = now;
            await _db.SaveChangesAsync(ct);
            return null;
        }
        if (!EmbeddedApiKey.Verify(secret, credential.SecretHash) || !IsIpAllowed(remoteIpAddress, credential.ApiApplication.AllowedIpRanges)) return null;
        credential.LastUsedAt = now;
        credential.LastUsedIpAddress = remoteIpAddress;
        credential.LastUpdatedAt = now;
        credential.ApiApplication.LastAuthenticatedAt = now;
        credential.ApiApplication.LastUpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new EmbeddedFinancePrincipal(credential.ApiApplicationId, credential.Id, credential.ApiApplication.BusinessProfileId, credential.ApiApplication.Scopes, credential.KeyId);
    }

    private static bool IsIpAllowed(string? remoteIpAddress, string? configuredRanges)
    {
        if (string.IsNullOrWhiteSpace(configuredRanges)) return true;
        if (!IPAddress.TryParse(remoteIpAddress, out var remote)) return false;
        foreach (var value in configuredRanges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(value, out var exact) && exact.Equals(remote)) return true;
            if (TryMatchCidr(remote, value)) return true;
        }
        return false;
    }

    private static bool TryMatchCidr(IPAddress remote, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefixLength)) return false;
        var wasIpv4 = network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        if (wasIpv4) prefixLength += 96;
        if (prefixLength < 0 || prefixLength > 128) return false;
        var remoteBytes = remote.MapToIPv6().GetAddressBytes();
        var networkBytes = network.MapToIPv6().GetAddressBytes();
        var fullBytes = prefixLength / 8; var remainingBits = prefixLength % 8;
        for (var i = 0; i < fullBytes; i++) if (remoteBytes[i] != networkBytes[i]) return false;
        if (remainingBits == 0) return true;
        var mask = (byte)(0xFF << (8 - remainingBits));
        return (remoteBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }
}
