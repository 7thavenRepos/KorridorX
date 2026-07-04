using Microsoft.AspNetCore.Identity;

namespace KorridorX.Data.Seed;

public static class RoleSeeder
{
    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        string[] roles =
        {
            "Consumer",
            "Business",
            "BusinessAdmin",
            "Compliance",
            "Support",
            "Admin",
            "SuperAdmin"
        };

        foreach (var role in roles)
        {
            var exists = await roleManager.RoleExistsAsync(role);

            if (!exists)
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Name = role,
                    NormalizedName = role.ToUpperInvariant()
                });
            }
        }
    }
}