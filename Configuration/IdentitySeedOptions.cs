namespace KorridorX.Configuration;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public bool SeedTestUsers { get; set; }

    public SeedUserOptions SuperAdmin { get; set; } = new();

    public NonProductionTestUserOptions TestUsers { get; set; } = new();

    public SeedBusinessOptions Business { get; set; } = new();
}

public sealed class SeedUserOptions
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? CountryCode { get; set; }

    public bool HasAnyConfiguredValue() =>
        !string.IsNullOrWhiteSpace(FirstName) ||
        !string.IsNullOrWhiteSpace(LastName) ||
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(Password) ||
        !string.IsNullOrWhiteSpace(PhoneNumber) ||
        !string.IsNullOrWhiteSpace(CountryCode);

    public bool HasRequiredIdentityValues() =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);
}

public sealed class NonProductionTestUserOptions
{
    public SeedUserOptions Consumer { get; set; } = new();
    public SeedUserOptions BusinessOwner { get; set; } = new();
    public SeedUserOptions BusinessAdministrator { get; set; } = new();
    public SeedUserOptions ComplianceOfficer { get; set; } = new();
    public SeedUserOptions SupportOfficer { get; set; } = new();
    public SeedUserOptions OperationsOfficer { get; set; } = new();
    public SeedUserOptions Administrator { get; set; } = new();
}

public sealed class SeedBusinessOptions
{
    public string Name { get; set; } = "";
    public string CountryCode { get; set; } = "";
}
