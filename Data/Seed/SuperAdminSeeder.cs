using KorridorX.Configuration;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace KorridorX.Data.Seed;

public interface ISuperAdminSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

public sealed class SuperAdminSeeder : ISuperAdminSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISeedUserProvisioner _userProvisioner;
    private readonly IdentitySeedOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SuperAdminSeeder> _logger;

    public SuperAdminSeeder(
        UserManager<ApplicationUser> userManager,
        ISeedUserProvisioner userProvisioner,
        IOptions<IdentitySeedOptions> options,
        IHostEnvironment environment,
        ILogger<SuperAdminSeeder> logger)
    {
        _userManager = userManager;
        _userProvisioner = userProvisioner;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_environment.IsProduction())
        {
            var existingSuperAdmins = await _userManager.GetUsersInRoleAsync(
                IdentityRoleNames.SuperAdmin);

            if (existingSuperAdmins.Count > 0)
            {
                _logger.LogInformation(
                    "A Super Administrator already exists. Startup seeding will not create or modify another production account.");
                return;
            }

            if (!_options.SuperAdmin.HasRequiredIdentityValues())
            {
                throw new IdentitySeedException(
                    "Production has no Super Administrator. Configure the complete IdentitySeed:SuperAdmin bootstrap identity through the deployment secret store before startup.");
            }
        }
        else if (!_options.SuperAdmin.HasAnyConfiguredValue())
        {
            _logger.LogInformation(
                "Super Administrator seeding is not configured for {EnvironmentName}.",
                _environment.EnvironmentName);
            return;
        }

        await _userProvisioner.EnsureUserAsync(
            _options.SuperAdmin,
            UserType.Admin,
            [IdentityRoleNames.SuperAdmin],
            ct);

        _logger.LogInformation(
            "The configured Super Administrator identity is ready for {EnvironmentName}.",
            _environment.EnvironmentName);
    }
}
