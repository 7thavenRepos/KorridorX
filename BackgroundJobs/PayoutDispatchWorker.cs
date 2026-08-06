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
