using System.Security.Cryptography;
using System.Text;
using KorridorX.Data;
using KorridorX.Dtos.Notifications;
using KorridorX.Models.Notifications;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Services.Notifications;

public sealed class MobilePushDeviceService
    : IMobilePushDeviceService
{
    private readonly AppDbContext _db;

    public MobilePushDeviceService(
        AppDbContext db)
    {
        _db = db;
    }

    public async Task<MobilePushDeviceDto> RegisterAsync(
        Guid userId,
        RegisterMobilePushDeviceRequestDto request,
        CancellationToken ct = default)
    {
        var platform = NormalizePlatform(request.Platform);

        var token = request.PushToken.Trim();

        var fingerprint =
            request.DeviceFingerprint.Trim();

        var deviceName =
            request.DeviceName.Trim();

        if (token.Length == 0)
        {
            throw new InvalidOperationException(
                "The push token is required.");
        }

        if (fingerprint.Length == 0)
        {
            throw new InvalidOperationException(
                "The device fingerprint is required.");
        }

        if (deviceName.Length == 0)
        {
            throw new InvalidOperationException(
                "The device name is required.");
        }

        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;

        var tokenMatch =
            await _db.MobilePushDevices
                .FirstOrDefaultAsync(
                    x =>
                        !x.IsDeleted &&
                        x.PushTokenHash == tokenHash &&
                        x.PushToken == token,
                    ct);

        var installationMatches =
            await _db.MobilePushDevices
                .Where(
                    x =>
                        !x.IsDeleted &&
                        x.UserId == userId &&
                        x.Platform == platform &&
                        x.DeviceFingerprint ==
                            fingerprint)
                .OrderByDescending(
                    x => x.LastSeenAt)
                .ToListAsync(ct);

        MobilePushDevice device;

        if (tokenMatch is not null)
        {
            device = tokenMatch;
        }
        else if (installationMatches.Count > 0)
        {
            device = installationMatches[0];
        }
        else
        {
            device = new MobilePushDevice
            {
                UserId = userId,
                Platform = platform,
                PushToken = token,
                PushTokenHash = tokenHash,
                DeviceFingerprint = fingerprint,
                DeviceName = deviceName,
                CreatedByUserId = userId
            };

            _db.MobilePushDevices.Add(device);
        }

        device.UserId = userId;
        device.Platform = platform;
        device.PushToken = token;
        device.PushTokenHash = tokenHash;
        device.DeviceFingerprint = fingerprint;
        device.DeviceName = deviceName;
        device.IsActive = true;
        device.DisabledAt = null;
        device.LastSeenAt = now;
        device.LastUpdatedAt = now;
        device.LastUpdatedByUserId = userId;

        var duplicates =
            await _db.MobilePushDevices
                .Where(
                    x =>
                        !x.IsDeleted &&
                        x.Id != device.Id &&
                        x.IsActive &&
                        (
                            (
                                x.PushTokenHash ==
                                    tokenHash &&
                                x.PushToken == token
                            ) ||
                            (
                                x.UserId == userId &&
                                x.Platform == platform &&
                                x.DeviceFingerprint ==
                                    fingerprint
                            )
                        ))
                .ToListAsync(ct);

        foreach (var duplicate in duplicates)
        {
            duplicate.IsActive = false;
            duplicate.DisabledAt = now;
            duplicate.LastUpdatedAt = now;
            duplicate.LastUpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct);

        return ToDto(device);
    }

    public async Task<UnregisterMobilePushDeviceResultDto>
        UnregisterAsync(
            Guid userId,
            UnregisterMobilePushDeviceRequestDto request,
            CancellationToken ct = default)
    {
        var token = request.PushToken.Trim();

        if (token.Length == 0)
        {
            throw new InvalidOperationException(
                "The push token is required.");
        }

        var tokenHash = HashToken(token);

        var devices =
            await _db.MobilePushDevices
                .Where(
                    x =>
                        !x.IsDeleted &&
                        x.UserId == userId &&
                        x.PushTokenHash ==
                            tokenHash &&
                        x.PushToken == token &&
                        x.IsActive)
                .ToListAsync(ct);

        if (devices.Count == 0)
        {
            return new
                UnregisterMobilePushDeviceResultDto(
                    false);
        }

        var now = DateTime.UtcNow;

        foreach (var device in devices)
        {
            device.IsActive = false;
            device.DisabledAt = now;
            device.LastUpdatedAt = now;
            device.LastUpdatedByUserId = userId;
        }

        await _db.SaveChangesAsync(ct);

        return new
            UnregisterMobilePushDeviceResultDto(
                true);
    }

    private static string NormalizePlatform(
        string value)
    {
        var cleaned = value.Trim();

        if (string.Equals(
            cleaned,
            "android",
            StringComparison.OrdinalIgnoreCase))
        {
            return MobilePushPlatforms.Android;
        }

        if (string.Equals(
            cleaned,
            "ios",
            StringComparison.OrdinalIgnoreCase))
        {
            return MobilePushPlatforms.Ios;
        }

        throw new InvalidOperationException(
            "Push notifications are supported only on Android and iOS.");
    }

    private static string HashToken(
        string token)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(bytes);
    }

    private static MobilePushDeviceDto ToDto(
        MobilePushDevice device) =>
        new(
            device.Id,
            device.Platform,
            device.DeviceName,
            device.IsActive,
            device.LastSeenAt);
}

public static class MobilePushPlatforms
{
    public const string Android = "Android";

    public const string Ios = "iOS";
}
