using System.Net;
using KorridorX.Data;
using KorridorX.Dtos.AccountClosure;
using KorridorX.Dtos.Auth;
using KorridorX.Models.AccountClosure;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Identity;
using KorridorX.Services.Auth;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

// Uses the disposable PostgreSQL RC fixture. Never point KORRIDORX_TEST_CONNECTION_STRING
// at an existing database: that fixture intentionally recreates the public schema.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class AccountClosureWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    private const string Password = "ReleaseCandidate!123";

    [DatabaseIntegrationFact]
    public async Task Consumer_can_request_withdraw_and_request_again_without_deleting_account()
    {
        using var client = fixture.CreateClient();
        var (registration, authentication) = await RegisterAsync(client, UserType.Consumer, "CA");
        client.UseBearerToken(authentication.AccessToken);

        using var initialResponse = await client.GetAsync("/api/account-closure/personal");
        Assert.Equal(HttpStatusCode.OK, initialResponse.StatusCode);
        var initial = Assert.IsType<AccountClosureOverviewDto>(
            (await initialResponse.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.Equal(registration.UserId, initial.SubjectId);
        Assert.Equal("Personal", initial.Scope);
        Assert.Null(initial.OpenRequestId);
        Assert.True(initial.MayRequest);
        Assert.False(initial.FinalizationAvailable);

        using var missingConsent = await client.PostJsonAsync(
            "/api/account-closure/personal/request", new AccountClosureRequestInput(false));
        Assert.Equal(HttpStatusCode.BadRequest, missingConsent.StatusCode);

        using var first = await client.PostJsonAsync(
            "/api/account-closure/personal/request", new AccountClosureRequestInput(true));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstOverview = Assert.IsType<AccountClosureOverviewDto>(
            (await first.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.True(firstOverview.OpenRequestId.HasValue);
        var firstId = firstOverview.OpenRequestId.Value;
        Assert.Equal("Requested", firstOverview.RequestStatus);
        Assert.False(firstOverview.FinalizationAvailable);

        using var repeated = await client.PostJsonAsync(
            "/api/account-closure/personal/request", new AccountClosureRequestInput(true));
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        var repeatedOverview = Assert.IsType<AccountClosureOverviewDto>(
            (await repeated.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.Equal(firstId, repeatedOverview.OpenRequestId);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.AccountClosureRequests.CountAsync(x =>
                x.Scope == AccountClosureScope.Personal && x.SubjectId == registration.UserId &&
                x.Status == AccountClosureRequestStatus.Requested && !x.IsDeleted));
            Assert.Equal(UserStatus.Active, (await db.Users.AsNoTracking()
                .SingleAsync(x => x.Id == registration.UserId)).Status);
            Assert.True(await db.CustomerProfiles.AsNoTracking().AnyAsync(x =>
                x.UserId == registration.UserId && !x.IsDeleted));
        }

        using var withdrawn = await client.PostJsonAsync(
            "/api/account-closure/personal/withdraw-request", new { });
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        var withdrawnOverview = Assert.IsType<AccountClosureOverviewDto>(
            (await withdrawn.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.Null(withdrawnOverview.OpenRequestId);
        Assert.False(withdrawnOverview.FinalizationAvailable);

        using var repeatWithdrawal = await client.PostJsonAsync(
            "/api/account-closure/personal/withdraw-request", new { });
        Assert.Equal(HttpStatusCode.OK, repeatWithdrawal.StatusCode);

        using var reopened = await client.PostJsonAsync(
            "/api/account-closure/personal/request", new AccountClosureRequestInput(true));
        Assert.Equal(HttpStatusCode.Accepted, reopened.StatusCode);
        var reopenedOverview = Assert.IsType<AccountClosureOverviewDto>(
            (await reopened.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.NotNull(reopenedOverview.OpenRequestId);
        Assert.NotEqual(firstId, reopenedOverview.OpenRequestId!.Value);
        Assert.False(reopenedOverview.FinalizationAvailable);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var requests = await db.AccountClosureRequests.AsNoTracking().Where(x =>
                x.Scope == AccountClosureScope.Personal && x.SubjectId == registration.UserId &&
                !x.IsDeleted).ToListAsync();
            Assert.Equal(2, requests.Count);
            Assert.Single(requests, x => x.Status == AccountClosureRequestStatus.Withdrawn);
            Assert.Single(requests, x => x.Status == AccountClosureRequestStatus.Requested);
            Assert.Equal(UserStatus.Active, (await db.Users.AsNoTracking()
                .SingleAsync(x => x.Id == registration.UserId)).Status);
        }
    }

    [DatabaseIntegrationFact]
    public async Task Consumer_can_request_closure_with_funds_but_review_remains_blocked_and_funds_untouched()
    {
        using var client = fixture.CreateClient();
        var (registration, authentication) = await RegisterAsync(client, UserType.Consumer, "CA");
        client.UseBearerToken(authentication.AccessToken);

        var walletId = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.FinancialAccounts.Add(new FinancialAccount
            {
                Id = walletId,
                AccountCode = $"KX-W11C5-RC-{Guid.NewGuid():N}",
                OwnerType = FinancialAccountOwnerType.User,
                OwnerId = registration.UserId,
                AssetCode = "CAD",
                AccountType = FinancialAccountType.Customer,
                Status = FinancialAccountStatus.Active,
                SettledBalance = 25m,
                AvailableBalance = 20m,
                HeldBalance = 5m
            });
            await db.SaveChangesAsync();
        }

        using var before = await client.GetAsync("/api/account-closure/personal");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeOverview = Assert.IsType<AccountClosureOverviewDto>(
            (await before.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.False(beforeOverview.ReadyForOperationsReview);
        Assert.Contains(beforeOverview.Blockers, x => x.Contains("Funds", StringComparison.OrdinalIgnoreCase));

        using var request = await client.PostJsonAsync(
            "/api/account-closure/personal/request", new AccountClosureRequestInput(true));
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var overview = Assert.IsType<AccountClosureOverviewDto>(
            (await request.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.NotNull(overview.OpenRequestId);
        Assert.False(overview.ReadyForOperationsReview);
        Assert.False(overview.FinalizationAvailable);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wallet = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == walletId);
            Assert.Equal(25m, wallet.SettledBalance);
            Assert.Equal(20m, wallet.AvailableBalance);
            Assert.Equal(5m, wallet.HeldBalance);
            Assert.Equal(UserStatus.Active, (await db.Users.AsNoTracking()
                .SingleAsync(x => x.Id == registration.UserId)).Status);
        }
    }

    [DatabaseIntegrationFact]
    public async Task Business_closure_is_owner_only_and_does_not_disable_member_or_owner_logins()
    {
        using var ownerClient = fixture.CreateClient();
        var (owner, ownerToken) = await RegisterBusinessForAuthorizationTestAsync(ownerClient);
        ownerClient.UseBearerToken(ownerToken);
        using var memberClient = fixture.CreateClient();
        var (member, memberToken) = await RegisterBusinessForAuthorizationTestAsync(memberClient);
        memberClient.UseBearerToken(memberToken);

        var businessId = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BusinessProfiles.Add(new BusinessProfile
            {
                Id = businessId,
                OwnerUserId = owner.UserId,
                BusinessName = "W11C5 Test Limited",
                CountryCode = "NG"
            });
            db.BusinessUsers.Add(new BusinessUser
            {
                BusinessProfileId = businessId,
                UserId = member.UserId,
                Role = BusinessUserRole.Member,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        using var memberBlocked = await memberClient.PostJsonAsync(
            "/api/account-closure/business/request", new AccountClosureRequestInput(true));
        Assert.Equal(HttpStatusCode.Forbidden, memberBlocked.StatusCode);

        using var wrongChannel = await ownerClient.GetAsync("/api/account-closure/personal");
        Assert.Equal(HttpStatusCode.Forbidden, wrongChannel.StatusCode);

        using var ownerRequest = await ownerClient.PostJsonAsync(
            "/api/account-closure/business/request", new AccountClosureRequestInput(true));
        Assert.Equal(HttpStatusCode.Accepted, ownerRequest.StatusCode);
        var overview = Assert.IsType<AccountClosureOverviewDto>(
            (await ownerRequest.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.Equal(businessId, overview.SubjectId);
        Assert.Equal("Business", overview.Scope);
        Assert.Equal("Requested", overview.RequestStatus);
        Assert.False(overview.FinalizationAvailable);

        using var ownerRepeat = await ownerClient.PostJsonAsync(
            "/api/account-closure/business/request", new AccountClosureRequestInput(true));
        Assert.Equal(HttpStatusCode.Accepted, ownerRepeat.StatusCode);
        var repeated = Assert.IsType<AccountClosureOverviewDto>(
            (await ownerRepeat.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.Equal(overview.OpenRequestId, repeated.OpenRequestId);

        using var memberQueue = await memberClient.GetAsync("/api/account-closure/admin/requests");
        Assert.Equal(HttpStatusCode.Forbidden, memberQueue.StatusCode);

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Single(await db.AccountClosureRequests.AsNoTracking()
                .Where(x => x.Scope == AccountClosureScope.Business &&
                    x.SubjectId == businessId && x.Status == AccountClosureRequestStatus.Requested)
                .ToListAsync());
            Assert.False((await db.BusinessProfiles.AsNoTracking()
                .SingleAsync(x => x.Id == businessId)).IsDeleted);
            Assert.Equal(UserStatus.Active, (await db.Users.AsNoTracking()
                .SingleAsync(x => x.Id == owner.UserId)).Status);
            Assert.Equal(UserStatus.Active, (await db.Users.AsNoTracking()
                .SingleAsync(x => x.Id == member.UserId)).Status);
        }

        using var withdrawn = await ownerClient.PostJsonAsync(
            "/api/account-closure/business/withdraw-request", new { });
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        var withdrawnOverview = Assert.IsType<AccountClosureOverviewDto>(
            (await withdrawn.ReadApiResponseAsync<AccountClosureOverviewDto>()).Data);
        Assert.Null(withdrawnOverview.OpenRequestId);
    }

    // Full MFA enrollment/replay is covered by MfaWorkflowTests.
    // For this role-authorization test, enable the account's actual MFA flag
    // before issuing an MFA-authenticated test JWT via the production token service.
    // The bearer validator requires BOTH the MFA flag and the MFA claim.
    private async Task<(RegistrationResultDto Registration, string AccessToken)> RegisterBusinessForAuthorizationTestAsync(
        HttpClient client)
    {
        var registration = await client.RegisterAndConfirmAsync(
            fixture.Factory.Services,
            new RegisterRequestDto("Wale", "Business", $"w11c5-business-{Guid.NewGuid():N}@example.test",
                Password, "+2348012345678", "NG", UserType.Business));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(registration.UserId.ToString())
            ?? throw new InvalidOperationException("Business test user was not found.");
        var enabled = await users.SetTwoFactorEnabledAsync(user, true);
        Assert.True(enabled.Succeeded);
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var (token, _) = await jwt.GenerateAccessTokenAsync(user, mfaAuthenticated: true);
        return (registration, token);
    }

    private async Task<(RegistrationResultDto Registration, AuthResponseDto Authentication)> RegisterAsync(
        HttpClient client, UserType type, string country)
    {
        var email = $"w11c5-{Guid.NewGuid():N}@example.test";
        return await client.RegisterConfirmAndLoginAsync(
            fixture.Factory.Services,
            new RegisterRequestDto(
                "Wale", "Acceptance", email, Password,
                type == UserType.Consumer ? "+14165550198" : "+2348012345678", country, type));
    }
}
