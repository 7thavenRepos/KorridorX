using KorridorX.Configuration;
using KorridorX.Tests.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace KorridorX.Tests;

public sealed class HostingOptionsValidatorTests
{
    [Fact]
    public void Staging_AllowsSwagger_WhenDeploymentHostingSettingsAreValid()
    {
        var validator = new HostingOptionsValidator(
            new TestHostEnvironment { EnvironmentName = Environments.Staging });

        var result = validator.Validate(
            null,
            CreateDeployedOptions(swaggerEnabled: true));

        Assert.True(result.Succeeded, string.Join("; ", result.Failures ?? []));
    }

    [Fact]
    public void Production_RejectsSwagger_WhenEnabled()
    {
        var validator = new HostingOptionsValidator(
            new TestHostEnvironment { EnvironmentName = Environments.Production });

        var result = validator.Validate(
            null,
            CreateDeployedOptions(swaggerEnabled: true));

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Failures ?? [],
            failure => failure.Contains(
                "Hosting:SwaggerEnabled must be false in Production.",
                StringComparison.Ordinal));
    }

    private static HostingOptions CreateDeployedOptions(bool swaggerEnabled) =>
        new()
        {
            ApplicationName = "KorridorX",
            AllowedOrigins = ["https://app.staging.korridorx.com"],
            RequireHttpsRedirection = true,
            SwaggerEnabled = swaggerEnabled,
            JsonConsoleLogging = true,
            DataProtectionKeysPath = "/var/lib/korridorx/keys"
        };
}
