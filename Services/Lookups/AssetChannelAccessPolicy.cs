using System.Security.Claims;

namespace KorridorX.Services.Lookups;

public enum AssetLookupChannel
{
    Individual,
    Business,
    AdminAll
}

public static class AssetChannelAccessPolicy
{
    // The legacy Flutter app does not send channel. Default MUST always remain
    // Individual, even when a person has both Consumer and Business roles.
    public static AssetLookupChannel ResolveChannel(string? channel, ClaimsPrincipal user)
    {
        var requested = (channel ?? "individual").Trim().ToLowerInvariant();
        if (requested == "individual")
            return AssetLookupChannel.Individual;
        var admin = user.IsInRole("Admin") || user.IsInRole("SuperAdmin");
        if (requested == "business")
        {
            if (!admin && !user.IsInRole("Business") && !user.IsInRole("BusinessAdmin"))
                throw new UnauthorizedAccessException("Business asset visibility requires a business session.");
            return AssetLookupChannel.Business;
        }
        if (requested == "admin")
        {
            if (!admin)
                throw new UnauthorizedAccessException("Admin asset visibility requires an administrator session.");
            return AssetLookupChannel.AdminAll;
        }
        throw new ArgumentException("Channel must be individual, business, or admin.");
    }
}
