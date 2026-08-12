using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class IdentitySeedOptionsValidator : IValidateOptions<IdentitySeedOptions>
{
    private readonly IHostEnvironment _environment;

    public IdentitySeedOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, IdentitySeedOptions options)
    {
        if (_environment.IsEnvironment("Testing"))
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (!_environment.IsProduction() && options.SeedTestUsers)
        {
            // The Super Admin is provisioned separately, but it is part of the
            // complete eight-role non-production login matrix.
            ValidateRequiredUser(
                options.SuperAdmin,
                $"{IdentitySeedOptions.SectionName}:SuperAdmin",
                requireCountry: false,
                failures);

            ValidateRequiredUser(
                options.TestUsers.Consumer,
                $"{IdentitySeedOptions.SectionName}:TestUsers:Consumer",
                requireCountry: true,
                failures);
            ValidateRequiredUser(
                options.TestUsers.BusinessOwner,
                $"{IdentitySeedOptions.SectionName}:TestUsers:BusinessOwner",
                requireCountry: true,
                failures);
            ValidateRequiredUser(
                options.TestUsers.BusinessAdministrator,
                $"{IdentitySeedOptions.SectionName}:TestUsers:BusinessAdministrator",
                requireCountry: true,
                failures);
            ValidateRequiredUser(
                options.TestUsers.ComplianceOfficer,
                $"{IdentitySeedOptions.SectionName}:TestUsers:ComplianceOfficer",
                requireCountry: false,
                failures);
            ValidateRequiredUser(
                options.TestUsers.SupportOfficer,
                $"{IdentitySeedOptions.SectionName}:TestUsers:SupportOfficer",
                requireCountry: false,
                failures);
            ValidateRequiredUser(
                options.TestUsers.OperationsOfficer,
                $"{IdentitySeedOptions.SectionName}:TestUsers:OperationsOfficer",
                requireCountry: false,
                failures);
            ValidateRequiredUser(
                options.TestUsers.Administrator,
                $"{IdentitySeedOptions.SectionName}:TestUsers:Administrator",
                requireCountry: false,
                failures);

            ValidateDistinctTestEmails(options, failures);

            if (string.IsNullOrWhiteSpace(options.Business.Name))
            {
                failures.Add(
                    $"{IdentitySeedOptions.SectionName}:Business:Name is required when test-user seeding is enabled.");
            }
            else if (options.Business.Name.Trim().Length > 200)
            {
                failures.Add(
                    $"{IdentitySeedOptions.SectionName}:Business:Name must not exceed 200 characters.");
            }

            ValidateCountryCode(
                options.Business.CountryCode,
                $"{IdentitySeedOptions.SectionName}:Business:CountryCode",
                required: true,
                failures);
        }
        else
        {
            ValidateOptionalUser(
                options.SuperAdmin,
                $"{IdentitySeedOptions.SectionName}:SuperAdmin",
                requireCountry: false,
                failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateOptionalUser(
        SeedUserOptions user,
        string path,
        bool requireCountry,
        ICollection<string> failures)
    {
        if (!user.HasAnyConfiguredValue())
            return;

        ValidateRequiredUser(user, path, requireCountry, failures);
    }

    private static void ValidateRequiredUser(
        SeedUserOptions user,
        string path,
        bool requireCountry,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(user.FirstName))
            failures.Add($"{path}:FirstName is required.");
        else if (user.FirstName.Trim().Length > 100)
            failures.Add($"{path}:FirstName must not exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(user.LastName))
            failures.Add($"{path}:LastName is required.");
        else if (user.LastName.Trim().Length > 100)
            failures.Add($"{path}:LastName must not exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            failures.Add($"{path}:Email is required.");
        }
        else if (!new EmailAddressAttribute().IsValid(user.Email.Trim()))
        {
            failures.Add($"{path}:Email must be a valid email address.");
        }
        else if (user.Email.Trim().Length > 256)
        {
            failures.Add($"{path}:Email must not exceed 256 characters.");
        }

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber) && user.PhoneNumber.Trim().Length > 50)
            failures.Add($"{path}:PhoneNumber must not exceed 50 characters.");

        ValidatePassword(user.Password, $"{path}:Password", failures);
        ValidateCountryCode(user.CountryCode, $"{path}:CountryCode", requireCountry, failures);
    }

    private static void ValidatePassword(
        string password,
        string path,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            failures.Add($"{path} is required.");
            return;
        }

        if (password.Length < 8 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit))
        {
            failures.Add(
                $"{path} must be at least eight characters and contain an uppercase letter, a lowercase letter, and a number.");
        }
    }

    private static void ValidateCountryCode(
        string? countryCode,
        string path,
        bool required,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            if (required)
                failures.Add($"{path} is required.");

            return;
        }

        var trimmed = countryCode.Trim();
        if (trimmed.Length is < 2 or > 10 || !trimmed.All(char.IsLetter))
        {
            failures.Add($"{path} must contain between two and ten letters.");
        }
    }

    private static void ValidateDistinctTestEmails(
        IdentitySeedOptions options,
        ICollection<string> failures)
    {
        var users = new[]
        {
            options.SuperAdmin,
            options.TestUsers.Consumer,
            options.TestUsers.BusinessOwner,
            options.TestUsers.BusinessAdministrator,
            options.TestUsers.ComplianceOfficer,
            options.TestUsers.SupportOfficer,
            options.TestUsers.OperationsOfficer,
            options.TestUsers.Administrator
        };

        var duplicateExists = users
            .Where(x => !string.IsNullOrWhiteSpace(x.Email))
            .GroupBy(x => x.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .Any(x => x.Count() > 1);

        if (duplicateExists)
        {
            failures.Add(
                $"{IdentitySeedOptions.SectionName} requires a different email address for every configured login identity.");
        }
    }
}
