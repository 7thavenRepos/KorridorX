using KorridorX.Configuration;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KorridorX.Data.Seed;

public interface ISeedUserProvisioner
{
    Task<ApplicationUser> EnsureUserAsync(
        SeedUserOptions options,
        UserType userType,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default);
}

public sealed class SeedUserProvisioner : ISeedUserProvisioner
{
    private readonly UserManager<ApplicationUser> _userManager;

    public SeedUserProvisioner(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ApplicationUser> EnsureUserAsync(
        SeedUserOptions options,
        UserType userType,
        IReadOnlyCollection<string> roles,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var email = options.Email.Trim().ToLowerInvariant();
        var normalizedEmail = _userManager.NormalizeEmail(email)
            ?? throw new IdentitySeedException("ASP.NET Core Identity could not normalize a seed email address.");

        var user = await _userManager.Users.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            ct);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = options.FirstName.Trim(),
                LastName = options.LastName.Trim(),
                PhoneNumber = CleanOptional(options.PhoneNumber),
                CountryCode = NormalizeCountryCode(options.CountryCode),
                UserType = userType,
                Status = UserStatus.Active
            };

            var createResult = await _userManager.CreateAsync(user, options.Password);
            if (!createResult.Succeeded)
            {
                throw IdentitySeedException.FromIdentityResult(
                    $"Creating the configured {userType} seed account",
                    createResult);
            }
        }
        else
        {
            if (user.UserType != userType)
            {
                throw new IdentitySeedException(
                    $"The configured seed email already belongs to a {user.UserType} account and cannot be reused as {userType}.");
            }

            if (user.Status != UserStatus.Active)
            {
                throw new IdentitySeedException(
                    "A configured seed account already exists but is not active. Reactivate it explicitly or configure a different email address.");
            }
        }

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            if (await _userManager.IsInRoleAsync(user, role))
                continue;

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw IdentitySeedException.FromIdentityResult(
                    $"Assigning the {role} role to a configured seed account",
                    roleResult);
            }
        }

        return user;
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCountryCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
