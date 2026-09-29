using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Wallets;
using KorridorX.Models.Enums;
using KorridorX.Services.Payments;
using KorridorX.Services.Wallets;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ConsumerWalletFundingWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ConsumerWalletFundingWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Successful_account_funding_collection_credits_wallet_once()
    {
        await using var lookupScope = _fixture.Factory.Services.CreateAsyncScope();
        var lookupDb = lookupScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var route = await lookupDb.CountryAssets
            .AsNoTracking()
            .Include(x => x.Country)
            .Include(x => x.Asset)
            .Where(x =>
                x.CanDeposit &&
                x.Country.IsSupported &&
                x.Asset.IsSupported &&
                x.Asset.Type == AssetType.Fiat &&
                ((x.CountryCode == "CA" && x.AssetCode == "CAD") ||
                 (x.CountryCode == "US" && x.AssetCode == "USD")))
            .OrderBy(x => x.CountryCode)
            .Select(x => new { x.CountryCode, x.AssetCode })
            .FirstAsync();

        using var client = _fixture.CreateClient();
        var (registration, _) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Wallet",
                "Funding",
                $"wallet-funding-{Guid.NewGuid():N}@example.test",
                "ReleaseCandidate!123",
                "+12145550155",
                route.CountryCode,
                UserType.Consumer));

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wallets = scope.ServiceProvider.GetRequiredService<IConsumerWalletService>();
        var funding = scope.ServiceProvider.GetRequiredService<IConsumerWalletFundingService>();
        var statuses = scope.ServiceProvider.GetRequiredService<ICollectionStatusService>();

        var wallet = await wallets.CreateWalletAsync(
            registration.UserId,
            new CreateConsumerWalletRequestDto(route.AssetCode));

        var methods = await funding.GetFundingMethodsAsync(registration.UserId, wallet.Id);
        var method = Assert.Single(methods).PaymentMethod;

        var created = await funding.CreateCollectionAsync(
            registration.UserId,
            wallet.Id,
            new CreateConsumerWalletFundingCollectionRequestDto(25m, method));

        var collection = await db.Collections
            .Include(x => x.Attempts)
            .SingleAsync(x => x.Id == created.Collection.Id);

        await statuses.ApplyTransitionAsync(
            collection,
            CollectionStatus.Initiated,
            new CollectionStatusTransitionContext(
                Source: "Test",
                ChangedByUserId: registration.UserId));

        await statuses.ApplyTransitionAsync(
            collection,
            CollectionStatus.Successful,
            new CollectionStatusTransitionContext(
                Source: "Test",
                ChangedByUserId: registration.UserId));

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var funded = await db.FinancialAccounts
            .AsNoTracking()
            .SingleAsync(x => x.Id == wallet.Id);

        Assert.Equal(25m, funded.SettledBalance);
        Assert.Equal(25m, funded.AvailableBalance);
        Assert.Equal(0m, funded.HeldBalance);

        Assert.Equal(
            1,
            await db.LedgerTransactions.CountAsync(x =>
                x.Type == LedgerTransactionType.ExternalCollectionCredit &&
                x.RelatedEntityType == "Collection" &&
                x.RelatedEntityId == collection.Id &&
                !x.IsDeleted));
    }
}
