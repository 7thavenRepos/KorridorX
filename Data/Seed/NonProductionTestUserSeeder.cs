using KorridorX.Configuration;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Data.Seed;

public interface INonProductionTestUserSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

public sealed class NonProductionTestUserSeeder : INonProductionTestUserSeeder
{
    private readonly AppDbContext _db;
    private readonly ISeedUserProvisioner _userProvisioner;
    private readonly IdentitySeedOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<NonProductionTestUserSeeder> _logger;

    public NonProductionTestUserSeeder(
        AppDbContext db,
        ISeedUserProvisioner userProvisioner,
        IOptions<IdentitySeedOptions> options,
        IHostEnvironment environment,
        ILogger<NonProductionTestUserSeeder> logger)
    {
        _db = db;
        _userProvisioner = userProvisioner;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // This is a second, local guard in addition to IdentitySeedRunner. It
        // makes this seeder safe even if another caller resolves it directly.
        if (_environment.IsProduction())
        {
            _logger.LogWarning(
                "Non-production identity seeding was requested in Production and was blocked.");
            return;
        }

        if (_environment.IsEnvironment("Testing") || !_options.SeedTestUsers)
            return;

        var testUsers = _options.TestUsers;

        var consumer = await _userProvisioner.EnsureUserAsync(
            testUsers.Consumer,
            UserType.Consumer,
            [IdentityRoleNames.Consumer],
            ct);

        var businessOwner = await _userProvisioner.EnsureUserAsync(
            testUsers.BusinessOwner,
            UserType.Business,
            [IdentityRoleNames.Business],
            ct);

        var businessAdministrator = await _userProvisioner.EnsureUserAsync(
            testUsers.BusinessAdministrator,
            UserType.Business,
            [IdentityRoleNames.Business, IdentityRoleNames.BusinessAdmin],
            ct);

        await _userProvisioner.EnsureUserAsync(
            testUsers.ComplianceOfficer,
            UserType.Admin,
            [IdentityRoleNames.Compliance],
            ct);

        await _userProvisioner.EnsureUserAsync(
            testUsers.SupportOfficer,
            UserType.Admin,
            [IdentityRoleNames.Support],
            ct);

        await _userProvisioner.EnsureUserAsync(
            testUsers.OperationsOfficer,
            UserType.Admin,
            [IdentityRoleNames.Operations],
            ct);

        await _userProvisioner.EnsureUserAsync(
            testUsers.Administrator,
            UserType.Admin,
            [IdentityRoleNames.Admin],
            ct);

        await EnsureConsumerProfileAsync(consumer, testUsers.Consumer, ct);
        await EnsureBusinessFixtureAsync(
            businessOwner,
            businessAdministrator,
            _options.Business,
            ct);

        _logger.LogInformation(
            "The configured non-production login identities and business membership fixture are ready for {EnvironmentName}.",
            _environment.EnvironmentName);
    }

    private async Task EnsureConsumerProfileAsync(
        ApplicationUser consumer,
        SeedUserOptions options,
        CancellationToken ct)
    {
        var exists = await _db.CustomerProfiles.AnyAsync(x => x.UserId == consumer.Id, ct);
        if (exists)
            return;

        _db.CustomerProfiles.Add(new CustomerProfile
        {
            UserId = consumer.Id,
            CustomerType = CustomerType.Individual,
            FirstName = consumer.FirstName,
            LastName = consumer.LastName,
            PhoneNumber = consumer.PhoneNumber,
            Email = consumer.Email,
            CountryCode = options.CountryCode!.Trim().ToUpperInvariant(),
            CreatedByUserId = consumer.Id
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureBusinessFixtureAsync(
        ApplicationUser owner,
        ApplicationUser administrator,
        SeedBusinessOptions options,
        CancellationToken ct)
    {
        var profile = await _db.BusinessProfiles
            .FirstOrDefaultAsync(x => x.OwnerUserId == owner.Id && !x.IsDeleted, ct);

        if (profile is null)
        {
            profile = new BusinessProfile
            {
                OwnerUserId = owner.Id,
                BusinessName = options.Name.Trim(),
                CountryCode = options.CountryCode.Trim().ToUpperInvariant(),
                ContactEmail = owner.Email,
                KybStatus = KybStatus.NotStarted,
                CreatedByUserId = owner.Id
            };
            _db.BusinessProfiles.Add(profile);
        }

        await EnsureBusinessMembershipAsync(
            profile,
            owner,
            BusinessUserRole.Owner,
            BusinessPermission.All,
            ct);

        await EnsureBusinessMembershipAsync(
            profile,
            administrator,
            BusinessUserRole.Admin,
            BusinessPermission.All,
            ct);

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureBusinessMembershipAsync(
        BusinessProfile profile,
        ApplicationUser user,
        BusinessUserRole role,
        BusinessPermission permissions,
        CancellationToken ct)
    {
        var membership = await _db.BusinessUsers.FirstOrDefaultAsync(
            x => x.BusinessProfileId == profile.Id && x.UserId == user.Id,
            ct);

        if (membership is null)
        {
            _db.BusinessUsers.Add(new BusinessUser
            {
                BusinessProfileId = profile.Id,
                UserId = user.Id,
                Role = role,
                Permissions = permissions,
                IsActive = true,
                CreatedByUserId = profile.OwnerUserId
            });
            return;
        }

        var changed = membership.IsDeleted ||
                      !membership.IsActive ||
                      membership.Role != role ||
                      membership.Permissions != permissions;

        if (!changed)
            return;

        membership.IsDeleted = false;
        membership.DeletedAt = null;
        membership.DeletedByUserId = null;
        membership.IsActive = true;
        membership.Role = role;
        membership.Permissions = permissions;
        membership.LastUpdatedAt = DateTime.UtcNow;
        membership.LastUpdatedByUserId = profile.OwnerUserId;
    }
}
