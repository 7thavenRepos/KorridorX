using KorridorX.Configuration;
using Microsoft.Extensions.Options;

namespace KorridorX.Data.Seed;

public sealed class IdentitySeedRunner
{
    private readonly IRoleSeeder _roleSeeder;
    private readonly ISuperAdminSeeder _superAdminSeeder;
    private readonly INonProductionTestUserSeeder _testUserSeeder;
    private readonly IdentitySeedOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<IdentitySeedRunner> _logger;

    public IdentitySeedRunner(
        IRoleSeeder roleSeeder,
        ISuperAdminSeeder superAdminSeeder,
        INonProductionTestUserSeeder testUserSeeder,
        IOptions<IdentitySeedOptions> options,
        IHostEnvironment environment,
        ILogger<IdentitySeedRunner> logger)
    {
        _roleSeeder = roleSeeder;
        _superAdminSeeder = superAdminSeeder;
        _testUserSeeder = testUserSeeder;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (_environment.IsEnvironment("Testing"))
            return;

        await _roleSeeder.SeedAsync(ct);
        await _superAdminSeeder.SeedAsync(ct);

        if (_environment.IsProduction())
        {
            if (_options.SeedTestUsers)
            {
                _logger.LogWarning(
                    "IdentitySeed:SeedTestUsers is enabled in Production. The setting was ignored and no test identity was created.");
            }

            return;
        }

        if (_options.SeedTestUsers)
            await _testUserSeeder.SeedAsync(ct);
    }
}
