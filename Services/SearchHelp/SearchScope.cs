using System.Security.Claims;
using KorridorX.Exceptions;
using KorridorX.Models.Enums;
using KorridorX.Services.BusinessContext;
using KorridorX.Services.BusinessTransfers;
namespace KorridorX.Services.SearchHelp;
public sealed record SearchScope(string Portal, Guid UserId, IReadOnlySet<string> Roles, BusinessAccessContext? Business)
{
    public bool IsAdmin => Portal == "admin";
    public Guid? BusinessId => Business?.BusinessProfileId;
    public bool HasRole(params string[] roles) => roles.Any(Roles.Contains);
    public bool Can(BusinessPermission permission) => !IsAdmin && Business?.HasPermission(permission) == true;
}
public sealed class SearchScopeResolver(IBusinessAccessService businesses, IBusinessContextAccessor selected)
{
    public static readonly string[] AdminRoles = ["Compliance", "InternalAudit", "Support", "Operations", "Admin", "SuperAdmin"];
    public async Task<SearchScope> ResolveAsync(ClaimsPrincipal principal, string portal, CancellationToken ct)
    {
        if (principal.Identity?.IsAuthenticated != true || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            throw new UnauthorizedAccessException("A current authenticated account is required.");
        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        if (portal == "admin")
        {
            if (!AdminRoles.Any(roles.Contains)) throw new ForbiddenException("This account cannot access the administration workspace.");
            return new SearchScope(portal, userId, roles, null);
        }
        if (portal != "business" || !new[] { "Business", "BusinessAdmin" }.Any(roles.Contains))
            throw new ForbiddenException("This account cannot access the requested workspace.");
        // Explicit selections, revoked memberships and ambiguous contexts are never ignored.
        if (selected.GetSelectedBusinessProfileId().HasValue)
            return new SearchScope(portal, userId, roles, await businesses.GetAccessAsync(userId, ct));
        var available = await businesses.GetAvailableBusinessesAsync(userId, ct);
        if (available.Count == 0) return new SearchScope(portal, userId, roles, null);
        return new SearchScope(portal, userId, roles, await businesses.GetAccessAsync(userId, ct));
    }
}
