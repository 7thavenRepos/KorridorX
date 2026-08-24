using System.Reflection;
using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Data.Seed;
using KorridorX.Dtos.Compliance;
using KorridorX.Exceptions;
using KorridorX.Models.Customers;
using KorridorX.Models.EmbeddedFinance;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Services.Auth;
using KorridorX.Services.Compliance;
using KorridorX.Services.FinancialCore;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class OutboundFundsRestrictionTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public OutboundFundsRestrictionTests(
        ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Internal_audit_is_a_privileged_identity_role()
    {
        Assert.Contains(
            IdentityRoleNames.InternalAudit,
            IdentityRoleNames.All);

        Assert.True(
            MfaSecurityPolicy.RequiresMfa(
                [IdentityRoleNames.InternalAudit]));

        var authorize = typeof(
                AdminOutboundFundsRestrictionsController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Contains(
            IdentityRoleNames.InternalAudit,
            authorize!.Roles!.Split(','));
    }

    [Fact]
    public void Subject_lookup_endpoint_is_available_to_restriction_roles()
    {
        var method = typeof(
                AdminOutboundFundsRestrictionsController)
            .GetMethod("SearchSubjects");

        Assert.NotNull(method);

        var route = method!
            .GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>();

        Assert.NotNull(route);
        Assert.Equal("subjects", route!.Template);
    }

    [Fact]
    public void Restricted_error_contract_is_stable_and_generic()
    {
        var exception = new OutboundFundsRestrictedException();

        Assert.Equal(
            "OUTBOUND_FUNDS_RESTRICTED",
            exception.Code);

        Assert.Equal(
            OutboundFundsRestrictedException.PublicMessage,
            exception.Message);

        Assert.DoesNotContain(
            "PND",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "bank",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [DatabaseIntegrationFact]
    public async Task User_restriction_blocks_outbound_and_requires_different_releaser()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var restrictions = scope.ServiceProvider
            .GetRequiredService<IOutboundFundsRestrictionService>();

        var subject = await CreateUserAsync(
            userManager,
            UserType.Consumer,
            "restriction-subject");

        var maker = await CreateUserAsync(
            userManager,
            UserType.Admin,
            "restriction-maker");

        var checker = await CreateUserAsync(
            userManager,
            UserType.Admin,
            "restriction-checker");

        var applied = await restrictions.ApplyAsync(
            new ApplyOutboundFundsRestrictionRequestDto
            {
                SubjectType =
                    OutboundFundsRestrictionSubjectType.User,
                SubjectId = subject.Id,
                Source =
                    OutboundFundsRestrictionSource.ExternalBankRestriction,
                InternalReason =
                    "External bank advised that outbound movement must be restricted.",
                ExternalReference = "TEST-PND-REFERENCE"
            },
            maker.Id);

        Assert.True(applied.IsActive);

        await Assert.ThrowsAsync<
            OutboundFundsRestrictedException>(
            () => restrictions.EnsureUserOutboundAllowedAsync(
                subject.Id,
                "test_withdrawal"));

        var sameMakerError =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => restrictions.LiftAsync(
                    applied.Id,
                    new LiftOutboundFundsRestrictionRequestDto
                    {
                        Reason = "Attempted maker release."
                    },
                    maker.Id));

        Assert.Contains(
            "different authorized reviewer",
            sameMakerError.Message,
            StringComparison.OrdinalIgnoreCase);

        var lifted = await restrictions.LiftAsync(
            applied.Id,
            new LiftOutboundFundsRestrictionRequestDto
            {
                Reason =
                    "Independent review confirmed the external restriction was removed."
            },
            checker.Id);

        Assert.False(lifted.IsActive);

        await restrictions.EnsureUserOutboundAllowedAsync(
            subject.Id,
            "test_withdrawal");
    }

    [DatabaseIntegrationFact]
    public async Task Subject_search_marks_an_existing_user_restriction_as_active()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var restrictions = scope.ServiceProvider
            .GetRequiredService<IOutboundFundsRestrictionService>();

        var subject = await CreateUserAsync(
            userManager,
            UserType.Consumer,
            "restriction-search-subject");

        var maker = await CreateUserAsync(
            userManager,
            UserType.Admin,
            "restriction-search-maker");

        await restrictions.ApplyAsync(
            new ApplyOutboundFundsRestrictionRequestDto
            {
                SubjectType =
                    OutboundFundsRestrictionSubjectType.User,
                SubjectId = subject.Id,
                Source =
                    OutboundFundsRestrictionSource.ComplianceReview,
                InternalReason =
                    "Test active flag in subject lookup."
            },
            maker.Id);

        var matches = await restrictions.SearchSubjectsAsync(
            OutboundFundsRestrictionSubjectType.User,
            subject.Email![..10]);

        var match = Assert.Single(
            matches,
            x => x.SubjectId == subject.Id);

        Assert.True(match.HasActiveRestriction);
        Assert.Equal(
            OutboundFundsRestrictionSubjectType.User,
            match.SubjectType);
    }

    [DatabaseIntegrationFact]
    public async Task Business_restriction_is_inherited_by_embedded_customer()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var restrictions = scope.ServiceProvider
            .GetRequiredService<IOutboundFundsRestrictionService>();

        var owner = await CreateUserAsync(
            userManager,
            UserType.Business,
            "restriction-business-owner");

        var maker = await CreateUserAsync(
            userManager,
            UserType.Admin,
            "restriction-business-maker");

        var business = new BusinessProfile
        {
            OwnerUserId = owner.Id,
            OwnerUser = owner,
            BusinessName =
                $"Restriction Business {Guid.NewGuid():N}",
            CountryCode = "CA"
        };

        var customer = new BusinessCustomer
        {
            BusinessProfile = business,
            BusinessProfileId = business.Id,
            ExternalReference =
                $"restriction-customer-{Guid.NewGuid():N}",
            DisplayName = "Restricted embedded customer",
            CountryCode = "CA",
            Status = BusinessCustomerStatus.Active
        };

        db.BusinessProfiles.Add(business);
        db.BusinessCustomers.Add(customer);
        await db.SaveChangesAsync();

        await restrictions.ApplyAsync(
            new ApplyOutboundFundsRestrictionRequestDto
            {
                SubjectType =
                    OutboundFundsRestrictionSubjectType.BusinessProfile,
                SubjectId = business.Id,
                Source =
                    OutboundFundsRestrictionSource.ComplianceReview,
                InternalReason =
                    "Business-wide outbound restriction for review."
            },
            maker.Id);

        await Assert.ThrowsAsync<
            OutboundFundsRestrictedException>(
            () =>
                restrictions
                    .EnsureBusinessCustomerOutboundAllowedAsync(
                        customer.Id,
                        "embedded_transfer"));
    }

    [DatabaseIntegrationFact]
    public async Task Financial_reservation_blocks_transfer_but_not_internal_trade()
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var restrictions = scope.ServiceProvider
            .GetRequiredService<IOutboundFundsRestrictionService>();

        var reservations = scope.ServiceProvider
            .GetRequiredService<IFinancialReservationService>();

        var subject = await CreateUserAsync(
            userManager,
            UserType.Consumer,
            "restriction-account-subject");

        var maker = await CreateUserAsync(
            userManager,
            UserType.Admin,
            "restriction-account-maker");

        var asset = await db.Assets
            .AsNoTracking()
            .FirstAsync();

        var account = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.User,
            OwnerId = subject.Id,
            AccountCode =
                $"RST-{Guid.NewGuid():N}"[..32],
            AssetCode = asset.Code,
            AccountType = FinancialAccountType.Customer,
            Status = FinancialAccountStatus.Active,
            SettledBalance = 1000m,
            AvailableBalance = 1000m,
            HeldBalance = 0m
        };

        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync();

        await restrictions.ApplyAsync(
            new ApplyOutboundFundsRestrictionRequestDto
            {
                SubjectType =
                    OutboundFundsRestrictionSubjectType.User,
                SubjectId = subject.Id,
                Source =
                    OutboundFundsRestrictionSource.InternalAuditReview,
                InternalReason =
                    "Test restriction for reservation boundary."
            },
            maker.Id);

        await Assert.ThrowsAsync<
            OutboundFundsRestrictedException>(
            () => reservations.ReserveAsync(
                account.Id,
                FinancialReservationType.Transfer,
                nameof(FinancialReservation),
                Guid.NewGuid(),
                10m,
                null));

        var tradeReservation =
            await reservations.ReserveAsync(
                account.Id,
                FinancialReservationType.MarketplaceTrade,
                nameof(FinancialReservation),
                Guid.NewGuid(),
                10m,
                null);

        Assert.Equal(
            FinancialReservationStatus.Active,
            tradeReservation.Status);
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        UserType userType,
        string prefix)
    {
        var email =
            $"{prefix}-{Guid.NewGuid():N}@example.test";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Restriction",
            LastName = "Tester",
            CountryCode = "CA",
            UserType = userType,
            Status = UserStatus.Active
        };

        var result = await userManager.CreateAsync(
            user,
            "RestrictionTest!123");

        Assert.True(
            result.Succeeded,
            string.Join(
                "; ",
                result.Errors.Select(x => x.Description)));

        return user;
    }
}
