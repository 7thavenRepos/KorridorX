using System.Net;
using System.Net.Sockets;

namespace KorridorX.Services.EmbeddedFinance;

public sealed class EmbeddedWebhookUrlSecurityValidator : IEmbeddedWebhookUrlSecurityValidator
{
    public async Task<string> ValidateAsync(string value, CancellationToken ct = default)
    {
        var cleaned = (value ?? "").Trim();
        if (cleaned.Length == 0 || cleaned.Length > 1000)
            throw new InvalidOperationException("Webhook URL is invalid.");
        if (!Uri.TryCreate(cleaned, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Webhook URL must be an absolute HTTPS URL.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Webhook URL cannot contain embedded credentials.");

        var host = uri.Host.Trim().TrimEnd('.');
        if (host.Length == 0)
            throw new InvalidOperationException("Webhook URL host is required.");
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".home", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Webhook URL cannot target a local or internal host.");

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literal)) addresses = [literal];
        else
        {
            try { addresses = await Dns.GetHostAddressesAsync(host, ct); }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                throw new InvalidOperationException("Webhook URL host could not be resolved.", ex);
            }
        }

        if (addresses.Length == 0 || addresses.Any(IsBlockedAddress))
            throw new InvalidOperationException("Webhook URL resolves to a restricted address.");

        return uri.ToString();
    }

    private static bool IsBlockedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            if (b[0] == 0 || b[0] == 10 || b[0] == 127 || b[0] >= 224) return true;
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
            if (b[0] == 169 && b[1] == 254) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            if (b[0] == 198 && (b[1] == 18 || b[1] == 19)) return true;
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal) return true;
            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return true;
            if (b[0] == 0xFE && (b[1] & 0xC0) == 0x80) return true;
            return false;
        }
        return true;
    }
}
