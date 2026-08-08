using KorridorX.Configuration;
using KorridorX.Services.Finance;
using Microsoft.Extensions.Options;

namespace KorridorX.BackgroundJobs;

public sealed class AccountingSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AccountingOptions> _options;
    private readonly ILogger<AccountingSyncWorker> _logger;

    public AccountingSyncWorker(IServiceScopeFactory scopeFactory, IOptions<AccountingOptions> options, ILogger<AccountingSyncWorker> logger)
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
            if (options.SyncWorkerEnabled)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IAccountingService>();
                    var result = await service.SyncAsync(DateTime.UtcNow.AddDays(-options.LookbackDays), DateTime.UtcNow.AddMinutes(1), stoppingToken);
                    _logger.LogInformation("Accounting sync completed. TransferFees={TransferFees}, FxSpread={FxSpread}, ProviderFees={ProviderFees}, SettlementVariances={SettlementVariances}, RefundReversals={RefundReversals}", result.TransferFeeEntries, result.FxSpreadEntries, result.ProviderFeeEntries, result.SettlementVarianceEntries, result.RefundReversalEntries);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Accounting synchronization worker failed.");
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(Math.Max(1, options.SyncIntervalMinutes)), stoppingToken);
        }
    }
}
