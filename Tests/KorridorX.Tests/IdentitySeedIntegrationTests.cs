using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Data.Seed;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class IdentitySeedIntegrationTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public IdentitySeedIntegrationTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Complete_non_production_identity_matrix_is_idempotent()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var seedOptions = IdentitySeedOptionsValidatorTests.CreateCompleteOptions(suffix);
        var options = Options.Create(seedOptions);
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development
        };

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleSeeder = scope.ServiceProvider.GetRequiredService<IRoleSeeder>();
        var provisioner = new SeedUserProvisioner(userManager);
        var superAdminSeeder = new SuperAdminSeeder(
            userManager,
            provisioner,
            options,
            environment,
            NullLogger<SuperAdminSeeder>.Instance);
        var testUserSeeder = new NonProductionTestUserSeeder(
            db,
            provisioner,
            options,
            environment,
            NullLogger<NonProductionTestUserSeeder>.Instance);

        await roleSeeder.SeedAsync();
        await superAdminSeeder.SeedAsync();
        await testUserSeeder.SeedAsync();

        await roleSeeder.SeedAsync();
        await superAdminSeeder.SeedAsync();
        await testUserSeeder.SeedAsync();

        foreach (var roleName in IdentityRoleNames.All)
        {
            Assert.Equal(
                1,
                await db.Roles.CountAsync(x => x.NormalizedName == roleName.ToUpperInvariant()));
        }

        await AssertUserAsync(
            userManager,
            seedOptions.SuperAdmin,
            UserType.Admin,
            IdentityRoleNames.SuperAdmin);
        await AssertUserAsync(
            userManager,
            seedOptions.TestUsers.Consumer,
            UserType.Consumer,
            IdentityRoleNames.Consumer);
        await AssertUserAsync(
            userManager,
            seedOptions.TestUsers.BusinessOwner,
            UserType.Business,
            IdentityRoleNames.Business);
        await AssertUserAsync(
            userManager,
            seedOptions.TestUsers.BusinessAdministrator,
            UserType.Business,
            IdentityRoleNames.Business,
            IdentityRoleNames.BusinessAdmin);
        await AssertUserAsync(
            userManager,
            seedOptions.TestUsers.ComplianceOfficer,
            UserType.Admin,
            IdentityRoleNames.Compliance);
        await AssertUserAsync(
            userManager,
            seedOptions.TestUsers.SupportOfficer,
            UserType.Admin,
            IdentityRoleNames.Support);
        await AssertUserAsync(
            userManager,
            seedOptions.TestUsers.OperationsOfficer,
            UserType.Admin,
            IdentityRoleNames.Operations);
        await AssertUserAsync(
            userManager,
            seedOptions.TestUsers.Administrator,
            UserType.Admin,
            IdentityRoleNames.Admin);

        var consumer = await userManager.FindByEmailAsync(seedOptions.TestUsers.Consumer.Email);
        var owner = await userManager.FindByEmailAsync(seedOptions.TestUsers.BusinessOwner.Email);
        var administrator = await userManager.FindByEmailAsync(
            seedOptions.TestUsers.BusinessAdministrator.Email);

        Assert.NotNull(consumer);
        Assert.NotNull(owner);
        Assert.NotNull(administrator);
        Assert.Equal(1, await db.CustomerProfiles.CountAsync(x => x.UserId == consumer!.Id));

        var profile = await db.BusinessProfiles.SingleAsync(x => x.OwnerUserId == owner!.Id);
        Assert.Equal(seedOptions.Business.Name, profile.BusinessName);
        Assert.Equal(2, await db.BusinessUsers.CountAsync(x => x.BusinessProfileId == profile.Id));

        var ownerMembership = await db.BusinessUsers.SingleAsync(x =>
            x.BusinessProfileId == profile.Id && x.UserId == owner.Id);
        Assert.Equal(BusinessUserRole.Owner, ownerMembership.Role);
        Assert.Equal(BusinessPermission.All, ownerMembership.Permissions);
        Assert.True(ownerMembership.IsActive);

        var administratorMembership = await db.BusinessUsers.SingleAsync(x =>
            x.BusinessProfileId == profile.Id && x.UserId == administrator!.Id);
        Assert.Equal(BusinessUserRole.Admin, administratorMembership.Role);
        Assert.Equal(BusinessPermission.All, administratorMembership.Permissions);
        Assert.True(administratorMembership.IsActive);
    }

    [DatabaseIntegrationFact]
    public async Task Existing_seed_user_is_reused_without_resetting_the_password()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var provisioner = new SeedUserProvisioner(userManager);
        var email = $"existing-seed-{Guid.NewGuid():N}@example.test";
        var existingPassword = "ExistingPassword9";
        var configuredPassword = "DifferentPassword9";
        var existing = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Existing",
            LastName = "Account",
            CountryCode = "CA",
            UserType = UserType.Consumer,
            Status = UserStatus.Active
        };

        var createResult = await userManager.CreateAsync(existing, existingPassword);
        Assert.True(createResult.Succeeded);

        var resolved = await provisioner.EnsureUserAsync(
            new SeedUserOptions
            {
                FirstName = "Configured",
                LastName = "Identity",
                Email = email.ToUpperInvariant(),
                Password = configuredPassword,
                CountryCode = "CA"
            },
            UserType.Consumer,
            [IdentityRoleNames.Consumer]);

        Assert.Equal(existing.Id, resolved.Id);
        Assert.True(await userManager.CheckPasswordAsync(resolved, existingPassword));
        Assert.False(await userManager.CheckPasswordAsync(resolved, configuredPassword));
        Assert.True(await userManager.IsInRoleAsync(resolved, IdentityRoleNames.Consumer));
    }

    [DatabaseIntegrationFact]
    public async Task Production_requires_a_first_super_admin_and_never_creates_test_identities()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleSeeder = scope.ServiceProvider.GetRequiredService<IRoleSeeder>();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        await using var transaction = await db.Database.BeginTransactionAsync();

        var superAdminRoleId = await db.Roles
            .Where(x => x.NormalizedName == IdentityRoleNames.SuperAdmin.ToUpperInvariant())
            .Select(x => x.Id)
            .SingleAsync();
        await db.UserRoles
            .Where(x => x.RoleId == superAdminRoleId)
            .ExecuteDeleteAsync();

        var provisioner = new SeedUserProvisioner(userManager);
        var missingConfigurationSeeder = new SuperAdminSeeder(
            userManager,
            provisioner,
            Options.Create(new IdentitySeedOptions()),
            environment,
            NullLogger<SuperAdminSeeder>.Instance);

        await Assert.ThrowsAsync<IdentitySeedException>(
            () => missingConfigurationSeeder.SeedAsync());

        var suffix = Guid.NewGuid().ToString("N");
        var seedOptions = IdentitySeedOptionsValidatorTests.CreateCompleteOptions(suffix);
        var options = Options.Create(seedOptions);
        var superAdminSeeder = new SuperAdminSeeder(
            userManager,
            provisioner,
            options,
            environment,
            NullLogger<SuperAdminSeeder>.Instance);
        var testUserSeeder = new NonProductionTestUserSeeder(
            db,
            provisioner,
            options,
            environment,
            NullLogger<NonProductionTestUserSeeder>.Instance);
        var runner = new IdentitySeedRunner(
            roleSeeder,
            superAdminSeeder,
            testUserSeeder,
            options,
            environment,
            NullLogger<IdentitySeedRunner>.Instance);

        await runner.SeedAsync();

        var superAdmin = await userManager.FindByEmailAsync(seedOptions.SuperAdmin.Email);
        Assert.NotNull(superAdmin);
        Assert.Equal(UserType.Admin, superAdmin.UserType);
        Assert.True(await userManager.IsInRoleAsync(superAdmin, IdentityRoleNames.SuperAdmin));

        var prohibitedEmails = new[]
        {
            seedOptions.TestUsers.Consumer.Email,
            seedOptions.TestUsers.BusinessOwner.Email,
            seedOptions.TestUsers.BusinessAdministrator.Email,
            seedOptions.TestUsers.ComplianceOfficer.Email,
            seedOptions.TestUsers.SupportOfficer.Email,
            seedOptions.TestUsers.OperationsOfficer.Email,
            seedOptions.TestUsers.Administrator.Email
        };

        foreach (var email in prohibitedEmails)
            Assert.Null(await userManager.FindByEmailAsync(email));

        await transaction.RollbackAsync();
    }

    private static async Task AssertUserAsync(
        UserManager<ApplicationUser> userManager,
        SeedUserOptions options,
        UserType expectedUserType,
        params string[] expectedRoles)
    {
        var user = await userManager.FindByEmailAsync(options.Email);
        Assert.NotNull(user);
        Assert.Equal(expectedUserType, user.UserType);

        var roles = await userManager.GetRolesAsync(user);
        Assert.Equal(
            expectedRoles.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            roles.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }
}
