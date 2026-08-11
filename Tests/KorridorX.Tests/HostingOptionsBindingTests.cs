using KorridorX.Configuration;
using Microsoft.Extensions.Configuration;

namespace KorridorX.Tests;

public sealed class HostingOptionsBindingTests
{
    [Fact]
    public void AllowedOrigins_UsesConfiguredOriginsWithoutRetainingLocalhostDefault()
    {
        const string stagingOrigin = "https://staging.korridorx.com";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{HostingOptions.SectionName}:AllowedOrigins:0"] = stagingOrigin
            })
            .Build();

        var options = new HostingOptions();

        configuration
            .GetSection(HostingOptions.SectionName)
            .Bind(options);

        Assert.Equal([stagingOrigin], options.AllowedOrigins);
        Assert.DoesNotContain(
            options.AllowedOrigins,
            origin => origin.Contains("localhost", StringComparison.OrdinalIgnoreCase));
    }
}