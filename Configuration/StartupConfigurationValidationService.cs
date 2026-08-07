using Microsoft.Extensions.Options;

namespace KorridorX.Configuration;

public sealed class StartupConfigurationValidationService : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly IOptions<HostingOptions> _hostingOptions;
    private readonly IOptions<ComplianceScreeningOptions> _screeningOptions;
    private readonly ILogger<StartupConfigurationValidationService> _logger;

    public StartupConfigurationValidationService(
        IConfiguration configuration,
        IHostEnvironment environment,
        IOptions<JwtOptions> jwtOptions,
        IOptions<HostingOptions> hostingOptions,
        IOptions<ComplianceScreeningOptions> screeningOptions,
        ILogger<StartupConfigurationValidationService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _jwtOptions = jwtOptions;
        _hostingOptions = hostingOptions;
        _screeningOptions = screeningOptions;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var jwt = _jwtOptions.Value;
        var hosting = _hostingOptions.Value;
        var screening = _screeningOptions.Value;

        if (string.IsNullOrWhiteSpace(connectionString))
            errors.Add("ConnectionStrings:DefaultConnection is required.");

        if (string.IsNullOrWhiteSpace(jwt.Issuer))
            errors.Add("Jwt:Issuer is required.");
        if (string.IsNullOrWhiteSpace(jwt.Audience))
            errors.Add("Jwt:Audience is required.");
        if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
            errors.Add("Jwt:Key must contain at least 32 characters.");

        if (_environment.IsProduction())
        {
            var allowedHosts = _configuration["AllowedHosts"];
            if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == "*")
                errors.Add("AllowedHosts must be explicitly configured in Production.");

            if (LooksLikePlaceholder(jwt.Key))
                errors.Add("Jwt:Key appears to be a placeholder and cannot be used in Production.");

            if (hosting.AllowedOrigins.Any(LooksLikePlaceholder))
                errors.Add("Hosting:AllowedOrigins contains a placeholder value.");

            if (screening.IsEnabled &&
                string.Equals(screening.ProviderCode, "ConfiguredWatchlist", StringComparison.OrdinalIgnoreCase) &&
                screening.Entries.Count == 0)
            {
                errors.Add(
                    "ComplianceScreening is enabled with ConfiguredWatchlist but no entries are configured. " +
                    "Configure a watchlist or replace ISanctionsScreeningProvider with a production provider.");
            }
        }

        if (errors.Count > 0)
        {
            throw new OptionsValidationException(
                "ApplicationStartup",
                typeof(StartupConfigurationValidationService),
                errors);
        }

        _logger.LogInformation(
            "Startup configuration validation succeeded for environment {EnvironmentName}.",
            _environment.EnvironmentName);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool LooksLikePlaceholder(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Contains("change-me", StringComparison.Ordinal) ||
               normalized.Contains("changeme", StringComparison.Ordinal) ||
               normalized.Contains("replace-me", StringComparison.Ordinal) ||
               normalized.Contains("example", StringComparison.Ordinal) ||
               normalized.Contains("placeholder", StringComparison.Ordinal);
    }
}
