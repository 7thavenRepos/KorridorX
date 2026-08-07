using KorridorX.Configuration;
using KorridorX.Services.Treasury;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public sealed class TreasurySyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<TreasuryOptions> _options;
    private readonly ILogger<TreasurySyncWorker> _logger;

    public TreasurySyncWorker(IServiceScopeFactory scopeFactory, IOptions<TreasuryOptions> options, ILogger<TreasurySyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.Value;
            if (options.WalletSyncWorkerEnabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
                    await treasury.SyncProviderWalletsAsync(Guid.Empty, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Treasury provider wallet synchronization failed.");
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.WalletSyncIntervalMinutes)), stoppingToken);
        }
    }
}
