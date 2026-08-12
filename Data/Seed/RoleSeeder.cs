using Microsoft.AspNetCore.Identity;

namespace KorridorX.Data.Seed;

public interface IRoleSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

public sealed class RoleSeeder : IRoleSeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public RoleSeeder(RoleManager<IdentityRole<Guid>> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var roleName in IdentityRoleNames.All)
        {
            ct.ThrowIfCancellationRequested();

            if (await _roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await _roleManager.CreateAsync(new IdentityRole<Guid>
            {
                Name = roleName
            });

            if (!result.Succeeded)
            {
                throw IdentitySeedException.FromIdentityResult(
                    $"Creating the {roleName} role",
                    result);
            }
        }
    }

    // Kept for release-candidate fixtures and one-off tools that already call
    // the original static entry point.
    public static async Task SeedRolesAsync(
        IServiceProvider services,
        CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IRoleSeeder>();
        await seeder.SeedAsync(ct);
    }
}
