using System.Net;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Models.Lookups;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ConsumerOnboardingLookupTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ConsumerOnboardingLookupTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Consumer_registration_normalizes_a_supported_send_country()
    {
        var countryCode = await AddCountryAsync(isSupported: true, isSendCountry: true);
        var email = $"onboarding-{Guid.NewGuid():N}@example.test";
        using var client = _fixture.CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/auth/register/consumer",
            Registration(email, countryCode.ToLowerInvariant()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.Equal(countryCode, user.CountryCode);
        Assert.Equal(
            countryCode,
            await db.CustomerProfiles
                .Where(x => x.UserId == user.Id)
                .Select(x => x.CountryCode)
                .SingleAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Consumer_registration_rejects_a_receive_only_country()
    {
        var countryCode = await AddCountryAsync(isSupported: true, isSendCountry: false);
        var email = $"receive-only-{Guid.NewGuid():N}@example.test";
        using var client = _fixture.CreateClient();

        using var response = await client.PostJsonAsync(
            "/api/auth/register/consumer",
            Registration(email, countryCode));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(email));
    }

    [DatabaseIntegrationFact]
    public async Task Profile_update_synchronizes_the_identity_returned_by_auth_me()
    {
        var firstCountry = await AddCountryAsync(isSupported: true, isSendCountry: true);
        var nextCountry = await AddCountryAsync(isSupported: true, isSendCountry: true);
        var email = $"profile-sync-{Guid.NewGuid():N}@example.test";
        const string password = "ReleaseCandidate!123";
        using var client = _fixture.CreateClient();

        var (_, authentication) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Initial",
                "Customer",
                email,
                password,
                null,
                firstCountry,
                UserType.Consumer));
        client.UseBearerToken(authentication.AccessToken);

        using var updateResponse = await client.PutJsonAsync(
            "/api/customer-profile/me",
            new UpdateCustomerProfileRequestDto(
                "Updated",
                "Customer",
                null,
                new DateTime(1990, 1, 2),
                "+15550001111",
                nextCountry.ToLowerInvariant(),
                "Ontario",
                "Toronto",
                "1 Test Street",
                null,
                "A1A 1A1"));
        await updateResponse.EnsureSuccessWithBodyAsync();

        using var meResponse = await client.GetAsync("/api/auth/me");
        await meResponse.EnsureSuccessWithBodyAsync();
        var me = (await meResponse.ReadApiResponseAsync<CurrentUserDto>()).Data
            ?? throw new InvalidOperationException("Current-user response returned no data.");

        Assert.Equal("Updated", me.FirstName);
        Assert.Equal("+15550001111", me.PhoneNumber);
        Assert.Equal(nextCountry, me.CountryCode);
    }

    private async Task<string> AddCountryAsync(bool isSupported, bool isSendCountry)
    {
        var code = $"Z{Guid.NewGuid():N}"[..7].ToUpperInvariant();
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Countries.Add(new Country
        {
            Code = code,
            Iso3Code = code[..3],
            Name = $"Onboarding Test Country {code}",
            IsSupported = isSupported,
            IsSendCountry = isSendCountry,
            IsReceiveCountry = !isSendCountry
        });
        await db.SaveChangesAsync();
        return code;
    }

    private static ChannelRegistrationRequestDto Registration(
        string email,
        string countryCode) =>
        new(
            "Mobile",
            "Customer",
            email,
            "ReleaseCandidate!123",
            null,
            countryCode);
}
