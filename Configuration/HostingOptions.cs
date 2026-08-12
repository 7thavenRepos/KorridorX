using Microsoft.Extensions.Options;
using System.Net;

namespace KorridorX.Configuration;

public sealed class HostingOptions
{
    public const string SectionName = "Hosting";

    public string ApplicationName { get; set; } = "KorridorX";
    public string[] AllowedOrigins { get; set; } = ["http://localhost:4200"];
    public bool RequireHttpsRedirection { get; set; } = true;
    public bool SwaggerEnabled { get; set; }
    public bool JsonConsoleLogging { get; set; } = true;
    public int SlowRequestThresholdMilliseconds { get; set; } = 1000;
    public string CorrelationHeaderName { get; set; } = "X-Correlation-ID";
    public string[] TrustedProxies { get; set; } = [];
    public int ForwardLimit { get; set; } = 1;
    public string DataProtectionKeysPath { get; set; } = "";
}

public sealed class HostingOptionsValidator : IValidateOptions<HostingOptions>
{
    private readonly IHostEnvironment _environment;

    public HostingOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, HostingOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApplicationName))
            errors.Add("Hosting:ApplicationName is required.");

        if (options.AllowedOrigins.Length == 0)
            errors.Add("Hosting:AllowedOrigins must contain at least one frontend origin.");

        foreach (var origin in options.AllowedOrigins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add($"Hosting:AllowedOrigins contains an invalid origin '{origin}'.");
                continue;
            }

            if (!_environment.IsDevelopment() &&
                !_environment.IsEnvironment("Testing") &&
                (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Hosting:AllowedOrigins cannot contain localhost or loopback addresses outside Development.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.CorrelationHeaderName) || options.CorrelationHeaderName.Length > 100)
            errors.Add("Hosting:CorrelationHeaderName must be between 1 and 100 characters.");

        if (options.SlowRequestThresholdMilliseconds is < 100 or > 300_000)
            errors.Add("Hosting:SlowRequestThresholdMilliseconds must be between 100 and 300000.");

        if (options.ForwardLimit is < 1 or > 10)
            errors.Add("Hosting:ForwardLimit must be between 1 and 10.");

        foreach (var trustedProxy in options.TrustedProxies)
        {
            if (!IPAddress.TryParse(trustedProxy, out _))
                errors.Add($"Hosting:TrustedProxies contains an invalid IP address '{trustedProxy}'.");
        }

        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            if (!options.RequireHttpsRedirection)
                errors.Add("Hosting:RequireHttpsRedirection must be true outside Development.");

            if (options.SwaggerEnabled)
                errors.Add("Hosting:SwaggerEnabled must be false outside Development.");

            if (string.IsNullOrWhiteSpace(options.DataProtectionKeysPath))
                errors.Add("Hosting:DataProtectionKeysPath is required outside Development.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }
}
