using KorridorX.Configuration;
using KorridorX.Services.Payments;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public class PayoutDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BlaaizOptions> _options;
    private readonly ILogger<PayoutDispatchWorker> _logger;

    public PayoutDispatchWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<BlaaizOptions> options,
        ILogger<PayoutDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configured = _options.Value;
        if (configured.IsEnabled && !configured.AutomaticPayoutDispatchEnabled)
            _logger.LogWarning(
                "KX-PAYOUT-005: Blaaiz is enabled but automatic recipient payout dispatch is disabled. Funded transfers require an authorized manual dispatch or the worker switch to be enabled.");
        else
            _logger.LogInformation(
                "Recipient payout worker configuration: provider enabled {ProviderEnabled}; automatic dispatch enabled {DispatchEnabled}; interval {IntervalSeconds}s.",
                configured.IsEnabled, configured.AutomaticPayoutDispatchEnabled,
                configured.PayoutDispatchIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.Value;

            if (options.IsEnabled && options.AutomaticPayoutDispatchEnabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var payoutService = scope.ServiceProvider.GetRequiredService<IPayoutService>();
                    var dispatched = await payoutService.DispatchPendingAsync(
                        Math.Min(options.ReconciliationBatchSize, 100),
                        stoppingToken);

                    if (dispatched > 0)
                    {
                        _logger.LogInformation("Dispatched {Count} pending KorridorX payouts.", dispatched);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Payout dispatch cycle failed.");
                }
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(10, options.PayoutDispatchIntervalSeconds)),
                stoppingToken);
        }
    }
}
