using KorridorX.Controllers;
using KorridorX.Data;
using KorridorX.Dtos.AdminFunding;
using KorridorX.Dtos.Auth;
using KorridorX.Models.AdminFunding;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Services.AdminFunding;
using KorridorX.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace KorridorX.Tests;

// IMPORTANT: The existing CI integration fixture DROPS the public schema.
// These tests MUST run only in GitHub CI's disposable 'korridorx_ci' database.
// Never set KORRIDORX_TEST_CONNECTION_STRING to the user's local 'korridorx_rc'.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class AdminWalletFundingWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
{
    private const string TestPassword = "ReleaseCandidate!123";

    [DatabaseIntegrationFact]
    public async Task Operations_request_requires_independent_approval_before_a_balanced_single_post()
    {
        using var client = fixture.CreateClient();
        var (registration, _) = await client.RegisterConfirmAndLoginAsync(
            fixture.Factory.Services,
            new RegisterRequestDto("Funding", "Customer",
                $"w12-ops-{Guid.NewGuid():N}@example.test", TestPassword,
                "+14165550123", "CA", UserType.Consumer));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<AdminWalletFundingService>();
        var (house, wallet) = SeedPair(db, FinancialAccountOwnerType.User,
            registration.UserId, "CAD", 150m, 5m);
        await db.SaveChangesAsync();

        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        var input = new CreateAdminWalletFundingRequestDto(house.Id, wallet.Id, 40m,
            "W12 disposable CI maker-checker acceptance");
        var pending = await service.CreateAsync(maker, false, input, $"ops-{Guid.NewGuid():N}", default);

        Assert.Equal(AdminWalletFundingStatus.PendingApproval, pending.Status);
        Assert.Null(pending.LedgerTransactionId);
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(x =>
            x.RelatedEntityType == nameof(AdminWalletFundingRequest) && x.RelatedEntityId == pending.Id));
        Assert.Equal(150m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == house.Id)).AvailableBalance);
        Assert.Equal(5m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == wallet.Id)).AvailableBalance);

        var selfReview = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(maker, pending.Id, default));
        Assert.Contains("cannot approve", selfReview.Message, StringComparison.OrdinalIgnoreCase);

        var posted = await service.ApproveAsync(checker, pending.Id, default);
        Assert.Equal(AdminWalletFundingStatus.Posted, posted.Status);
        Assert.Equal(checker, posted.ReviewedByUserId);
        Assert.NotNull(posted.LedgerTransactionId);

        var repeat = await service.ApproveAsync(checker, pending.Id, default);
        Assert.Equal(posted.LedgerTransactionId, repeat.LedgerTransactionId);
        db.ChangeTracker.Clear();

        var houseAfter = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == house.Id);
        var walletAfter = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == wallet.Id);
        Assert.Equal(110m, houseAfter.AvailableBalance);
        Assert.Equal(110m, houseAfter.SettledBalance);
        Assert.Equal(45m, walletAfter.AvailableBalance);
        Assert.Equal(45m, walletAfter.SettledBalance);
        Assert.Equal(0m, houseAfter.HeldBalance);
        Assert.Equal(0m, walletAfter.HeldBalance);

        var ledger = await db.LedgerTransactions.AsNoTracking().Include(x => x.Postings)
            .SingleAsync(x => x.Id == posted.LedgerTransactionId!.Value);
        Assert.Equal(LedgerTransactionType.FundingCredit, ledger.Type);
        Assert.Equal(40m, ledger.Amount);
        Assert.Equal(2, ledger.Postings.Count);
        Assert.Contains(ledger.Postings, x => x.FinancialAccountId == house.Id &&
            x.Side == LedgerPostingSide.Debit && x.Amount == 40m &&
            x.BalanceBucket == LedgerBalanceBucket.Available);
        Assert.Contains(ledger.Postings, x => x.FinancialAccountId == wallet.Id &&
            x.Side == LedgerPostingSide.Credit && x.Amount == 40m &&
            x.BalanceBucket == LedgerBalanceBucket.Available);
        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x =>
            x.RelatedEntityType == nameof(AdminWalletFundingRequest) && x.RelatedEntityId == pending.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Direct_business_funding_is_idempotent_and_cannot_overdraw_house_account()
    {
        using var client = fixture.CreateClient();
        var registered = await client.RegisterAndConfirmAsync(
            fixture.Factory.Services,
            new RegisterRequestDto("Funding", "Business",
                $"w12-business-{Guid.NewGuid():N}@example.test", TestPassword,
                "+2348012345678", "NG", UserType.Business));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<AdminWalletFundingService>();
        var business = new BusinessProfile
        {
            OwnerUserId = registered.UserId,
            BusinessName = "W12 Funding CI Acceptance Ltd",
            CountryCode = "NG"
        };
        db.BusinessProfiles.Add(business);
        var (house, wallet) = SeedPair(db, FinancialAccountOwnerType.Business,
            business.Id, "NGN", 1000m, 0m);
        await db.SaveChangesAsync();

        var admin = Guid.NewGuid();
        var idempotency = $"admin-{Guid.NewGuid():N}";
        var command = new CreateAdminWalletFundingRequestDto(house.Id, wallet.Id,
            120m, "W12 disposable CI business credit");
        var first = await service.CreateAsync(admin, true, command, idempotency, default);
        var second = await service.CreateAsync(admin, true, command, idempotency, default);
        Assert.Equal(AdminWalletFundingStatus.Posted, first.Status);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.LedgerTransactionId, second.LedgerTransactionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(admin, true,
                command with { Amount = 121m }, idempotency, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(admin, true,
                command with { Amount = 990m }, $"overdraw-{Guid.NewGuid():N}", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(admin, true,
                command with { Amount = 0.001m }, $"precision-{Guid.NewGuid():N}", default));

        db.ChangeTracker.Clear();
        var houseAfter = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == house.Id);
        var walletAfter = await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == wallet.Id);
        Assert.Equal(880m, houseAfter.AvailableBalance);
        Assert.Equal(880m, houseAfter.SettledBalance);
        Assert.Equal(120m, walletAfter.AvailableBalance);
        Assert.Equal(120m, walletAfter.SettledBalance);
        Assert.Equal(1, await db.AdminWalletFundingRequests.CountAsync(x =>
            x.SourceHouseAccountId == house.Id));
        Assert.Equal(1, await db.LedgerTransactions.CountAsync(x =>
            x.RelatedEntityType == nameof(AdminWalletFundingRequest) && x.RelatedEntityId == first.Id));
    }

    [DatabaseIntegrationFact]
    public async Task Rejected_operations_request_and_invalid_currency_never_change_balances()
    {
        using var client = fixture.CreateClient();
        var (registration, _) = await client.RegisterConfirmAndLoginAsync(
            fixture.Factory.Services,
            new RegisterRequestDto("Funding", "Rejection",
                $"w12-reject-{Guid.NewGuid():N}@example.test", TestPassword,
                "+14165550126", "CA", UserType.Consumer));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<AdminWalletFundingService>();
        var (house, wallet) = SeedPair(db, FinancialAccountOwnerType.User,
            registration.UserId, "CAD", 50m, 0m);
        var differentAssetWallet = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.User,
            OwnerId = registration.UserId,
            AccountType = FinancialAccountType.Customer,
            AccountCode = $"KX-W12-CI-USD-{Guid.NewGuid():N}",
            AssetCode = "USD", Status = FinancialAccountStatus.Active
        };
        db.FinancialAccounts.Add(differentAssetWallet);
        await db.SaveChangesAsync();

        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        var request = await service.CreateAsync(maker, false,
            new CreateAdminWalletFundingRequestDto(house.Id, wallet.Id, 30m,
                "W12 disposable CI rejection acceptance"),
            $"reject-{Guid.NewGuid():N}", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RejectAsync(maker, request.Id, "Maker self review", default));
        var rejected = await service.RejectAsync(checker, request.Id,
            "Independent reviewer rejected", default);
        Assert.Equal(AdminWalletFundingStatus.Rejected, rejected.Status);
        Assert.Null(rejected.LedgerTransactionId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveAsync(checker, request.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(checker, true,
                new CreateAdminWalletFundingRequestDto(house.Id, differentAssetWallet.Id, 5m,
                    "Currency mismatch"), $"cross-{Guid.NewGuid():N}", default));

        db.ChangeTracker.Clear();
        Assert.Equal(50m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == house.Id)).AvailableBalance);
        Assert.Equal(0m, (await db.FinancialAccounts.AsNoTracking().SingleAsync(x => x.Id == wallet.Id)).AvailableBalance);
        Assert.Equal(0, await db.LedgerTransactions.CountAsync(x =>
            x.RelatedEntityType == nameof(AdminWalletFundingRequest) && x.RelatedEntityId == request.Id));
    }

    [Fact]
    public void Funding_endpoints_require_approver_roles_and_legacy_unbacked_credit_routes_are_restricted()
    {
        var controller = typeof(AdminWalletFundingController);
        var classRole = Assert.Single(controller.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("Admin,SuperAdmin,Operations", classRole.Roles);
        foreach (var method in new[] { nameof(AdminWalletFundingController.Approve), nameof(AdminWalletFundingController.Reject) })
        {
            var endpoint = controller.GetMethod(method) ?? throw new InvalidOperationException($"Missing {method} endpoint.");
            var attribute = Assert.Single(endpoint.GetCustomAttributes<AuthorizeAttribute>());
            Assert.Equal("Admin,SuperAdmin", attribute.Roles);
        }
        var legacy = typeof(AdminBusinessWalletsController);
        foreach (var method in new[] { nameof(AdminBusinessWalletsController.Credit), nameof(AdminBusinessWalletsController.Adjust), nameof(AdminBusinessWalletsController.ReverseLedgerTransaction) })
        {
            var endpoint = legacy.GetMethod(method) ?? throw new InvalidOperationException($"Missing {method} endpoint.");
            Assert.Equal("Admin,SuperAdmin", Assert.Single(endpoint.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        }
    }

    private static (FinancialAccount House, FinancialAccount Wallet) SeedPair(
        AppDbContext db, FinancialAccountOwnerType ownerType, Guid ownerId,
        string assetCode, decimal houseBalance, decimal walletBalance)
    {
        var house = new FinancialAccount
        {
            OwnerType = FinancialAccountOwnerType.Treasury,
            OwnerId = Guid.NewGuid(),
            AccountType = FinancialAccountType.House,
            AccountCode = $"KX-W12-CI-H-{Guid.NewGuid():N}",
            AssetCode = assetCode, Status = FinancialAccountStatus.Active,
            SettledBalance = houseBalance, AvailableBalance = houseBalance,
            HeldBalance = 0m
        };
        var wallet = new FinancialAccount
        {
            OwnerType = ownerType, OwnerId = ownerId,
            AccountType = FinancialAccountType.Customer,
            AccountCode = $"KX-W12-CI-W-{Guid.NewGuid():N}",
            AssetCode = assetCode, Status = FinancialAccountStatus.Active,
            SettledBalance = walletBalance, AvailableBalance = walletBalance,
            HeldBalance = 0m
        };
        db.FinancialAccounts.AddRange(house, wallet);
        return (house, wallet);
    }
}
