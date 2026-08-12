using KorridorX.Configuration;
using KorridorX.Tests.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace KorridorX.Tests;

public sealed class IdentitySeedOptionsValidatorTests
{
    [Fact]
    public void Complete_development_test_identity_configuration_is_valid()
    {
        var options = CreateCompleteOptions();
        var validator = CreateValidator(Environments.Development);

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_development_test_users_require_the_complete_matrix()
    {
        var options = new IdentitySeedOptions
        {
            SeedTestUsers = true
        };
        var validator = CreateValidator(Environments.Development);

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Failures,
            x => x.Contains("TestUsers:Consumer:Email", StringComparison.Ordinal));
        Assert.Contains(
            result.Failures,
            x => x.Contains("Business:Name", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_does_not_validate_non_production_test_identity_values()
    {
        var options = new IdentitySeedOptions
        {
            SeedTestUsers = true
        };
        var validator = CreateValidator(Environments.Production);

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Partially_configured_super_admin_is_rejected_without_exposing_its_value()
    {
        const string sensitiveValue = "DoNotEchoThisPassword9";
        var options = new IdentitySeedOptions
        {
            SuperAdmin = new SeedUserOptions
            {
                Email = "superadmin@example.test",
                Password = sensitiveValue
            }
        };
        var validator = CreateValidator(Environments.Production);

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(sensitiveValue, string.Join(" ", result.Failures));
    }

    [Fact]
    public void Testing_environment_bypasses_application_startup_identity_configuration()
    {
        var options = new IdentitySeedOptions
        {
            SeedTestUsers = true,
            SuperAdmin = new SeedUserOptions { Email = "invalid" }
        };
        var validator = CreateValidator("Testing");

        var result = validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Enabled_test_identity_matrix_requires_distinct_email_addresses()
    {
        var options = CreateCompleteOptions();
        options.TestUsers.BusinessAdministrator.Email = options.TestUsers.BusinessOwner.Email.ToUpperInvariant();
        var validator = CreateValidator(Environments.Development);

        var result = validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Failures,
            x => x.Contains("different email address", StringComparison.Ordinal));
    }

    private static IdentitySeedOptionsValidator CreateValidator(string environmentName) =>
        new(new TestHostEnvironment { EnvironmentName = environmentName });

    internal static IdentitySeedOptions CreateCompleteOptions(string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N");

        return new IdentitySeedOptions
        {
            SeedTestUsers = true,
            SuperAdmin = User("Super", "Administrator", $"super-{suffix}@example.test", "US"),
            TestUsers = new NonProductionTestUserOptions
            {
                Consumer = User("Consumer", "Tester", $"consumer-{suffix}@example.test", "CA"),
                BusinessOwner = User("Business", "Owner", $"owner-{suffix}@example.test", "CA"),
                BusinessAdministrator = User("Business", "Administrator", $"business-admin-{suffix}@example.test", "CA"),
                ComplianceOfficer = User("Compliance", "Officer", $"compliance-{suffix}@example.test"),
                SupportOfficer = User("Support", "Officer", $"support-{suffix}@example.test"),
                OperationsOfficer = User("Operations", "Officer", $"operations-{suffix}@example.test"),
                Administrator = User("Platform", "Administrator", $"admin-{suffix}@example.test")
            },
            Business = new SeedBusinessOptions
            {
                Name = $"KorridorX Test Business {suffix}",
                CountryCode = "CA"
            }
        };
    }

    private static SeedUserOptions User(
        string firstName,
        string lastName,
        string email,
        string? countryCode = null) =>
        new()
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = "SeedTestPassword9",
            CountryCode = countryCode
        };
}
