using KorridorX.Data;
using KorridorX.Dtos.Auth;
using KorridorX.Dtos.Wallets;
using KorridorX.Models.Enums;
using KorridorX.Services.Wallets;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class ConsumerWalletWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public ConsumerWalletWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Consumer_can_add_supported_fiat_wallet_idempotently()
    {
        var mapping = await FindEligibleMappingAsync();

        using var client = _fixture.CreateClient();
        var (registration, _) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Wallet",
                "Consumer",
                $"wallet-{Guid.NewGuid():N}@example.test",
                "ReleaseCandidate!123",
                "+12145550101",
                mapping.CountryCode,
                UserType.Consumer));

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wallets = scope.ServiceProvider.GetRequiredService<IConsumerWalletService>();

        var before = await wallets.GetAvailableAssetsAsync(registration.UserId);
        var candidate = Assert.Single(
            before.Where(x => x.AssetCode == mapping.AssetCode));
        Assert.False(candidate.IsAdded);

        var first = await wallets.CreateWalletAsync(
            registration.UserId,
            new CreateConsumerWalletRequestDto(
                mapping.AssetCode.ToLowerInvariant()));

        var second = await wallets.CreateWalletAsync(
            registration.UserId,
            new CreateConsumerWalletRequestDto(mapping.AssetCode));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(mapping.AssetCode, first.AssetCode);
        Assert.Equal(0m, first.AvailableBalance);
        Assert.Equal(0m, first.SettledBalance);
        Assert.Equal(0m, first.HeldBalance);
        Assert.Equal(FinancialAccountStatus.Active.ToString(), first.Status);

        db.ChangeTracker.Clear();

        Assert.Equal(
            1,
            await db.FinancialAccounts.CountAsync(x =>
                x.OwnerType == FinancialAccountOwnerType.User &&
                x.OwnerId == registration.UserId &&
                x.AccountType == FinancialAccountType.Customer &&
                x.AssetCode == mapping.AssetCode &&
                !x.IsDeleted));

        var listed = await wallets.GetWalletsAsync(registration.UserId);
        Assert.Contains(
            listed,
            x => x.Id == first.Id && x.AssetCode == mapping.AssetCode);

        var after = await wallets.GetAvailableAssetsAsync(registration.UserId);
        Assert.True(
            Assert.Single(after.Where(x => x.AssetCode == mapping.AssetCode))
                .IsAdded);
    }

    [DatabaseIntegrationFact]
    public async Task Consumer_cannot_create_unconfigured_wallet()
    {
        var mapping = await FindEligibleMappingAsync();

        using var client = _fixture.CreateClient();
        var (registration, _) = await client.RegisterConfirmAndLoginAsync(
            _fixture.Factory.Services,
            new RegisterRequestDto(
                "Wallet",
                "Boundary",
                $"wallet-boundary-{Guid.NewGuid():N}@example.test",
                "ReleaseCandidate!123",
                "+12145550102",
                mapping.CountryCode,
                UserType.Consumer));

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var wallets = scope.ServiceProvider.GetRequiredService<IConsumerWalletService>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => wallets.CreateWalletAsync(
                registration.UserId,
                new CreateConsumerWalletRequestDto("ZZZ")));

        Assert.Contains(
            "not available",
            error.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(string CountryCode, string AssetCode)> FindEligibleMappingAsync()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mapping = await db.CountryAssets
            .AsNoTracking()
            .Include(x => x.Country)
            .Include(x => x.Asset)
            .Where(x =>
                x.Country.IsSupported &&
                x.Asset.IsSupported &&
                x.Asset.Type == AssetType.Fiat &&
                (x.CanDeposit ||
                 x.CanWithdraw ||
                 x.CanSend ||
                 x.CanReceive ||
                 x.CanTrade ||
                 x.CanUseInstant))
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CountryCode)
            .ThenBy(x => x.AssetCode)
            .Select(x => new { x.CountryCode, x.AssetCode })
            .FirstAsync();

        return (mapping.CountryCode, mapping.AssetCode);
    }
}
