using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.BusinessBeneficiaries;
using KorridorX.Models.BusinessBeneficiaries;
using KorridorX.Models.Customers;
using KorridorX.Models.Enums;
using KorridorX.Models.Providers;
using KorridorX.Services.BusinessBeneficiaries;
using KorridorX.Services.Providers;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

// IMPORTANT: This fixture DROPS the public schema. Never run it against staging,
// production, or the user's local RC database. CI uses disposable PostgreSQL.
[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class BusinessBeneficiaryDestinationsDatabaseTests(ReleaseCandidateDatabaseFixture fixture)
{
    [DatabaseIntegrationFact]
    public async Task New_bank_and_mobile_wallet_destinations_insert_and_default_replacement_succeeds()
    {
        using var client = fixture.CreateClient();
        var registration = await client.RegisterAndConfirmAsync(
            fixture.Factory.Services,
            new RegisterRequestDto("Bank", "Tester",
                $"g5-bank-{Guid.NewGuid():N}@example.test", "ReleaseCandidate!123",
                "+2348012345678", "NG", UserType.Business));

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IBusinessBeneficiaryService>();
        var business = new BusinessProfile {
            OwnerUserId = registration.UserId, CountryCode = "NG", BusinessName = "G5 Bank CI"
        };
        var beneficiary = new BusinessBeneficiary {
            BusinessProfileId = business.Id, CountryCode = "NG", Name = "CI Beneficiary"
        };
        var providerBank = new ProviderBank {
            ProviderCode = ProviderCode.Blaaiz,
            ProviderBankId = "G5-CI-" + Guid.NewGuid().ToString("N"),
            Name = "CI Example Bank", Code = "012345", CountryCode = "NG", IsActive = true
        };
        db.BusinessProfiles.Add(business);
        db.BusinessBeneficiaries.Add(beneficiary);
        db.ProviderBanks.Add(providerBank);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var beforeLedger = await db.LedgerTransactions.CountAsync();
        var beforeProviderRequests = await db.ProviderRequestLogs.CountAsync();

        var firstRequest = new AddBusinessBeneficiaryBankAccountRequestDto(
            "NG", "NGN", providerBank.Name, providerBank.Code, null,
            "CI Account Holder", "0000000001", null, null, null, null,
            providerBank.ProviderBankId, true);
        var first = await service.AddBankAccountAsync(registration.UserId, beneficiary.Id, firstRequest);
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.Equal(beneficiary.Id, first.BusinessBeneficiaryId);
        Assert.Equal(providerBank.ProviderBankId, first.ProviderBankId);
        Assert.True(first.IsDefault);
        Assert.False(first.IsVerified); // Creating a destination does not verify the account.

        var second = await service.AddBankAccountAsync(registration.UserId, beneficiary.Id,
            firstRequest with { AccountNumber = "0000000002" });
        Assert.NotEqual(first.Id, second.Id);
        Assert.True(second.IsDefault);

        var wallet = await service.AddMobileWalletAsync(registration.UserId, beneficiary.Id,
            new AddBusinessBeneficiaryMobileWalletRequestDto(
                "NG", "NGN", "CI Mobile Wallet", "08000000000", "CI Account Holder", true));
        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.True(wallet.IsDefault);
        Assert.False(wallet.IsVerified);

        db.ChangeTracker.Clear();
        var savedBanks = await db.BusinessBeneficiaryBankAccounts.AsNoTracking()
            .Where(x => x.BusinessBeneficiaryId == beneficiary.Id && !x.IsDeleted)
            .ToListAsync();
        Assert.Equal(2, savedBanks.Count);
        Assert.Single(savedBanks.Where(x => x.IsDefault));
        Assert.Contains(savedBanks, x => x.Id == first.Id && !x.IsDefault);
        Assert.Contains(savedBanks, x => x.Id == second.Id && x.IsDefault);
        Assert.Single(await db.BusinessBeneficiaryMobileWallets.AsNoTracking()
            .Where(x => x.BusinessBeneficiaryId == beneficiary.Id && !x.IsDeleted)
            .ToListAsync());
        Assert.Equal(beforeLedger, await db.LedgerTransactions.CountAsync());
        Assert.Equal(beforeProviderRequests, await db.ProviderRequestLogs.CountAsync());
    }

    [DatabaseIntegrationFact]
    public async Task Directory_pagination_is_complete_with_multiple_banks_of_the_same_name()
    {
        using var client = fixture.CreateClient();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var directory = scope.ServiceProvider.GetRequiredService<IBankDirectoryService>();
        var uniqueCountry = "G" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant(); // test-only isolated country code
        var names = Enumerable.Range(1, 105)
            .Select(i => new ProviderBank {
                ProviderCode = ProviderCode.Blaaiz,
                ProviderBankId = $"G5-PAGE-{Guid.NewGuid():N}-{i}",
                CountryCode = uniqueCountry, Name = "Same Test Bank", Code = i.ToString("D4"),
                IsActive = true
            }).ToList();
        db.ProviderBanks.AddRange(names);
        await db.SaveChangesAsync();

        var page1 = await directory.GetBanksAsync(uniqueCountry, null, false, 1, 100);
        var page2 = await directory.GetBanksAsync(uniqueCountry, null, false, 2, 100);
        Assert.Equal(105, page1.Meta.TotalItems);
        Assert.Equal(105, page2.Meta.TotalItems);
        Assert.Equal(100, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        var ids = page1.Items.Concat(page2.Items).Select(x => x.Id).ToArray();
        Assert.Equal(105, ids.Distinct().Count());
    }
}
