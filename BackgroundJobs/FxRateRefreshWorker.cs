using KorridorX.Services.Fx;

namespace KorridorX.BackgroundJobs;

public sealed class FxRateRefreshWorker(IServiceScopeFactory scopeFactory, IHostEnvironment environment,
    ILogger<FxRateRefreshWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        // No configured active schedules means no provider requests. Deployment itself never activates a configuration.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IFxProviderRateService>().RefreshDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("FX refresh cycle failed: {ErrorType}.", ex.GetType().Name); }
        }
    }
}
