namespace KorridorX.Data.Seed;

public static class IdentityRoleNames
{
    public const string Consumer = "Consumer";
    public const string Business = "Business";
    public const string BusinessAdmin = "BusinessAdmin";
    public const string Compliance = "Compliance";
    public const string InternalAudit = "InternalAudit";
    public const string Support = "Support";
    public const string Operations = "Operations";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        Consumer,
        Business,
        BusinessAdmin,
        Compliance,
        InternalAudit,
        Support,
        Operations,
        Admin,
        SuperAdmin
    });
}
