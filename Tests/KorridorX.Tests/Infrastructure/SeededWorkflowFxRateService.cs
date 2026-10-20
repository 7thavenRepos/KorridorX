using KorridorX.Configuration;
using KorridorX.Data;
using KorridorX.Models.Enums;
using KorridorX.Models.Fx;
using KorridorX.Models.Marketplace;
using KorridorX.Services.Fx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KorridorX.Tests.Infrastructure;

/// <summary>
/// Test-only adapter for existing workflow fixtures that seed ExchangeRates.
/// It never contacts a provider and is registered only by the test application factory.
/// Live pricing, connection checks and freshness are exercised with the real FX service
/// in dedicated tests, not by this adapter.
/// </summary>
public sealed class SeededWorkflowFxRateService(
    AppDbContext db,
    IOptions<TreasuryOptions> treasuryOptions) : ILiveFxRateService
{
    public async Task<T> UseFreshRateAsync<T>(
        string source,
        string destination,
        FxRateUse use,
        Func<ExchangeRate, Guid, CancellationToken, Task<T>> consume,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rate = await db.ExchangeRates.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive &&
                x.SourceCurrencyCode == source && x.DestinationCurrencyCode == destination &&
                x.EffectiveFrom <= now && (x.EffectiveTo == null || x.EffectiveTo > now))
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No seeded workflow FX rate is available for this pair.");

        // Preserve the pre-existing Instant workflow's stale-rate rejection in fixtures.
        if (use == FxRateUse.Instant && rate.EffectiveFrom < now.AddMinutes(-treasuryOptions.Value.FxRateStaleMinutes))
            throw new InvalidOperationException("The current instant FX rate is stale.");

        var instantPairId = await db.InstantPairs.AsNoTracking()
            .Where(x => !x.IsDeleted && x.SourceAssetCode == source && x.DestinationAssetCode == destination)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        Guid? marketplacePairId = null;
        if (!instantPairId.HasValue)
        {
            marketplacePairId = await db.MarketplacePairs.AsNoTracking()
                .Where(x => !x.IsDeleted && x.BaseAssetCode == source && x.QuoteAssetCode == destination)
                .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            if (!marketplacePairId.HasValue)
            {
                // Satisfy provenance FKs without adding a visible or tradable catalogue pair.
                var fixturePair = new MarketplacePair
                {
                    Code = $"SEEDED-FX-{Guid.NewGuid():N}",
                    BaseAssetCode = source,
                    QuoteAssetCode = destination,
                    Status = MarketplacePairStatus.Paused,
                    IsDeleted = true,
                    DeletedAt = now,
                    MinimumOrderQuantity = 1m,
                    QuantityIncrement = 1m,
                    PriceIncrement = 0.01m
                };
                db.MarketplacePairs.Add(fixturePair);
                marketplacePairId = fixturePair.Id;
            }
        }

        var configuration = new FxProviderRateConfiguration
        {
            InstantPairId = instantPairId,
            MarketplacePairId = marketplacePairId,
            SourceCurrencyCode = source,
            DestinationCurrencyCode = destination,
            ProviderCode = rate.ProviderCode,
            ProviderRateId = rate.ProviderRateId,
            IsActive = false,
            ScheduleMode = "Disabled"
        };
        var observation = new FxRateObservation
        {
            ConfigurationId = configuration.Id,
            ConfigurationRevision = configuration.Revision,
            ProviderCode = rate.ProviderCode,
            ProviderRateId = rate.ProviderRateId ?? $"seeded-{rate.Id:N}",
            ConnectionKey = "SEEDED_WORKFLOW_TEST_ONLY",
            SourceCurrencyCode = source,
            DestinationCurrencyCode = destination,
            ProviderRate = rate.ProviderRate,
            CustomerRate = rate.CustomerRate,
            RequestedAtUtc = now,
            ReceivedAtUtc = now,
            ProviderTimestampUtc = rate.EffectiveFrom,
            SendEnabled = true,
            SwapEnabled = true,
            Purpose = "SeededWorkflowTest"
        };
        db.FxProviderRateConfigurations.Add(configuration);
        db.FxRateObservations.Add(observation);
        var result = await consume(rate, observation.Id, ct);
        await db.SaveChangesAsync(ct);
        return result;
    }
}
