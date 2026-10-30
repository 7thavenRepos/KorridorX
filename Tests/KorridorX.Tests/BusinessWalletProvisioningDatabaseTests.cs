using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.BusinessFunding;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Services.BusinessFunding;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

// The shared fixture DROPS the public schema. Run only in CI's disposable PostgreSQL.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessWalletProvisioningDatabaseTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task New_business_wallet_is_tenant_owned_idempotent_audited_and_zero_balance()
    {
        using var client = fixture.CreateClient();
        var registration = await client.RegisterAndConfirmAsync(
            fixture.Factory.Services,
            new RegisterRequestDto("Wallet", "Business",
                $"g1-business-{Guid.NewGuid():N}@example.test", "ReleaseCandidate!123",
                "+2348012345678", "NG", UserType.Business));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<BusinessWalletProvisioningService>();
        var business = new BusinessProfile
        {
            OwnerUserId = registration.UserId,
            BusinessName = "G1 Wallet CI Business",
            CountryCode = "NG",
            KybStatus = KybStatus.Approved
        };
        db.BusinessProfiles.Add(business);
        // Separate, test-only fiat master data keeps this case independent of
        // other acceptance tests that may temporarily disable standard assets.
        const string assetCode = "QXZ";
        if (!await db.Assets.AnyAsync(x => x.Code == assetCode))
            db.Assets.Add(new Asset { Code = assetCode, Name = "CI Test Fiat", Symbol = "T",
                Type = AssetType.Fiat, IsSupported = true, BusinessEnabled = true, DepositEnabled = true });
        if (!await db.CountryAssets.AnyAsync(x => x.CountryCode == "NG" && x.AssetCode == assetCode))
            db.CountryAssets.Add(new CountryAsset { CountryCode = "NG", AssetCode = assetCode, CanDeposit = true });
        await db.SaveChangesAsync();

        var beforeLedger = await db.LedgerTransactions.CountAsync();
        var beforeProviderCalls = await db.ProviderRequestLogs.CountAsync();
        var available = await service.GetAvailableAssetsAsync(registration.UserId);
        Assert.Contains(available, x => x.AssetCode == assetCode && !x.HasWallet && !x.IsReserved);

        var first = await service.CreateAsync(registration.UserId, new CreateBusinessWalletRequestDto(" qxz "));
        var second = await service.CreateAsync(registration.UserId, new CreateBusinessWalletRequestDto("QXZ"));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(business.Id, first.BusinessProfileId);
        Assert.Equal(0m, first.SettledBalance);
        Assert.Equal(0m, first.AvailableBalance);
        Assert.Equal(0m, first.HeldBalance);
        Assert.Equal(1, await db.FinancialAccounts.CountAsync(x =>
            x.OwnerType == FinancialAccountOwnerType.Business && x.OwnerId == business.Id &&
            x.AssetCode == assetCode && x.AccountType == FinancialAccountType.Customer));
        Assert.Equal(beforeLedger, await db.LedgerTransactions.CountAsync());
        Assert.Equal(beforeProviderCalls, await db.ProviderRequestLogs.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(x =>
            x.EntityName == nameof(FinancialAccount) && x.EntityId == first.Id.ToString("D") &&
            x.Action == "BUSINESS_WALLET_CREATED"));

        var after = await service.GetAvailableAssetsAsync(registration.UserId);
        Assert.Contains(after, x => x.AssetCode == assetCode && x.HasWallet);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(registration.UserId, new CreateBusinessWalletRequestDto("USDT")));
    }
}
