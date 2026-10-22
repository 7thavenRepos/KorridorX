using KorridorX.Configuration;
using KorridorX.Services.WalletWithdrawals;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

// Runs in the API process like other bounded KorridorX workers. Both provider
// dispatch and read-only provider recovery are individually gated and use
// their own scoped, transactional service on every cycle.
public sealed class WalletWithdrawalProcessingWorker(
    IServiceScopeFactory scopes,
    IOptions<WalletWithdrawalProcessingOptions> options,
    ILogger<WalletWithdrawalProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IWalletWithdrawalExecutionService>();
                var batchSize = Math.Clamp(options.Value.BatchSize, 1, 50);
                var dispatched = await service.DispatchBatchAsync(batchSize, stoppingToken);
                var recovered = await service.RecoverBatchAsync(batchSize, stoppingToken);
                if (dispatched > 0 || recovered > 0)
                    logger.LogInformation("Wallet withdrawals: {Dispatched} claimed, {Recovered} checked.",
                        dispatched, recovered);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Wallet withdrawal processing cycle failed."); }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollIntervalSeconds, 15, 3600)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
