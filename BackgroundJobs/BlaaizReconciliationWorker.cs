using KorridorX.Configuration;
using KorridorX.Services.Reconciliation;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public class BlaaizReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BlaaizOptions> _options;
    private readonly ILogger<BlaaizReconciliationWorker> _logger;

    public BlaaizReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BlaaizOptions> options,
        ILogger<BlaaizReconciliationWorker> logger)
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

            if (options.IsEnabled && options.ReconciliationEnabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IProviderReconciliationService>();
                    var result = await service.ReconcilePendingAsync(options.ReconciliationBatchSize, stoppingToken);

                    if (result.Examined > 0)
                    {
                        _logger.LogInformation(
                            "Blaaiz reconciliation examined {Examined}, updated {Updated}, failed {Failed} provider transactions.",
                            result.Examined,
                            result.Updated,
                            result.Failed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Blaaiz reconciliation cycle failed.");
                }
            }

            await Task.Delay(
                TimeSpan.FromMinutes(Math.Max(1, options.ReconciliationIntervalMinutes)),
                stoppingToken);
        }
    }
}
