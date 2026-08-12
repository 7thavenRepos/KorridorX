using KorridorX.Configuration;
using KorridorX.Data.Seed;
using KorridorX.Tests.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests;

public sealed class IdentitySeedRunnerTests
{
    [Fact]
    public async Task Production_never_invokes_the_non_production_user_seeder()
    {
        var roles = new CountingRoleSeeder();
        var superAdmin = new CountingSuperAdminSeeder();
        var testUsers = new CountingTestUserSeeder();
        var options = new IdentitySeedOptions { SeedTestUsers = true };
        var runner = CreateRunner(
            Environments.Production,
            options,
            roles,
            superAdmin,
            testUsers);

        await runner.SeedAsync();

        Assert.Equal(1, roles.Calls);
        Assert.Equal(1, superAdmin.Calls);
        Assert.Equal(0, testUsers.Calls);
    }

    [Fact]
    public async Task Testing_never_runs_application_startup_identity_seeding()
    {
        var roles = new CountingRoleSeeder();
        var superAdmin = new CountingSuperAdminSeeder();
        var testUsers = new CountingTestUserSeeder();
        var runner = CreateRunner(
            "Testing",
            new IdentitySeedOptions { SeedTestUsers = true },
            roles,
            superAdmin,
            testUsers);

        await runner.SeedAsync();

        Assert.Equal(0, roles.Calls);
        Assert.Equal(0, superAdmin.Calls);
        Assert.Equal(0, testUsers.Calls);
    }

    [Fact]
    public async Task Development_runs_test_identity_seeding_only_when_explicitly_enabled()
    {
        var disabledTestUsers = new CountingTestUserSeeder();
        var disabledRunner = CreateRunner(
            Environments.Development,
            new IdentitySeedOptions { SeedTestUsers = false },
            testUsers: disabledTestUsers);

        await disabledRunner.SeedAsync();

        Assert.Equal(0, disabledTestUsers.Calls);

        var enabledTestUsers = new CountingTestUserSeeder();
        var enabledRunner = CreateRunner(
            Environments.Development,
            new IdentitySeedOptions { SeedTestUsers = true },
            testUsers: enabledTestUsers);

        await enabledRunner.SeedAsync();

        Assert.Equal(1, enabledTestUsers.Calls);
    }

    [Fact]
    public async Task Direct_non_production_seeder_call_is_blocked_in_production()
    {
        var seeder = new NonProductionTestUserSeeder(
            null!,
            null!,
            Options.Create(new IdentitySeedOptions { SeedTestUsers = true }),
            new TestHostEnvironment { EnvironmentName = Environments.Production },
            NullLogger<NonProductionTestUserSeeder>.Instance);

        await seeder.SeedAsync();
    }

    private static IdentitySeedRunner CreateRunner(
        string environmentName,
        IdentitySeedOptions options,
        CountingRoleSeeder? roles = null,
        CountingSuperAdminSeeder? superAdmin = null,
        CountingTestUserSeeder? testUsers = null) =>
        new(
            roles ?? new CountingRoleSeeder(),
            superAdmin ?? new CountingSuperAdminSeeder(),
            testUsers ?? new CountingTestUserSeeder(),
            Options.Create(options),
            new TestHostEnvironment { EnvironmentName = environmentName },
            NullLogger<IdentitySeedRunner>.Instance);

    private sealed class CountingRoleSeeder : IRoleSeeder
    {
        public int Calls { get; private set; }

        public Task SeedAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingSuperAdminSeeder : ISuperAdminSeeder
    {
        public int Calls { get; private set; }

        public Task SeedAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingTestUserSeeder : INonProductionTestUserSeeder
    {
        public int Calls { get; private set; }

        public Task SeedAsync(CancellationToken ct = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
