using KorridorX.Data.Seed;

namespace KorridorX.Services.Auth;

public static class MfaSecurityPolicy
{
    public const string AuthenticationMethodClaim = "amr";
    public const string PasswordAuthenticationMethod = "pwd";
    public const string MfaAuthenticationMethod = "mfa";

    private static readonly HashSet<string> PrivilegedRoles = new(
        new[]
        {
            IdentityRoleNames.Business,
            IdentityRoleNames.BusinessAdmin,
            IdentityRoleNames.Compliance,
            IdentityRoleNames.Support,
            IdentityRoleNames.Operations,
            IdentityRoleNames.Admin,
            IdentityRoleNames.SuperAdmin
        },
        StringComparer.OrdinalIgnoreCase);

    public static bool RequiresMfa(IEnumerable<string> roles) =>
        roles.Any(PrivilegedRoles.Contains);
}
