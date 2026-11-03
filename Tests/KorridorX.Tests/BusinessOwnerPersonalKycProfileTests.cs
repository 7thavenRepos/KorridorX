using KorridorX.Data;
using KorridorX.Dtos.Compliance;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Identity;
using KorridorX.Services.Compliance;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessOwnerPersonalKycProfileTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task Business_owner_prepares_independent_personal_kyc_without_linking_consumer()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IKycService>();
        var suffix = Guid.NewGuid().ToString("N");
        var owner = NewUser("owner-" + suffix, UserType.Business);
        var consumer = NewUser("consumer-" + suffix, UserType.Consumer);
        db.Users.AddRange(owner, consumer);
        db.BusinessProfiles.Add(new BusinessProfile
        {
            Id = Guid.NewGuid(), OwnerUserId = owner.Id,
            BusinessName = "Owner profile coverage " + suffix,
            CountryCode = "NG", KybStatus = KybStatus.Approved
        });
        var existingConsumer = new CustomerProfile
        {
            Id = Guid.NewGuid(), UserId = consumer.Id,
            CustomerType = CustomerType.Individual,
            CountryCode = "NG", FirstName = "Consumer", LastName = "Existing",
            Email = consumer.Email, KycStatus = KycStatus.Approved,
            BlaaizCustomerId = "personal-existing-" + suffix
        };
        db.CustomerProfiles.Add(existingConsumer);
        await db.SaveChangesAsync();

        var request = new PreparePrimaryOwnerPersonalKycRequest
        {
            FirstName = "Business", LastName = "Owner",
            DateOfBirth = new DateTime(1988, 6, 17, 0, 0, 0, DateTimeKind.Unspecified),
            CountryCode = "NG", AddressLine1 = "1 Test Street", City = "Lagos",
            StateOrProvince = "Lagos", PostalCode = "100001"
        };
        var first = await service.PreparePrimaryOwnerPersonalKycAsync(owner.Id, request);
        var second = await service.PreparePrimaryOwnerPersonalKycAsync(owner.Id, request);

        Assert.Equal(first.CustomerProfileId, second.CustomerProfileId);
        Assert.Equal(1, await db.CustomerProfiles.CountAsync(x => x.UserId == owner.Id));
        Assert.Equal(KycStatus.NotStarted, first.KycStatus);
        var created = await db.CustomerProfiles.SingleAsync(x => x.Id == first.CustomerProfileId);
        Assert.Equal(DateTimeKind.Utc, created.DateOfBirth!.Value.Kind);
        Assert.Equal(new DateTime(1988, 6, 17, 0, 0, 0, DateTimeKind.Utc), created.DateOfBirth);
        Assert.Null(created.BlaaizCustomerId);
        Assert.Equal("personal-existing-" + suffix,
            (await db.CustomerProfiles.SingleAsync(x => x.Id == existingConsumer.Id)).BlaaizCustomerId);

        var app = await service.StartMyApplicationAsync(owner.Id);
        Assert.Equal(KycStatus.Pending, app.Status);
        Assert.Equal(1, await db.KycProfiles.CountAsync(x => x.CustomerProfileId == first.CustomerProfileId));
    }

    [DatabaseIntegrationFact]
    public async Task Business_team_member_cannot_prepare_primary_owner_personal_kyc()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IKycService>();
        var user = NewUser("member-" + Guid.NewGuid().ToString("N"), UserType.Business);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.PreparePrimaryOwnerPersonalKycAsync(user.Id,
                new PreparePrimaryOwnerPersonalKycRequest
                {
                    FirstName = "Team", LastName = "Member", CountryCode = "NG",
                    DateOfBirth = new DateTime(1990, 1, 1), AddressLine1 = "1 Street", City = "Lagos"
                }));
        Assert.False(await db.CustomerProfiles.AnyAsync(x => x.UserId == user.Id));
    }

    private static ApplicationUser NewUser(string prefix, UserType type) => new()
    {
        Id = Guid.NewGuid(), UserName = prefix + "@example.invalid",
        NormalizedUserName = (prefix + "@example.invalid").ToUpperInvariant(),
        Email = prefix + "@example.invalid",
        NormalizedEmail = (prefix + "@example.invalid").ToUpperInvariant(),
        FirstName = "Test", LastName = "Account", CountryCode = "NG",
        UserType = type, EmailConfirmed = true, Status = UserStatus.Active
    };
}
