using KorridorX.Data;
using KorridorX.Dtos.Treasury;
using KorridorX.Models.Enums;
using KorridorX.Models.FinancialCore;
using KorridorX.Models.Lookups;
using KorridorX.Models.Treasury;
using KorridorX.Services.Treasury;
using KorridorX.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KorridorX.Tests;

[Collection(ReleaseCandidateDatabaseCollection.Name)]
public sealed class UnifiedTreasuryLiquidityWorkflowTests
{
    private readonly ReleaseCandidateDatabaseFixture _fixture;

    public UnifiedTreasuryLiquidityWorkflowTests(ReleaseCandidateDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [DatabaseIntegrationFact]
    public async Task Internal_thresholds_drive_house_liquidity_status_and_treasury_to_house_suggestion()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var assetCode = await CreateAssetAsync(db, "TLH");

        var treasury = await CreateAccountAsync(
            db, assetCode, FinancialAccountOwnerType.Treasury,
            FinancialAccountType.Treasury, 200m);

        var house = await CreateAccountAsync(
            db, assetCode, FinancialAccountOwnerType.Platform,
            FinancialAccountType.House, 20m);

        await service.UpsertThresholdAsync(
            Guid.Empty,
            new UpsertLiquidityThresholdRequestDto
            {
                ScopeType = TreasuryLiquidityScopeType.FinancialAccount,
                ProviderCode = "INTERNAL",
                CurrencyCode = assetCode,
                FinancialAccountType = FinancialAccountType.Treasury,
                MinimumBalance = 50m,
                TargetBalance = 100m,
                MaximumBalance = 250m,
                IsActive = true
            });

        await service.UpsertThresholdAsync(
            Guid.Empty,
            new UpsertLiquidityThresholdRequestDto
            {
                ScopeType = TreasuryLiquidityScopeType.FinancialAccount,
                ProviderCode = "INTERNAL",
                CurrencyCode = assetCode,
                FinancialAccountType = FinancialAccountType.House,
                MinimumBalance = 30m,
                TargetBalance = 80m,
                MaximumBalance = 150m,
                IsActive = true
            });

        var positions = await service.GetLiquidityPositionsAsync();

        var housePosition = Assert.Single(
            positions,
            x => x.SourceId == house.Id);

        Assert.Equal(LiquidityPositionStatus.Low, housePosition.LiquidityStatus);
        Assert.Equal(80m, housePosition.TargetBalance);

        var treasuryPosition = Assert.Single(
            positions,
            x => x.SourceId == treasury.Id);

        Assert.Equal(LiquidityPositionStatus.AboveTarget, treasuryPosition.LiquidityStatus);

        var suggestions = await service.GetUnifiedRebalanceSuggestionsAsync();

        var suggestion = Assert.Single(
            suggestions,
            x =>
                x.ActionType == TreasuryLiquidityActionType.InternalTransfer &&
                x.FromFinancialAccountId == treasury.Id &&
                x.ToFinancialAccountId == house.Id);

        Assert.Equal(60m, suggestion.SuggestedAmount);
        Assert.Equal(assetCode, suggestion.AssetCode);
    }

    [DatabaseIntegrationFact]
    public async Task Provider_thresholds_create_topup_and_sweep_recommendations_with_network_context()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var assetCode = await CreateAssetAsync(db, "TLP");

        var treasury = await CreateAccountAsync(
            db, assetCode, FinancialAccountOwnerType.Treasury,
            FinancialAccountType.Treasury, 300m);

        await service.UpsertThresholdAsync(
            Guid.Empty,
            new UpsertLiquidityThresholdRequestDto
            {
                ScopeType = TreasuryLiquidityScopeType.FinancialAccount,
                ProviderCode = "INTERNAL",
                CurrencyCode = assetCode,
                FinancialAccountType = FinancialAccountType.Treasury,
                MinimumBalance = 50m,
                TargetBalance = 100m,
                MaximumBalance = 500m,
                IsActive = true
            });

        var lowWallet = new ProviderWalletBalance
        {
            ProviderCode = "TESTPROVIDER",
            ProviderWalletId = $"LOW-{Guid.NewGuid():N}",
            CurrencyCode = assetCode,
            NetworkCode = "TESTNET",
            Balance = 20m,
            IsActive = true,
            LastSyncedAt = DateTime.UtcNow
        };

        var highWallet = new ProviderWalletBalance
        {
            ProviderCode = "TESTPROVIDER",
            ProviderWalletId = $"HIGH-{Guid.NewGuid():N}",
            CurrencyCode = assetCode,
            NetworkCode = "TESTNET2",
            Balance = 180m,
            IsActive = true,
            LastSyncedAt = DateTime.UtcNow
        };

        db.ProviderWalletBalances.AddRange(lowWallet, highWallet);
        await db.SaveChangesAsync();

        await service.UpsertThresholdAsync(
            Guid.Empty,
            new UpsertLiquidityThresholdRequestDto
            {
                ScopeType = TreasuryLiquidityScopeType.ProviderWallet,
                ProviderCode = "TESTPROVIDER",
                CurrencyCode = assetCode,
                NetworkCode = "TESTNET",
                MinimumBalance = 40m,
                TargetBalance = 80m,
                MaximumBalance = 120m,
                IsActive = true
            });

        await service.UpsertThresholdAsync(
            Guid.Empty,
            new UpsertLiquidityThresholdRequestDto
            {
                ScopeType = TreasuryLiquidityScopeType.ProviderWallet,
                ProviderCode = "TESTPROVIDER",
                CurrencyCode = assetCode,
                NetworkCode = "TESTNET2",
                MinimumBalance = 40m,
                TargetBalance = 80m,
                MaximumBalance = 120m,
                IsActive = true
            });

        var suggestions = await service.GetUnifiedRebalanceSuggestionsAsync();

        var topUp = Assert.Single(
            suggestions,
            x =>
                x.ActionType == TreasuryLiquidityActionType.ProviderTopUp &&
                x.ProviderWalletBalanceId == lowWallet.Id);

        Assert.Equal(60m, topUp.SuggestedAmount);
        Assert.Equal(treasury.Id, topUp.FromFinancialAccountId);
        Assert.Equal("TESTNET", topUp.NetworkCode);

        var sweep = Assert.Single(
            suggestions,
            x =>
                x.ActionType == TreasuryLiquidityActionType.ProviderSweep &&
                x.ProviderWalletBalanceId == highWallet.Id);

        Assert.Equal(100m, sweep.SuggestedAmount);
        Assert.Equal("TESTNET2", sweep.NetworkCode);
    }

    [DatabaseIntegrationFact]
    public async Task Internal_liquidity_transfer_is_atomic_and_idempotent()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var assetCode = await CreateAssetAsync(db, "TLX");

        var source = await CreateAccountAsync(
            db, assetCode, FinancialAccountOwnerType.Treasury,
            FinancialAccountType.Treasury, 150m);

        var destination = await CreateAccountAsync(
            db, assetCode, FinancialAccountOwnerType.Platform,
            FinancialAccountType.House, 25m);

        var request = new ExecuteInternalLiquidityTransferRequestDto
        {
            FromFinancialAccountId = source.Id,
            ToFinancialAccountId = destination.Id,
            Amount = 40m,
            IdempotencyKey = $"liq-{Guid.NewGuid():N}",
            Reason = "House liquidity top-up."
        };

        var first = await service.ExecuteInternalLiquidityTransferAsync(
            Guid.Empty,
            request);

        var second = await service.ExecuteInternalLiquidityTransferAsync(
            Guid.Empty,
            request);

        Assert.Equal(first.LedgerTransactionId, second.LedgerTransactionId);

        db.ChangeTracker.Clear();

        var sourceAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == source.Id);

        var destinationAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == destination.Id);

        Assert.Equal(110m, sourceAfter.AvailableBalance);
        Assert.Equal(110m, sourceAfter.SettledBalance);
        Assert.Equal(65m, destinationAfter.AvailableBalance);
        Assert.Equal(65m, destinationAfter.SettledBalance);

        var ledger = await db.LedgerTransactions.AsNoTracking()
            .Include(x => x.Postings)
            .SingleAsync(x => x.Id == first.LedgerTransactionId);

        Assert.Equal(LedgerTransactionType.Treasury, ledger.Type);
        Assert.Equal(40m, ledger.Amount);
        Assert.Equal(2, ledger.Postings.Count);

        Assert.Equal(
            1,
            await db.LedgerTransactions.CountAsync(x =>
                x.IdempotencyScope == "TreasuryInternalLiquidityTransfer" &&
                x.IdempotencyKey == request.IdempotencyKey));
    }

    [DatabaseIntegrationFact]
    public async Task Internal_liquidity_transfer_rejects_cross_asset_movement()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var assetOne = await CreateAssetAsync(db, "TLA");
        var assetTwo = await CreateAssetAsync(db, "TLB");

        var source = await CreateAccountAsync(
            db, assetOne, FinancialAccountOwnerType.Treasury,
            FinancialAccountType.Treasury, 100m);

        var destination = await CreateAccountAsync(
            db, assetTwo, FinancialAccountOwnerType.Platform,
            FinancialAccountType.House, 10m);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteInternalLiquidityTransferAsync(
                Guid.Empty,
                new ExecuteInternalLiquidityTransferRequestDto
                {
                    FromFinancialAccountId = source.Id,
                    ToFinancialAccountId = destination.Id,
                    Amount = 10m,
                    IdempotencyKey = $"cross-{Guid.NewGuid():N}",
                    Reason = "Invalid cross-asset move."
                }));

        Assert.Contains("same asset", ex.Message, StringComparison.OrdinalIgnoreCase);

        db.ChangeTracker.Clear();

        var sourceAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == source.Id);

        var destinationAfter = await db.FinancialAccounts.AsNoTracking()
            .SingleAsync(x => x.Id == destination.Id);

        Assert.Equal(100m, sourceAfter.AvailableBalance);
        Assert.Equal(10m, destinationAfter.AvailableBalance);
    }

    [DatabaseIntegrationFact]
    public async Task Exposure_and_alerts_keep_internal_and_external_liquidity_separate()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var assetCode = await CreateAssetAsync(db, "TLE");

        await CreateAccountAsync(
            db, assetCode, FinancialAccountOwnerType.Platform,
            FinancialAccountType.House, 30m);

        await CreateAccountAsync(
            db, assetCode, FinancialAccountOwnerType.Treasury,
            FinancialAccountType.Treasury, 70m);

        var provider = new ProviderWalletBalance
        {
            ProviderCode = "TESTPROVIDER",
            ProviderWalletId = $"EXT-{Guid.NewGuid():N}",
            CurrencyCode = assetCode,
            Balance = 50m,
            IsActive = true,
            LastSyncedAt = DateTime.UtcNow.AddHours(-2)
        };

        db.ProviderWalletBalances.Add(provider);
        await db.SaveChangesAsync();

        await service.UpsertThresholdAsync(
            Guid.Empty,
            new UpsertLiquidityThresholdRequestDto
            {
                ScopeType = TreasuryLiquidityScopeType.ProviderWallet,
                ProviderCode = "TESTPROVIDER",
                CurrencyCode = assetCode,
                MinimumBalance = 60m,
                TargetBalance = 80m,
                MaximumBalance = 120m,
                IsActive = true
            });

        var exposure = Assert.Single(
            await service.GetAssetExposureAsync(),
            x => x.AssetCode == assetCode);

        Assert.Equal(30m, exposure.HouseAvailable);
        Assert.Equal(70m, exposure.TreasuryAvailable);
        Assert.Equal(50m, exposure.ExternalProviderBalance);
        Assert.Equal(150m, exposure.ObservedLiquidity);

        var alerts = await service.GetLiquidityAlertsAsync();

        Assert.Contains(
            alerts,
            x =>
                x.SourceId == provider.Id &&
                x.AssetCode == assetCode &&
                x.Severity == TreasuryLiquidityAlertSeverity.Warning);
    }

    private static async Task<string> CreateAssetAsync(
        AppDbContext db,
        string prefix)
    {
        var assetCode = $"{prefix}{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        db.Assets.Add(new Asset
        {
            Code = assetCode,
            Name = $"Treasury Test Asset {assetCode}",
            Symbol = assetCode,
            Type = AssetType.Crypto,
            DecimalPlaces = 6,
            IsStablecoin = true,
            IsSupported = true,
            DepositEnabled = true,
            WithdrawalEnabled = true,
            TradingEnabled = true,
            InstantEnabled = true
        });

        await db.SaveChangesAsync();
        return assetCode;
    }

    private static async Task<FinancialAccount> CreateAccountAsync(
        AppDbContext db,
        string assetCode,
        FinancialAccountOwnerType ownerType,
        FinancialAccountType accountType,
        decimal balance)
    {
        var account = new FinancialAccount
        {
            OwnerType = ownerType,
            OwnerId = Guid.NewGuid(),
            AccountCode = TestReference($"{accountType}-{assetCode}"),
            AssetCode = assetCode,
            AccountType = accountType,
            Status = FinancialAccountStatus.Active,
            SettledBalance = balance,
            AvailableBalance = balance,
            HeldBalance = 0m
        };

        db.FinancialAccounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private static string TestReference(string prefix)
    {
        var value = $"{prefix}-{Guid.NewGuid():N}";
        return value.Length <= 60 ? value : value[..60];
    }
}
