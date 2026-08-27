using System.Net;
using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class RegistrationChannelBoundaryTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public RegistrationChannelBoundaryTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Consumer_endpoint_assigns_consumer_and_creates_customer_profile()
    {
        using var client = _fixture.CreateClient();
        var email = $"mobile-consumer-{Guid.NewGuid():N}@example.test";

        using var response = await client.PostJsonAsync(
            "/api/auth/register/consumer",
            Request(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAssignmentAsync(email, UserType.Consumer, "Consumer", hasCustomerProfile: true);
    }

    [DatabaseIntegrationFact]
    public async Task Business_endpoint_assigns_business_without_customer_profile()
    {
        using var client = _fixture.CreateClient();
        var email = $"web-business-{Guid.NewGuid():N}@example.test";

        using var response = await client.PostJsonAsync(
            "/api/auth/register/business",
            Request(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAssignmentAsync(email, UserType.Business, "Business", hasCustomerProfile: false);
    }

    [DatabaseIntegrationFact]
    public async Task Registration_ignores_caller_supplied_user_type_and_removes_generic_route()
    {
        using var client = _fixture.CreateClient();
        var consumerEmail = $"spoof-consumer-{Guid.NewGuid():N}@example.test";
        var businessEmail = $"spoof-business-{Guid.NewGuid():N}@example.test";

        using var consumerSpoof = await client.PostJsonAsync(
            "/api/auth/register/consumer",
            new
            {
                FirstName = "Channel",
                LastName = "Boundary",
                Email = consumerEmail,
                Password = "Password1",
                PhoneNumber = (string?)null,
                CountryCode = "US",
                UserType = UserType.Business
            });
        using var businessSpoof = await client.PostJsonAsync(
            "/api/auth/register/business",
            new
            {
                FirstName = "Channel",
                LastName = "Boundary",
                Email = businessEmail,
                Password = "Password1",
                PhoneNumber = (string?)null,
                CountryCode = "US",
                UserType = UserType.Consumer
            });
        using var genericRegistration = await client.PostJsonAsync(
            "/api/auth/register",
            Request($"generic-{Guid.NewGuid():N}@example.test"));

        Assert.Equal(HttpStatusCode.OK, consumerSpoof.StatusCode);
        Assert.Equal(HttpStatusCode.OK, businessSpoof.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, genericRegistration.StatusCode);
        await AssertAssignmentAsync(
            consumerEmail,
            UserType.Consumer,
            "Consumer",
            hasCustomerProfile: true);
        await AssertAssignmentAsync(
            businessEmail,
            UserType.Business,
            "Business",
            hasCustomerProfile: false);
    }

    private static ChannelRegistrationRequestDto Request(string email) =>
        new(
            "Channel",
            "Boundary",
            email,
            "Password1",
            null,
            "US");

    private async Task AssertAssignmentAsync(
        string email,
        UserType expectedUserType,
        string expectedRole,
        bool hasCustomerProfile)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.Equal(expectedUserType, user.UserType);
        Assert.True(await userManager.IsInRoleAsync(user, expectedRole));
        Assert.Equal(
            hasCustomerProfile,
            await db.CustomerProfiles.AnyAsync(profile => profile.UserId == user.Id));
    }
}
