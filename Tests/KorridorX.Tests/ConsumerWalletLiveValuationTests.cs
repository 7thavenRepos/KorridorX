using KorridorX.Configuration;
using KorridorX.Dtos.Wallets;
using KorridorX.Models.Fx;
using KorridorX.Services.Fx;
using KorridorX.Services.Wallets;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorridorX.Tests;

public sealed class ConsumerWalletLiveValuationTests
{
    [Fact]
    public async Task Every_valuation_reads_each_nonzero_direct_pair_and_never_reuses_a_previous_success()
    {
        var wallets = new Wallets([Wallet("CAD", 10m), Wallet("GBP", 2m), Wallet("JPY", 0m)]);
        var live = new LiveRates { CustomerRate = 1.25m };
        var service = Service(wallets, live);
        var first = await service.GetValuationAsync(Guid.NewGuid(), "cad");
        Assert.Equal(12.50m, first.EstimatedAvailableBalance);
        Assert.Equal("ZeroBalance", first.Components[2].Status);
        Assert.Equal(("GBP", "CAD", FxRateUse.Valuation), Assert.Single(live.Calls));

        live.CustomerRate = 1.50m;
        var second = await service.GetValuationAsync(Guid.NewGuid(), "CAD");
        Assert.Equal(13m, second.EstimatedAvailableBalance);
        Assert.Equal(2, live.Calls.Count);
        Assert.NotEqual(first.Components[1].ExchangeRateId, second.Components[1].ExchangeRateId);

        live.Unavailable = true;
        var failed = await service.GetValuationAsync(Guid.NewGuid(), "CAD");
        Assert.Equal("Unavailable", failed.Status);
        Assert.Null(failed.EstimatedAvailableBalance);
        Assert.Equal(10m, failed.Components[0].EstimatedBaseAmount);
        Assert.Equal("MissingRate", failed.Components[1].Status);
        Assert.Null(failed.Components[1].CustomerRate);
        Assert.Equal(3, live.Calls.Count);
        Assert.Equal(2m, wallets.Rows[1].AvailableBalance);
        Assert.Equal(20m, wallets.Rows[1].HeldBalance);
    }

    [Fact]
    public async Task Native_and_zero_balances_need_no_FX_provider_access()
    {
        var live = new LiveRates { Unavailable = true };
        var service = Service(new Wallets([Wallet("CAD", 10m), Wallet("GBP", 0m)]), live);
        var result = await service.GetValuationAsync(Guid.NewGuid(), "CAD");
        Assert.Equal(10m, result.EstimatedAvailableBalance);
        Assert.Empty(live.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_during_live_read_propagates_instead_of_returning_an_unpriced_total()
    {
        using var canceled = new CancellationTokenSource();
        var live = new LiveRates { BeforeRead = canceled.Cancel };
        var service = Service(new Wallets([Wallet("CAD", 10m), Wallet("GBP", 2m)]), live);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetValuationAsync(Guid.NewGuid(), "CAD", canceled.Token));
        Assert.Single(live.Calls);
    }

    private static ConsumerWalletValuationService Service(Wallets wallets, LiveRates live) =>
        new(live, wallets, Options.Create(new TreasuryOptions { FxRateStaleMinutes = 60 }));

    private static ConsumerWalletDto Wallet(string code, decimal available) =>
        new(Guid.NewGuid(), code, code, code, 2, "Active", available + 20m, available, 20m,
            code == "CAD", true, true, true, true, false, false, DateTime.UtcNow);

    private sealed class Wallets(IReadOnlyList<ConsumerWalletDto> rows) : IConsumerWalletService
    {
        public IReadOnlyList<ConsumerWalletDto> Rows => rows;
        public Task<IReadOnlyList<ConsumerWalletDto>> GetWalletsAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(rows);
        public Task<IReadOnlyList<AvailableConsumerWalletAssetDto>> GetAvailableAssetsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AvailableConsumerWalletAssetDto>>(rows.Select(x => new AvailableConsumerWalletAssetDto(
                x.AssetCode, x.AssetName, x.Symbol, x.DecimalPlaces, x.IsDefault,
                x.CanDeposit, x.CanWithdraw, x.CanSend, x.CanReceive, x.CanTrade, x.CanUseInstant, true)).ToArray());
        public Task<ConsumerWalletDto> CreateWalletAsync(Guid userId, CreateConsumerWalletRequestDto request, CancellationToken ct = default) =>
            throw new NotSupportedException("Valuation must not create or change a wallet.");
    }

    private sealed class LiveRates : ILiveFxRateService
    {
        public decimal CustomerRate { get; set; } = 1m;
        public bool Unavailable { get; set; }
        public Action? BeforeRead { get; set; }
        public List<(string Source, string Destination, FxRateUse Use)> Calls { get; } = [];
        public Task<T> UseFreshRateAsync<T>(string source, string destination, FxRateUse use,
            Func<ExchangeRate, Guid, CancellationToken, Task<T>> consume, CancellationToken ct = default)
        {
            Calls.Add((source, destination, use));
            BeforeRead?.Invoke();
            ct.ThrowIfCancellationRequested();
            if (Unavailable) throw new InvalidOperationException("A fresh FX rate is unavailable.");
            return consume(new ExchangeRate
            {
                SourceCurrencyCode = source, DestinationCurrencyCode = destination,
                ProviderRate = CustomerRate, CustomerRate = CustomerRate, EffectiveFrom = DateTime.UtcNow
            }, Guid.NewGuid(), ct);
        }
    }
}
